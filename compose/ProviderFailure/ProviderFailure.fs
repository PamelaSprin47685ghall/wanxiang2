namespace Wanxiang.Agent

open System
open System.Net.Http
open System.Net.Sockets
open System.Threading.Tasks
open Wanxiang.Core

/// 把 Provider 端异常翻译成面向用户的结构化错误。
///
/// 上游 SDK 只给 `ClientResultException`（带 HTTP 状态码）或裸 `HttpRequestException`，
/// 直接把 `ex.Message` 抛给用户既看不懂也可能带出 URL/头片段，因此在这里统一归类。
module ProviderFailure =

    /// 上游响应正文里能识别出的语义信号（状态码不足以区分时使用）。
    let private bodySignals =
        [ "context_length_exceeded", ContextTooLong
          "context length", ContextTooLong
          "maximum context", ContextTooLong
          "too many tokens", ContextTooLong
          "reduce the length", ContextTooLong
          "content_filter", ContentFiltered
          "content management policy", ContentFiltered
          "safety", ContentFiltered
          "model_not_found", ModelNotFound
          "does not exist", ModelNotFound
          "invalid_api_key", ProviderAuthFailed
          "incorrect api key", ProviderAuthFailed ]

    let private detailOf (text: string) : string = HttpFailureFacts.detail text

    let private signalFromBody (text: string) : GenerationErrorKind option =
        if String.IsNullOrWhiteSpace text then None
        else
            let lowered = text.ToLowerInvariant()
            bodySignals |> List.tryPick (fun (needle, kind) -> if lowered.Contains needle then Some kind else None)

    let private retryAfterOfClientResult (ex: System.ClientModel.ClientResultException) : int option =
        try
            let response = ex.GetRawResponse()
            if isNull response then None
            else
                let mutable value = ""
                if response.Headers.TryGetValue("Retry-After", &value) then
                    HttpFailureFacts.retryAfterOfValue DateTimeOffset.UtcNow value
                else None
        with _ -> None

    let retryAfterOfResponse (response: HttpResponseMessage) : int option = HttpFailureFacts.retryAfterOfResponse response

    let private retryAfterOfHttpException (hre: HttpRequestException) : int option = HttpFailureFacts.retryAfterOfData hre

    let private kindOfStatus (status: int) (body: string) : GenerationErrorKind =
        match signalFromBody body with
        | Some kind -> kind
        | None ->
            match status with
            | 400 | 422 -> ProviderBadRequest
            | 401 | 403 -> ProviderAuthFailed
            | 404 -> ModelNotFound
            | 408 -> ProviderTimeout
            | 429 -> ProviderRateLimited
            | s when s >= 500 -> ProviderUnavailable
            | _ -> UnknownFailure

    let private serverUnavailableMessage (providerLabel: string) (status: int option) : string =
        match status with
        | Some 500 -> sprintf "「%s」服务暂时不可用（HTTP 500）。" providerLabel
        | Some 502 -> sprintf "「%s」服务暂时不可用（HTTP 502）。" providerLabel
        | Some 503 -> sprintf "「%s」服务暂时不可用（HTTP 503）。" providerLabel
        | Some 504 -> sprintf "「%s」服务暂时不可用（HTTP 504）。" providerLabel
        | Some s when s >= 500 -> sprintf "「%s」服务暂时不可用（HTTP %d）。" providerLabel s
        | _ -> sprintf "「%s」服务暂时不可用。" providerLabel

    let private networkErrorMessage (providerLabel: string) : string =
        sprintf "无法连接「%s」，请检查网络或地址。" providerLabel

    let private messageOf (kind: GenerationErrorKind) (providerLabel: string) (model: string) : string =
        match kind with
        | ProviderAuthFailed -> sprintf "「%s」拒绝了当前凭据。" providerLabel
        | ProviderRateLimited -> sprintf "「%s」限流了这次请求。" providerLabel
        | ProviderTimeout -> sprintf "等待「%s」响应超时。" providerLabel
        | ProviderUnavailable -> networkErrorMessage providerLabel
        | ProviderBadRequest -> sprintf "「%s」认为这次请求参数不合法。" providerLabel
        | ContextTooLong -> "本次上下文超出模型能接受的长度。"
        | ModelNotFound -> sprintf "「%s」上找不到模型 %s。" providerLabel model
        | ContentFiltered -> "内容被模型服务安全策略拦截。"
        | ToolFailed -> "工具执行失败。"
        | ConfigInvalid -> "对话配置无效。"
        | UnknownFailure -> sprintf "调用「%s」时发生未预期的错误。" providerLabel

    let private isNetworkException (ex: exn) : bool = HttpFailureFacts.isNetwork ex

    let private tryParse5xxStatus (text: string) : int option = HttpFailureFacts.fiveXxIn text

    /// 归类一个 Provider 调用异常。
    /// 失败异常里携带的限流信号（决策 241）：SDK 的原始响应头，或自有传输塞进 Data 的解析结果。
    /// 沿 InnerException 往下找，SDK 有时会包一层。
    let rec rateLimitOf (ex: exn) : ProviderRateLimit option =
        if isNull ex then None
        else
            let direct =
                try
                    match ex with
                    | :? System.ClientModel.ClientResultException as cre ->
                        let response = cre.GetRawResponse()
                        if isNull response || isNull response.Headers then None
                        else
                            let lookup (name: string) =
                                let mutable value = ""
                                if response.Headers.TryGetValue(name, &value) && not (String.IsNullOrWhiteSpace value) then Some value
                                else None
                            ProviderRateLimit.ofHeaders DateTimeOffset.UtcNow lookup
                    | _ when not (isNull ex.Data) && ex.Data.Contains "RateLimit" ->
                        match ex.Data["RateLimit"] with
                        | :? ProviderRateLimit as limit -> Some limit
                        | _ -> None
                    | _ -> None
                with _ -> None
            match direct with
            | Some _ -> direct
            | None -> rateLimitOf ex.InnerException

    let classify (providerLabel: string) (model: string) (ex: exn) : GenerationError =
        match ex with
        | :? System.ClientModel.ClientResultException as cre ->
            let body =
                try
                    let response = cre.GetRawResponse()
                    if isNull response then cre.Message
                    else
                        let content = response.Content
                        if isNull content then cre.Message else content.ToString()
                with _ -> cre.Message
            let kind =
                if cre.Status <= 0 then ProviderUnavailable
                else kindOfStatus cre.Status body
            let message =
                match kind with
                | ProviderUnavailable when cre.Status >= 500 ->
                    serverUnavailableMessage providerLabel (Some cre.Status)
                | ProviderUnavailable ->
                    networkErrorMessage providerLabel
                | other ->
                    messageOf other providerLabel model
            { GenerationError.create kind message with
                detail = Some(detailOf (if cre.Status > 0 then sprintf "HTTP %d · %s" cre.Status body else cre.Message))
                retryAfterSeconds = retryAfterOfClientResult cre
                retryable =
                    match kind with
                    | ProviderRateLimited | ProviderTimeout | ProviderUnavailable | UnknownFailure -> true
                    | _ -> false }
        | :? TaskCanceledException
        | :? TimeoutException ->
            GenerationError.create ProviderTimeout (messageOf ProviderTimeout providerLabel model)
            |> GenerationError.withDetail (detailOf ex.Message)
        | :? HttpRequestException as hre ->
            let statusCode = hre.StatusCode |> Option.ofNullable |> Option.map int
            let kind =
                match statusCode with
                | Some code -> kindOfStatus code hre.Message
                | None -> ProviderUnavailable
            let message =
                match kind with
                | ProviderUnavailable ->
                    match statusCode with
                    | Some code when code >= 500 ->
                        serverUnavailableMessage providerLabel (Some code)
                    | _ ->
                        networkErrorMessage providerLabel
                | other ->
                    messageOf other providerLabel model
            { GenerationError.create kind message with
                detail = Some(detailOf hre.Message)
                retryAfterSeconds = retryAfterOfHttpException hre
                retryable =
                    match kind with
                    | ProviderRateLimited | ProviderTimeout | ProviderUnavailable | UnknownFailure -> true
                    | _ -> false }
        | :? SocketException as se ->
            let msg = networkErrorMessage providerLabel
            { GenerationError.create ProviderUnavailable msg with
                detail = Some(detailOf se.Message)
                retryable = true }
        | _ ->
            match signalFromBody ex.Message with
            | Some kind ->
                GenerationError.create kind (messageOf kind providerLabel model)
                |> GenerationError.withDetail (detailOf ex.Message)
            | None ->
                if isNetworkException ex then
                    let msg = networkErrorMessage providerLabel
                    { GenerationError.create ProviderUnavailable msg with
                        detail = Some(detailOf ex.Message)
                        retryable = true }
                else
                    match tryParse5xxStatus ex.Message with
                    | Some status ->
                        let msg = serverUnavailableMessage providerLabel (Some status)
                        { GenerationError.create ProviderUnavailable msg with
                            detail = Some(detailOf ex.Message)
                            retryable = true }
                    | None ->
                        GenerationError.create UnknownFailure (messageOf UnknownFailure providerLabel model)
                        |> GenerationError.withDetail (detailOf ex.Message)
