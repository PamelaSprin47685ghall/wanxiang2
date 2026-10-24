namespace Wanxiang.Agent

open System
open System.Globalization
open System.Net.Http
open System.Net.Sockets
open System.Text.RegularExpressions

/// 从 HTTP 调用失败里读出事实：Retry-After 秒数、是否网络不可达、文字里的 5xx 状态、给人看的精简细节。
/// 只依赖 BCL；不做归类，归类归调用方。从旧 ProviderFailure 拆出，行为不变。
module HttpFailureFacts =

    let private clampPositiveSeconds (totalSeconds: float) : int option =
        if Double.IsNaN totalSeconds || totalSeconds <= 0.0 then
            None
        elif totalSeconds >= float Int32.MaxValue then
            Some Int32.MaxValue
        else
            let s = int totalSeconds
            if s > 0 then Some s else None

    /// 一个 Retry-After 头值：秒数或 HTTP 日期；不为正时 None。
    let retryAfterOfValue (now: DateTimeOffset) (value: string) : int option =
        if String.IsNullOrWhiteSpace value then
            None
        else
            let trimmed = value.Trim()

            match Int64.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, seconds ->
                if seconds <= 0L then
                    None
                elif seconds >= int64 Int32.MaxValue then
                    Some Int32.MaxValue
                else
                    Some(int seconds)
            | _ ->
                // 小数（如 "1.5"）不是合法 HTTP-date，拦截掉避免被 DateTimeOffset.TryParse 当成「1 月 5 日」。
                match Double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture) with
                | true, _ -> None
                | _ ->
                    match
                        DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
                    with
                    | true, at -> clampPositiveSeconds (at - now).TotalSeconds
                    | _ -> None

    let retryAfterOfResponse (response: HttpResponseMessage) : int option =
        try
            if isNull response || isNull response.Headers then
                None
            elif not (isNull response.Headers.RetryAfter) then
                let ra = response.Headers.RetryAfter

                if ra.Delta.HasValue then
                    clampPositiveSeconds ra.Delta.Value.TotalSeconds
                elif ra.Date.HasValue then
                    clampPositiveSeconds (ra.Date.Value - DateTimeOffset.UtcNow).TotalSeconds
                else
                    None
            else
                let mutable values: Collections.Generic.IEnumerable<string> = null

                if response.Headers.TryGetValues("Retry-After", &values) then
                    values |> Seq.tryHead |> Option.bind (retryAfterOfValue DateTimeOffset.UtcNow)
                else
                    None
        with _ ->
            None

    /// 自有传输把解析好的秒数塞进异常 Data["RetryAfterSeconds"]。
    let retryAfterOfData (ex: exn) : int option =
        try
            if ex.Data.Contains "RetryAfterSeconds" then
                match ex.Data["RetryAfterSeconds"] with
                | :? int as s when s > 0 -> Some s
                | _ -> None
            else
                None
        with _ ->
            None

    /// 去 HTML 标签、压空白、截到 600 字符：足够定位问题，又不把整份上游响应灌进 UI。
    let detail (text: string) : string =
        let sanitized =
            if isNull text then
                ""
            else
                Regex.Replace(Regex.Replace(text, @"<[^>]+>", " "), @"[\r\n\t ]+", " ")

        let trimmed = sanitized.Trim()

        if trimmed.Length > 600 then
            trimmed.Substring(0, 600) + "…"
        else
            trimmed

    /// 连不上：套接字异常、无状态码的 HttpRequestException、常见的不可达文字，沿 InnerException 往下找。
    let rec isNetwork (ex: exn) : bool =
        match ex with
        | null -> false
        | :? SocketException -> true
        | :? HttpRequestException as hre when not hre.StatusCode.HasValue -> true
        | _ ->
            let msg =
                if isNull ex.Message then
                    ""
                else
                    ex.Message.ToLowerInvariant()

            msg.Contains "connection refused"
            || msg.Contains "actively refused"
            || msg.Contains "name or service not known"
            || msg.Contains "no such host"
            || msg.Contains "network is unreachable"
            || msg.Contains "network unreachable"
            || msg.Contains "connection reset"
            || (not (isNull ex.InnerException) && isNetwork ex.InnerException)

    /// 文字里提到的 5xx 状态（500/502/503/504 或其英文短语）。
    let fiveXxIn (text: string) : int option =
        if String.IsNullOrWhiteSpace text then
            None
        else
            let lowered = text.ToLowerInvariant()

            if lowered.Contains "500" || lowered.Contains "internal server error" then
                Some 500
            elif lowered.Contains "502" || lowered.Contains "bad gateway" then
                Some 502
            elif lowered.Contains "503" || lowered.Contains "service unavailable" then
                Some 503
            elif lowered.Contains "504" || lowered.Contains "gateway timeout" then
                Some 504
            else
                None
