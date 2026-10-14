namespace Wanxiang.UI

open System.Collections.Generic

type ReconcilePlan<'V> = { Keep: int; Append: 'V list }

// 照抄旧 Views/ChatView.fs 对账段：messagePanel 换成本地 List，Keep/Append 由面板前后对比读出
type Reconciler<'K, 'V when 'K: equality>() =
    let cardCache = Dictionary<'K, 'V>()
    let mutable mountedKeys: 'K option array = [||]
    let messagePanel = List<'V>()
    member _.MountedKeys = Array.copy mountedKeys
    member _.CachedCount = cardCache.Count
    member _.Reconcile(desiredList: ('K option * (unit -> 'V)) list) : ReconcilePlan<'V> =
        let desired = List<'K option * (unit -> 'V)>(desiredList)
        let mutable common = 0
        while common < mountedKeys.Length
              && common < desired.Count
              && (match mountedKeys[common], fst desired[common] with
                  | Some mounted, Some wanted -> mounted = wanted
                  | _ -> false) do
            common <- common + 1
        if messagePanel.Count > common then
            messagePanel.RemoveRange(common, messagePanel.Count - common)
        for index in common .. desired.Count - 1 do
            let key, create = desired[index]
            let control =
                match key with
                | None -> create ()
                | Some k ->
                    match cardCache.TryGetValue k with
                    | true, cached -> cached
                    | _ ->
                        let created = create ()
                        cardCache[k] <- created
                        created
            messagePanel.Add control
        mountedKeys <- desired |> Seq.map fst |> Array.ofSeq
        let live = HashSet<'K>()
        for key in mountedKeys do
            match key with
            | Some k -> live.Add k |> ignore
            | None -> ()
        if cardCache.Count > live.Count then
            for stale in cardCache.Keys |> Seq.filter (live.Contains >> not) |> Array.ofSeq do
                cardCache.Remove stale |> ignore
        { Keep = common; Append = [ for i in common .. messagePanel.Count - 1 -> messagePanel[i] ] }
    member _.Clear() =
        cardCache.Clear()
        mountedKeys <- [||]
        messagePanel.Clear()
