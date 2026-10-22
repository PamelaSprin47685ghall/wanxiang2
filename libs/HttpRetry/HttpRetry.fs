namespace Wanxiang.Agent

open System
open System.Net.Http
open System.Threading
open System.Threading.Tasks

/// 对模型提供方的 HTTP 请求：首字节之前的退避重试，以及把非 2xx 翻成带状态码的异常。
module HttpRetry =

    /// 调用方按状态码给错误归类，所以必须是带状态码的 HttpRequestException，换成别的类型就只能归为「未知错误」。
    /// 非 2xx 时返回要抛出的异常；2xx 返回 None。异常带状态码，Retry-After 记在 Data["RetryAfterSeconds"]。
    let statusError (response: HttpResponseMessage) (body: string) : HttpRequestException option =
        if response.IsSuccessStatusCode then
            None
        else
            let detail =
                let raw =
                    if String.IsNullOrWhiteSpace body then
                        response.ReasonPhrase
                    else
                        body

                sprintf "(%d) %s" (int response.StatusCode) raw

            let ex = new HttpRequestException(detail, null, Nullable response.StatusCode)

            try
                if not (isNull response.Headers.RetryAfter) then
                    let ra = response.Headers.RetryAfter

                    if ra.Delta.HasValue then
                        ex.Data["RetryAfterSeconds"] <- int ra.Delta.Value.TotalSeconds
                    elif ra.Date.HasValue then
                        ex.Data["RetryAfterSeconds"] <- int (ra.Date.Value - DateTimeOffset.UtcNow).TotalSeconds
            with _ ->
                ()

            Some ex

    let private transient (status: System.Net.HttpStatusCode) =
        let code = int status
        code = 429 || code = 408 || code >= 500

    /// 指数退避的等待时长：250 ms 起步，每次翻倍，上限 8 秒。
    ///
    /// 单独抽出来是为了能直接断言边界：位移 `1 <<< (attempt - 1)` 在 attempt ≥ 32 时
    /// 会溢出成负数（`1 <<< 31` 本身已是 `Int32.MinValue`），乘出来的等待是负值，
    /// `Task.Delay` 收到负延迟会直接抛异常。这里用浮点幂再收敛到上限，位移彻底不参与。
    let backoffDelay (attempt: int) : TimeSpan =
        let exponent = max 0 (attempt - 1)
        let scaled = 250.0 * (2.0 ** float exponent)
        TimeSpan.FromMilliseconds(min 8000.0 scaled)

    /// 按 maxRetries 退避重试。
    ///
    /// 只在**首个字节之前**重试：先用 ResponseHeadersRead 拿到状态行，
    /// 尚未向下游发出任何增量，此时重发是安全的；一旦开始流式输出就绝不重试，
    /// 否则用户会看到同一段回答出现两次。
    let sendWithRetry
        (http: HttpClient)
        (makeRequest: unit -> HttpRequestMessage)
        (maxRetries: int)
        (ct: CancellationToken)
        : Task<HttpResponseMessage> =
        task {
            let attempts = max 1 (maxRetries + 1)
            let mutable attempt = 0
            let mutable result = Unchecked.defaultof<HttpResponseMessage>
            let mutable finished = false

            while not finished do
                attempt <- attempt + 1
                let last = attempt >= attempts
                let mutable retryAfter = TimeSpan.Zero

                try
                    let request = makeRequest ()
                    let! response = http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)

                    if response.IsSuccessStatusCode || last || not (transient response.StatusCode) then
                        result <- response
                        finished <- true
                    else
                        retryAfter <-
                            match response.Headers.RetryAfter with
                            | null -> TimeSpan.Zero
                            | value when value.Delta.HasValue -> value.Delta.Value
                            | _ -> TimeSpan.Zero

                        response.Dispose()
                // 取消不是失败，直接向上传播；最后一次尝试的异常同样不吞，
                // 交给 ProviderFailure 归类成面向用户的错误。
                with ex when not last && not (ex :? OperationCanceledException) ->
                    ignore ex

                if not finished then
                    // 指数退避，上限 8 秒；上游给了 Retry-After 就听它的
                    let backoff =
                        if retryAfter > TimeSpan.Zero then
                            retryAfter
                        else
                            backoffDelay attempt

                    do! Task.Delay(backoff, ct)

            return result
        }
