namespace Wanxiang.UI

open System

// 原样摘自 AppShell.fs：字段声明（第 63、64、72 行）、ProbeProvider、ExpireHostRequests、
// HandleEvent 的 ProviderProbeResult / SearchResults / SearchFailed 分支、BuildSidebar 的 SearchHandler。副作用换成返回值。
type PendingProbes() =
    let pendingProbes = System.Collections.Generic.Dictionary<Guid, string * DateTimeOffset>()
    member _.Track(requestId: Guid, providerId: string, at: DateTimeOffset) = pendingProbes[requestId] <- (providerId, at)
    member _.Resolve(requestId: Guid) = pendingProbes.Remove requestId |> ignore
    member _.Expire(now: DateTimeOffset, timeout: TimeSpan) : (Guid * string) list =
        let dueProbes = [ for KeyValue(id, (pid, at)) in pendingProbes do if now - at >= timeout then id, pid ]
        for (id, pid) in dueProbes do
            pendingProbes.Remove id |> ignore
        dueProbes

type SearchInFlight() =
    let mutable searchSentAt: DateTimeOffset option = None
    let mutable latestSearchQueryId: Guid option = None
    member _.Latest = latestSearchQueryId
    member _.Sent(queryId: Guid, at: DateTimeOffset) =
        latestSearchQueryId <- Some queryId
        searchSentAt <- Some at
    member _.Accept(queryId: Guid) : bool =
        let d = {| queryId = queryId |}
        if latestSearchQueryId = Some d.queryId then
            searchSentAt <- None
            true
        else false
    member _.Expire(now: DateTimeOffset, timeout: TimeSpan) : bool =
        match searchSentAt with
        | Some at when now - at >= timeout ->
            searchSentAt <- None
            latestSearchQueryId <- None
            true
        | _ -> false
