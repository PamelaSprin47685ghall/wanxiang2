namespace Wanxiang.UI

open System
open System.Collections.Generic

/// 决策 443/444：发给主机、等它回应的探测请求。到点仍没回应的统一收尾。
/// 迁自旧 AppShell 的 pendingProbes 字典及 ProbeProvider / ProviderProbeResult / ExpireHostRequests 里对它的读写。
type PendingProbes() =
    let pending = Dictionary<Guid, string * DateTimeOffset>()

    member _.Track(requestId: Guid, providerId: string, at: DateTimeOffset) = pending[requestId] <- (providerId, at)

    /// 回应到达：不再等待。
    member _.Resolve(requestId: Guid) = pending.Remove requestId |> ignore

    /// 取出所有到点的请求 (requestId, providerId)，按发出顺序。
    member _.Expire(now: DateTimeOffset, timeout: TimeSpan) : (Guid * string) list =
        let due =
            [
                for KeyValue(id, (pid, at)) in pending do
                    if now - at >= timeout then
                        id, pid
            ]

        for (id, _) in due do
            pending.Remove id |> ignore

        due

/// W4：消息正文搜索的在途查询。每次发出换一个 queryId，只认最近一次；更早的结果迟到即丢。
/// 迁自旧 AppShell 的 latestSearchQueryId / searchSentAt。
type SearchInFlight() =
    let mutable latest: Guid option = None
    let mutable sentAt: DateTimeOffset option = None

    member _.Latest = latest

    member _.Sent(queryId: Guid, at: DateTimeOffset) =
        latest <- Some queryId
        sentAt <- Some at

    /// 结果或失败到达：是最近一次的才接收（并停止计时）。
    member _.Accept(queryId: Guid) : bool =
        if latest = Some queryId then
            sentAt <- None
            true
        else
            false

    /// 到点仍没回应：放弃这次查询，返回 true 由宿主显示失败。
    member _.Expire(now: DateTimeOffset, timeout: TimeSpan) : bool =
        match sentAt with
        | Some at when now - at >= timeout ->
            sentAt <- None
            latest <- None
            true
        | _ -> false
