namespace Wanxiang.Core

open System

/// 全局连续提交序号（从 1 开始，跨 UTC 日期文件不归零，不允许空洞）。
type CommitId = uint64

/// 单次生成用量（generation.finished 透传，不落 NDJSON）。
type GenerationUsage = {
    promptTokens: int option
    completionTokens: int option
    cachedTokens: int option
    totalTokens: int option
    durationMs: int64 option
}

module GenerationUsage =

    let empty =
        { promptTokens = None
          completionTokens = None
          cachedTokens = None
          totalTokens = None
          durationMs = None }

    let formatSummary (u: GenerationUsage) : string option =
        let total =
            match u.totalTokens with
            | Some t -> Some t
            | None ->
                match u.promptTokens, u.completionTokens with
                | Some p, Some c -> Some(p + c)
                | _ -> None
        match total with
        | None -> None
        | Some t ->
            let tok =
                if t >= 1000 then sprintf "%.1fk tok" (float t / 1000.0)
                else sprintf "%d tok" t
            match u.durationMs with
            | Some ms when ms >= 1000L -> Some(sprintf "%s · %.1fs" tok (float ms / 1000.0))
            | Some ms -> Some(sprintf "%s · %dms" tok (int ms))
            | None -> Some tok
 
     /// 缓存命中数（有值且 > 0 时）。客户端据此在用量角标上补一段「缓存 N」：
    /// 缓存命中省钱，用户看得到才可能去把 PromptCache 打开。0 与 None 一样不显示。
    /// 只给数字：文案由展示层决定，format* 一族职责是「取数」而非「排版」。
    let cachedCount (u: GenerationUsage) : int option =
        match u.cachedTokens with
        | Some c when c > 0 -> Some c
        | _ -> None

    let formatDetail (u: GenerationUsage) : string =
        let part label v =
            match v with
            | Some n -> sprintf "%s %d" label n
            | None -> ""
        let parts =
            [| part "输入" u.promptTokens
               part "输出" u.completionTokens
               part "缓存" u.cachedTokens
               part "合计" u.totalTokens |]
            |> Array.filter (fun s -> s <> "")
        let body = if parts.Length = 0 then "暂无 token 数据" else String.Join(" · ", parts)
        match u.durationMs with
        | Some ms when ms >= 1000L -> body + sprintf " · 耗时 %.1fs" (float ms / 1000.0)
        | Some ms -> body + sprintf " · 耗时 %dms" (int ms)
        | None -> body

/// 生成失败的结构化分类。决定 UI 如何呈现、是否给「重试」入口、
/// 以及提示用户该去改哪里（密钥 / 模型 / 网络 / 内容）。
type GenerationErrorKind =
    /// 密钥无效或权限不足（401 / 403）
    | ProviderAuthFailed
    /// 触发限流（429）
    | ProviderRateLimited
    /// 请求超时或流中断
    | ProviderTimeout
    /// 主机不可用 / 网络失败（5xx、DNS、连接被拒）
    | ProviderUnavailable
    /// 请求被拒（400）：参数不被该模型支持等
    | ProviderBadRequest
    /// 上下文超长
    | ContextTooLong
    /// 模型不存在或无权访问（404）
    | ModelNotFound
    /// 被内容策略拦截
    | ContentFiltered
    /// 工具执行失败
    | ToolFailed
    /// 对话配置指向的 provider/模型已不存在
    | ConfigInvalid
    /// 未归类
    | UnknownFailure

/// 面向用户的生成失败描述（随 generation.finished 下发）。
type GenerationError = {
    kind: GenerationErrorKind
    /// 面向用户的一句话说明
    message: string
    /// 面向开发者的原始细节（异常文本 / 上游响应片段）
    detail: string option
    /// 是否值得重试（UI 给「重试」按钮）
    retryable: bool
    /// 建议等待秒数（来自 Retry-After）
    retryAfterSeconds: int option
}

