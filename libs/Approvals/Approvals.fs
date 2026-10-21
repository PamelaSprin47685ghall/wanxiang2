namespace Wanxiang.Server

open System
open System.Collections.Concurrent
open System.Threading.Tasks

/// 等待中的审批表：先到者胜，处理过的结果记住一段时间，好告诉晚到者「已处理」。
/// 不懂工具、对话和线协议；'Item 是调用方挂在审批上的任意数据，'Memo 是要记住的结果摘要。
type ApprovalDesk<'Item, 'Outcome, 'Memo>(memo: 'Outcome -> 'Memo, recentLimit: int) =
    let pending = ConcurrentDictionary<Guid, 'Item * TaskCompletionSource<'Outcome>>()
    // 「移出等待」和「记入最近」必须不可分，否则落败者可能在两步之间查不到结果。
    let gate = obj ()
    let recent = ConcurrentDictionary<Guid, 'Memo>()
    let order = ConcurrentQueue<Guid>()

    let remember id m =
        recent[id] <- m
        order.Enqueue id

        while order.Count > recentLimit do
            match order.TryDequeue() with
            | true, old -> recent.TryRemove old |> ignore
            | _ -> ()

    /// 登记一个审批，返回等待结果的任务。续体异步执行，结束方不会被等待者卡住。
    member _.Open(id: Guid, item: 'Item) : Task<'Outcome> =
        let tcs =
            TaskCompletionSource<'Outcome>(TaskCreationOptions.RunContinuationsAsynchronously)

        pending[id] <- (item, tcs)
        tcs.Task

    /// 以 outcome 结束审批。只有第一次成功；成功时先调用 beforeComplete，再交出结果。
    member _.Resolve(id: Guid, outcome: 'Outcome, beforeComplete: 'Item -> 'Memo -> unit) : bool =
        let taken =
            lock gate (fun () ->
                match pending.TryRemove id with
                | true, (item, tcs) ->
                    let m = memo outcome
                    remember id m
                    Some(item, tcs, m)
                | _ -> None)

        match taken with
        | Some(item, tcs, m) ->
            beforeComplete item m
            tcs.TrySetResult outcome |> ignore
            true
        | None -> false

    /// 最近处理过的审批的结果摘要；与 Resolve 互斥，不会读到一半。
    member _.Recent(id: Guid) : 'Memo option =
        lock gate (fun () ->
            match recent.TryGetValue id with
            | true, m -> Some m
            | _ -> None)

    /// 仍在等待的审批。
    member _.Pending: (Guid * 'Item) list = [ for kv in pending -> kv.Key, fst kv.Value ]

    member _.Count = pending.Count
