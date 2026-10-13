namespace Ledger

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

/// 事件账本 actor：私有状态为领域状态、id 序列、幂等索引、区间索引、在途提交与等待队列。
/// 只通过 Post / Ask 接收 LedgerIn，只通过 Outbox 发出 LedgerOut；不碰磁盘、不读时钟、不调用任何外部对象。
/// 消息逐条处理（邮箱 + 蹦床）：在 Outbox 处理器里再 Post 只是入队，不会重入。
type Ledger<'S, 'E, 'Err>(apply: 'S -> Commit<'E> -> Result<'S, 'Err>, from: Checkpoint<'S>) =
    let gate = obj ()
    let mailbox = Queue<LedgerIn<'E>>()
    /// 正在排空邮箱的线程 id；0 = 无
    let mutable drainer = 0
    let outbox = Event<LedgerOut<'E, 'Err>>()
    /// Ask 的等待者：ticket -> 回复
    let askers = Dictionary<Guid, TaskCompletionSource<LedgerOut<'E, 'Err>>>()

    let mutable state = from.state
    let mutable lastId = from.throughId
    let mutable stamps = from.stamps
    let ends = ResizeArray<int64>(from.ends)
    let mutable inFlight : (Guid * Commit<'E> * 'S) option = None
    let waiting = Queue<Guid * 'E list * DateTimeOffset * Stamp option>()

    do
        if uint64 ends.Count <> from.throughId then
            invalidArg (nameof from) (sprintf "checkpoint has %d ends for %d commits" ends.Count from.throughId)
        for i in 0 .. ends.Count - 1 do
            let prev = if i = 0 then from.origin else ends[i - 1]
            if ends[i] <= prev then invalidArg (nameof from) "checkpoint ends must be strictly increasing from origin"

    let tail () = if ends.Count = 0 then from.origin else ends[ends.Count - 1]
    let spanOf (id: uint64) =
        let i = int id - 1
        { start = (if i = 0 then from.origin else ends[i - 1]); ``end`` = ends[i] }

    let record (c: Commit<'E>) (s: 'S) (endOffset: int64) =
        state <- s
        lastId <- c.id
        ends.Add endOffset
        match c.stamp with
        | Some st -> stamps <- stamps |> Map.add st.commandId (st.hash, c.id)
        | None -> ()

    let rec decideNext (emit: LedgerOut<'E, 'Err> -> unit) =
        if inFlight.IsNone && waiting.Count > 0 then
            let ticket, events, at, stamp = waiting.Dequeue()
            let seen = stamp |> Option.bind (fun s -> stamps |> Map.tryFind s.commandId |> Option.map (fun v -> s, v))
            match seen with
            | Some(s, (h, id)) when h = s.hash -> emit (Refused(ticket, Duplicate(id, spanOf id))); decideNext emit
            | Some(_, (_, id)) -> emit (Refused(ticket, StampConflict id)); decideNext emit
            | None ->
                let c = { id = lastId + 1UL; at = at; stamp = stamp; events = events }
                match apply state c with
                | Error e -> emit (Refused(ticket, Rejected e)); decideNext emit
                | Ok s ->
                    inFlight <- Some(ticket, c, s)
                    emit (AppendRequested(ticket, c, tail ()))

    let handle (msg: LedgerIn<'E>) (emit: LedgerOut<'E, 'Err> -> unit) =
        match msg with
        | Submit(t, evs, at, st) ->
            waiting.Enqueue(t, evs, at, st)
            decideNext emit
        | Appended(id, endOffset) ->
            match inFlight with
            | Some(t, c, s) when c.id = id ->
                let start = tail ()
                if endOffset <= start then invalidOp (sprintf "Appended %d ends at %d, not after %d" id endOffset start)
                inFlight <- None
                record c s endOffset
                emit (Committed(Some t, c, { start = start; ``end`` = endOffset }))
                decideNext emit
            | _ -> invalidOp (sprintf "Appended %d does not match the commit in flight" id)
        | AppendFailed(id, reason) ->
            match inFlight with
            | Some(t, c, _) when c.id = id ->
                inFlight <- None
                emit (Refused(t, WriteFailed reason))
                decideNext emit
            | _ -> invalidOp (sprintf "AppendFailed %d does not match the commit in flight" id)
        | Replay(c, span) ->
            let refuse p = emit (ReplayRejected(c, span, p))
            if inFlight.IsSome || waiting.Count > 0 then refuse Busy
            elif c.id <> lastId + 1UL then refuse (IdGap(lastId + 1UL, c.id))
            elif span.start <> tail () || span.``end`` <= span.start then refuse (SpanGap(tail (), span))
            elif c.stamp |> Option.exists (fun s -> stamps.ContainsKey s.commandId) then refuse (RepeatedStamp c.stamp.Value.commandId)
            else
                match apply state c with
                | Error e -> refuse (FoldRejected e)
                | Ok s ->
                    record c s span.``end``
                    emit (Committed(None, c, span))

    let deliver (o: LedgerOut<'E, 'Err>) =
        outbox.Trigger o
        let ticket =
            match o with
            | Committed(Some t, _, _) | Refused(t, _) -> Some t
            | _ -> None
        match ticket with
        | Some t ->
            let waiter =
                lock gate (fun () ->
                    match askers.TryGetValue t with
                    | true, w -> askers.Remove t |> ignore; Some w
                    | _ -> None)
            waiter |> Option.iter (fun (w: TaskCompletionSource<LedgerOut<'E, 'Err>>) -> w.SetResult o)
        | None -> ()

    /// 空账本，介质从偏移 origin 开始（通常为 0）。
    new(apply, initial: 'S, ?origin: int64) =
        Ledger<'S, 'E, 'Err>(apply, { state = initial; throughId = 0UL; stamps = Map.empty; origin = defaultArg origin 0L; ends = [||] })

    /// 发出的消息。处理器在排空邮箱的线程上同步执行。
    [<CLIEvent>]
    member _.Outbox = outbox.Publish

    /// 投递一条消息，不等回复。
    member _.Post(msg: LedgerIn<'E>) : unit =
        let me = Environment.CurrentManagedThreadId
        let run =
            lock gate (fun () ->
                mailbox.Enqueue msg
                if drainer <> 0 then false
                else
                    drainer <- me
                    true)
        let pending = ResizeArray<LedgerOut<'E, 'Err>>()
        let more () =
            lock gate (fun () ->
                if mailbox.Count = 0 then
                    drainer <- 0
                    false
                else true)
        while run && more () do
            lock gate (fun () ->
                try handle (mailbox.Dequeue()) pending.Add
                with _ ->
                    // 协议错误：丢弃本条，交还邮箱，再抛出
                    pending.Clear()
                    drainer <- 0
                    reraise ())
            for o in pending do deliver o
            pending.Clear()

    /// 投递 Submit 并等待它的回复（Committed 或 Refused）。
    /// 在 Outbox 处理器里调用会永远等不到（回复要等本线程处理完当前消息），所以直接抛出。
    member this.Ask(events: 'E list, at: DateTimeOffset, ?stamp: Stamp) : LedgerOut<'E, 'Err> =
        if lock gate (fun () -> drainer = Environment.CurrentManagedThreadId) then
            invalidOp "Ask called from inside an Outbox handler: the reply can never arrive; use Post instead"
        let t = Guid.NewGuid()
        let w = TaskCompletionSource<LedgerOut<'E, 'Err>>(TaskCreationOptions.RunContinuationsAsynchronously)
        lock gate (fun () -> askers[t] <- w)
        this.Post(Submit(t, events, at, stamp))
        w.Task.Result

    // —— 查询（只读，立即回答）——
    member _.State : 'S = lock gate (fun () -> state)
    member _.LastId : uint64 = lock gate (fun () -> lastId)

    /// 可保存的完整状态；以后用 Ledger(apply, checkpoint) 恢复。不含在途与等待中的请求。
    member _.Checkpoint : Checkpoint<'S> =
        lock gate (fun () -> { state = state; throughId = lastId; stamps = stamps; origin = from.origin; ends = ends.ToArray() })

    /// id 的区间。
    member _.SpanOf(id: uint64) : Span option =
        lock gate (fun () -> if id = 0UL || id > lastId then None else Some(spanOf id))

    /// id 大于 cursor 的提交的 (id, 区间)，按 id 升序，至多 limit 条（缺省不限）。
    member _.SpansAfter(cursor: uint64, ?limit: int) : (uint64 * Span) list =
        lock gate (fun () ->
            let limit = defaultArg limit Int32.MaxValue
            if limit < 0 then invalidArg (nameof limit) "limit must be >= 0"
            if cursor >= lastId then []
            else
                let n = int (min (uint64 limit) (lastId - cursor))
                [ for i in 0 .. n - 1 -> let id = cursor + 1UL + uint64 i in id, spanOf id ])
