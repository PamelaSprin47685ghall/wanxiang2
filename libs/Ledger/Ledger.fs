namespace Ledger

open System
open System.Collections.Generic

/// 事件账本 actor：私有状态为领域状态、id 序列、幂等索引、位置索引、在途提交与等待队列。
/// 只通过 Post 接收 LedgerIn，只通过 Outbox 发出 LedgerOut；不碰磁盘、不读时钟、不调用任何外部对象。
/// 消息逐条处理（邮箱 + 蹦床）：在 Outbox 处理器里再 Post 也只是入队，不会重入。
type Ledger<'S, 'E, 'P>(apply: 'S -> Commit<'E> -> Result<'S, string>, from: Checkpoint<'S, 'P>) =
    let gate = obj ()
    let mailbox = Queue<LedgerIn<'E, 'P>>()
    let mutable draining = false
    let outbox = Event<LedgerOut<'E, 'P>>()

    let mutable state = from.state
    let mutable lastId = from.throughId
    let mutable stamps = from.stamps
    let positions = ResizeArray<'P>(from.positions)
    /// 在途提交：已发出 AppendRequested、尚未收到回复
    let mutable inFlight : (Guid * Commit<'E> * 'S) option = None
    /// 等待裁决的提交请求（在途提交落定后才裁决，保证按已落定的状态判断）
    let waiting = Queue<Guid * 'E list * DateTimeOffset * Stamp option>()

    do
        if uint64 positions.Count <> from.throughId then
            invalidArg (nameof from) (sprintf "checkpoint has %d positions for %d commits" positions.Count from.throughId)

    let record (c: Commit<'E>) (s: 'S) (p: 'P) =
        state <- s
        lastId <- c.id
        positions.Add p
        match c.stamp with
        | Some st -> stamps <- stamps |> Map.add st.commandId (st.hash, c.id)
        | None -> ()

    /// 裁决等待队列的队首，直到发出一条 AppendRequested 或队列空。
    let rec decideNext (emit: LedgerOut<'E, 'P> -> unit) =
        if inFlight.IsNone && waiting.Count > 0 then
            let ticket, events, at, stamp = waiting.Dequeue()
            let seen = stamp |> Option.bind (fun s -> stamps |> Map.tryFind s.commandId |> Option.map (fun v -> s, v))
            match seen with
            | Some(s, (h, id)) when h = s.hash -> emit (Refused(ticket, Duplicate(id, positions[int id - 1]))); decideNext emit
            | Some(_, (_, id)) -> emit (Refused(ticket, StampConflict id)); decideNext emit
            | None ->
                let c = { id = lastId + 1UL; at = at; stamp = stamp; events = events }
                match apply state c with
                | Error msg -> emit (Refused(ticket, Rejected msg)); decideNext emit
                | Ok s ->
                    inFlight <- Some(ticket, c, s)
                    emit (AppendRequested(ticket, c))

    let handle (msg: LedgerIn<'E, 'P>) (emit: LedgerOut<'E, 'P> -> unit) =
        match msg with
        | Submit(t, evs, at, st) ->
            waiting.Enqueue(t, evs, at, st)
            decideNext emit
        | Appended(id, p) ->
            match inFlight with
            | Some(t, c, s) when c.id = id ->
                inFlight <- None
                record c s p
                emit (Committed(Some t, c, p))
                decideNext emit
            | _ -> invalidOp (sprintf "Appended %d does not match the commit in flight" id)
        | AppendFailed(id, reason) ->
            match inFlight with
            | Some(t, c, _) when c.id = id ->
                inFlight <- None
                emit (Refused(t, WriteFailed reason))
                decideNext emit
            | _ -> invalidOp (sprintf "AppendFailed %d does not match the commit in flight" id)
        | Replay(c, p) ->
            let refuse why = emit (ReplayRejected(c, p, why))
            if inFlight.IsSome || waiting.Count > 0 then refuse "ledger is busy with live submits"
            elif c.id <> lastId + 1UL then refuse (sprintf "id gap: expected %d, got %d" (lastId + 1UL) c.id)
            elif c.stamp |> Option.exists (fun s -> stamps.ContainsKey s.commandId) then
                refuse (sprintf "repeats commandId %s" c.stamp.Value.commandId)
            else
                match apply state c with
                | Error msg -> refuse ("fold rejected: " + msg)
                | Ok s ->
                    record c s p
                    emit (Committed(None, c, p))

    /// 空账本。
    new(apply, initial: 'S) = Ledger<'S, 'E, 'P>(apply, { state = initial; throughId = 0UL; stamps = Map.empty; positions = [] })

    /// 发出的消息。处理器在 Post 的调用线程上同步执行。
    [<CLIEvent>]
    member _.Outbox = outbox.Publish

    /// 投递一条消息。
    member _.Post(msg: LedgerIn<'E, 'P>) : unit =
        let pending = ResizeArray<LedgerOut<'E, 'P>>()
        let run =
            lock gate (fun () ->
                mailbox.Enqueue msg
                if draining then false
                else
                    draining <- true
                    true)
        // 蹦床：本线程负责排空邮箱；状态变更在锁内，事件在锁外按序发出
        let more () =
            lock gate (fun () ->
                if mailbox.Count = 0 then
                    draining <- false
                    false
                else true)
        while run && more () do
            lock gate (fun () ->
                try handle (mailbox.Dequeue()) pending.Add
                with _ ->
                    // 协议错误（组装方回复了不存在的在途提交）：清空本轮，交还邮箱，再抛出
                    pending.Clear()
                    draining <- false
                    reraise ())
            for o in pending do outbox.Trigger o
            pending.Clear()

    // —— 查询消息（只读，立即回复）——
    member _.State : 'S = lock gate (fun () -> state)
    member _.LastId : uint64 = lock gate (fun () -> lastId)

    /// 可保存的完整状态；以后用 Ledger(apply, checkpoint) 恢复。不含在途与等待中的请求。
    member _.Checkpoint : Checkpoint<'S, 'P> =
        lock gate (fun () -> { state = state; throughId = lastId; stamps = stamps; positions = List.ofSeq positions })

    /// id 的位置。
    member _.PositionOf(id: uint64) : 'P option =
        lock gate (fun () -> if id = 0UL || id > lastId then None else Some positions[int id - 1])

    /// id 大于 cursor 的提交的 (id, 位置)，按 id 升序，至多 limit 条（缺省不限）。
    member _.PositionsAfter(cursor: uint64, ?limit: int) : (uint64 * 'P) list =
        lock gate (fun () ->
            let limit = defaultArg limit Int32.MaxValue
            if limit < 0 then invalidArg (nameof limit) "limit must be >= 0"
            if cursor >= lastId then []
            else
                let n = int (min (uint64 limit) (lastId - cursor))
                [ for i in 0 .. n - 1 -> let id = cursor + 1UL + uint64 i in id, positions[int id - 1] ])
