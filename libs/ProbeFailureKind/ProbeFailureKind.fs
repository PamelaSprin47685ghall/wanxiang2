namespace Wanxiang.UI

open System

/// 模型探测失败的类型（与主机 `ProviderCatalog.ProbeFailure` 的稳定错误码一一对应）。
/// 界面按类型决定下一步，不再靠文案关键词判断。
type ProbeFailureKind =
    | ProbeInvalidKey
    | ProbeEndpointNotFound
    | ProbeRateLimited
    | ProbeServerError
    | ProbeUnreachable
    | ProbeTimedOut
    | ProbeBadResponse
    | ProbeRejected
    | ProbeCancelled
    /// 旧主机（无错误码前缀）或未知码
    | ProbeUnclassified

module ProbeFailureKind =

    let ofCode (code: string) =
        match code with
        | "invalid-key" -> ProbeInvalidKey
        | "endpoint-not-found" -> ProbeEndpointNotFound
        | "rate-limited" -> ProbeRateLimited
        | "server-error" -> ProbeServerError
        | "unreachable" -> ProbeUnreachable
        | "timeout" -> ProbeTimedOut
        | "bad-response" -> ProbeBadResponse
        | "rejected" -> ProbeRejected
        | "cancelled" -> ProbeCancelled
        | _ -> ProbeUnclassified

    /// 解析线上 `[code] 说明`。没有前缀时类型为 ProbeUnclassified，说明原样保留。
    let parse (text: string) : ProbeFailureKind * string =
        if not (String.IsNullOrWhiteSpace text) && text.StartsWith("[", StringComparison.Ordinal) then
            let close = text.IndexOf ']'
            if close > 1 then ofCode (text.Substring(1, close - 1)), text.Substring(close + 1).TrimStart()
            else ProbeUnclassified, text
        else
            ProbeUnclassified, (if isNull text then "" else text)

    /// 失败后值得给「改用兼容端点 / 改用原生接口」这类下一步的类型。
    let suggestsTransportSwitch (kind: ProbeFailureKind) =
        match kind with
        | ProbeEndpointNotFound | ProbeBadResponse -> true
        | _ -> false