module GenerationErrorKind =

    let code (k: GenerationErrorKind) : string =
        match k with
        | ProviderAuthFailed -> "provider-auth-failed"
        | ProviderRateLimited -> "provider-rate-limited"
        | ProviderTimeout -> "provider-timeout"
        | ProviderUnavailable -> "provider-unavailable"
        | ProviderBadRequest -> "provider-bad-request"
        | ContextTooLong -> "context-too-long"
        | ModelNotFound -> "model-not-found"
        | ContentFiltered -> "content-filtered"
        | ToolFailed -> "tool-failed"
        | ConfigInvalid -> "config-invalid"
        | UnknownFailure -> "unknown-failure"

    let ofCode (s: string) : GenerationErrorKind =
        match s with
        | "provider-auth-failed" -> ProviderAuthFailed
        | "provider-rate-limited" -> ProviderRateLimited
        | "provider-timeout" -> ProviderTimeout
        | "provider-unavailable" -> ProviderUnavailable
        | "provider-bad-request" -> ProviderBadRequest
        | "context-too-long" -> ContextTooLong
        | "model-not-found" -> ModelNotFound
        | "content-filtered" -> ContentFiltered
        | "tool-failed" -> ToolFailed
        | "config-invalid" -> ConfigInvalid
        | _ -> UnknownFailure

    /// 针对模型服务不可用（5xx 主机故障 vs 本地网络不可达）的细分建议。
    let hintUnavailable (isServerOutage: bool) : string =
        if isServerOutage then "模型服务暂时过载或维护中，稍等后可重试。"
        else "无法连接到模型服务网络，请检查网络设置。"

    /// 该类别下用户能做什么（UI 在错误卡片里给出的行动建议）。
    let hint (k: GenerationErrorKind) : string =
        match k with
        | ProviderAuthFailed -> "请在设置中检查该模型服务的 API Key。"
        | ProviderRateLimited -> "模型服务限流或额度已用尽：稍等再试，或换个模型继续。"
        | ProviderTimeout -> "网络或模型服务响应过慢，可直接重试。"
        | ProviderUnavailable -> "模型服务暂时过载或网络异常，请检查网络设置或稍后重试。"
        | ProviderBadRequest -> "当前模型不接受这些生成参数，请到对话设置调整。"
        | ContextTooLong -> "对话太长了，可新建对话或删除部分早期消息。"
        | ModelNotFound -> "该模型不可用，请在设置中确认模型名称。"
        | ContentFiltered -> "内容被模型服务的安全策略拦截。"
        | ToolFailed -> "工具执行失败，请检查工具配置。"
        | ConfigInvalid -> "对话使用的模型服务或模型已不存在，请到对话设置重新选择。"
        | UnknownFailure -> "可以先重试；若持续失败请查看主机日志。"

module GenerationError =

    /// 判断指定错误类别是否属于可重试的临时故障（标准 HTTP 语义）。
    let isRetryable (kind: GenerationErrorKind) : bool =
        match kind with
        | ProviderRateLimited | ProviderTimeout | ProviderUnavailable | UnknownFailure -> true
        | ProviderAuthFailed | ProviderBadRequest | ContextTooLong | ModelNotFound | ContentFiltered | ToolFailed | ConfigInvalid -> false

    let create (kind: GenerationErrorKind) (message: string) : GenerationError =
        { kind = kind
          message = message
          detail = None
          retryable = isRetryable kind
          retryAfterSeconds = None }

    let withDetail (detail: string) (e: GenerationError) : GenerationError =
        { e with detail = if System.String.IsNullOrWhiteSpace detail then None else Some detail }

    /// 根据具体失败信息与技术细节提供更有针对性的行动建议。
    /// 区分 502/503/504 等模型服务过载/维护，与本地连接断开/DNS 异常。
    let hint (e: GenerationError) : string =
        match e.kind with
        | ProviderUnavailable ->
            let combined =
                let d = e.detail |> Option.defaultValue ""
                sprintf "%s %s" e.message d
            let is5xx =
                combined.Contains "500" || combined.Contains "502" || combined.Contains "503" || combined.Contains "504"
                || combined.Contains "Bad Gateway" || combined.Contains "Service Unavailable"
                || combined.Contains "Gateway Timeout" || combined.Contains "Internal Server Error"
                || combined.Contains "过载" || combined.Contains "维护"
            let isNetwork =
                combined.Contains "无法连接" || combined.Contains "Connection refused"
                || combined.Contains "actively refused" || combined.Contains "no such host"
                || combined.Contains "name or service not known" || combined.Contains "network is unreachable"
                || combined.Contains "network unreachable" || combined.Contains "connection reset"
                || combined.Contains "SocketException"
            if is5xx && not isNetwork then
                GenerationErrorKind.hintUnavailable true
            elif isNetwork then
                GenerationErrorKind.hintUnavailable false
            else
                GenerationErrorKind.hint e.kind
        | other -> GenerationErrorKind.hint other

    /// 供 UI 单行展示：说明 + 行动建议。
    let display (e: GenerationError) : string =
        let h = hint e
        if e.message.Contains h then e.message
        elif e.message.EndsWith("。") || e.message.EndsWith(" ") then e.message + h
        else e.message.TrimEnd() + " " + h
