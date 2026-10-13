namespace Ledger

open System

/// 事件账本对象：拥有领域状态、id 序列、幂等索引和位置索引。
/// 它不碰磁盘、不读时钟、不调用任何外部对象：
/// 调用方拿 Decide 的裁决去持久化，再用 Record 把「已经持久化的事实」交回来。
/// 重放就是把读出的提交逐条 Record。
/// 内部可变状态只在 gate 锁内读写。
type Ledger<'S, 'E, 'P>(apply: 'S -> Commit<'E> -> Result<'S, string>, from: Checkpoint<'S, 'P>) =
    let gate = obj ()
    let mutable state = from.state
    let mutable lastId = from.throughId
    let mutable stamps = from.stamps
    let positions = ResizeArray<'P>(from.positions)
    let committed = Event<Commit<'E> * 'P>()
    do
        if uint64 positions.Count <> from.throughId then
            invalidArg (nameof from) (sprintf "checkpoint has %d positions for %d commits" positions.Count from.throughId)

    /// 空账本。
    new(apply, initial: 'S) = Ledger<'S, 'E, 'P>(apply, { state = initial; throughId = 0UL; stamps = Map.empty; positions = [] })

    member _.State : 'S = lock gate (fun () -> state)
    member _.LastId : uint64 = lock gate (fun () -> lastId)

    /// 可保存的完整状态；以后用 Ledger(apply, checkpoint) 恢复。
    member _.Checkpoint : Checkpoint<'S, 'P> =
        lock gate (fun () -> { state = state; throughId = lastId; stamps = stamps; positions = List.ofSeq positions })

    /// 每次 Record 成功后按 id 顺序触发（锁内同步触发；处理器不应回调本对象）。
    [<CLIEvent>]
    member _.Committed = committed.Publish

    /// 裁决一批事件能否成为下一条提交。不改变账本。
    member _.Decide(events: 'E list, at: DateTimeOffset, ?stamp: Stamp) : Decision<'E, 'P> =
        lock gate (fun () ->
            let seen = stamp |> Option.bind (fun s -> stamps |> Map.tryFind s.commandId |> Option.map (fun v -> s, v))
            match seen with
            | Some(s, (h, id)) when h = s.hash -> Duplicate(id, positions[int id - 1])
            | Some(_, (_, id)) -> StampConflict id
            | None ->
                let c = { id = lastId + 1UL; at = at; stamp = stamp; events = events }
                match apply state c with
                | Ok _ -> Accept c
                | Error msg -> Rejected msg)

    /// 记入一条已持久化的提交及其位置。现场提交与重放走同一条路。
    /// id 必须紧接 LastId，折叠必须成功，commandId 不能重复；否则返回 Error 且账本不变。
    member _.Record(c: Commit<'E>, position: 'P) : Result<unit, string> =
        lock gate (fun () ->
            if c.id <> lastId + 1UL then Error(sprintf "id gap: expected %d, got %d" (lastId + 1UL) c.id)
            elif c.stamp |> Option.exists (fun s -> stamps.ContainsKey s.commandId) then
                Error(sprintf "commit %d repeats commandId %s" c.id c.stamp.Value.commandId)
            else
                match apply state c with
                | Error msg -> Error(sprintf "commit %d rejected: %s" c.id msg)
                | Ok s ->
                    state <- s
                    lastId <- c.id
                    positions.Add position
                    match c.stamp with
                    | Some st -> stamps <- stamps |> Map.add st.commandId (st.hash, c.id)
                    | None -> ()
                    committed.Trigger(c, position)
                    Ok())

    /// id 的位置。
    member _.PositionOf(id: uint64) : 'P option =
        lock gate (fun () -> if id = 0UL || id > lastId then None else Some positions[int id - 1])

    /// id 大于 cursor 的提交的位置，按 id 升序，至多 limit 条（缺省不限）。
    member _.PositionsAfter(cursor: uint64, ?limit: int) : (uint64 * 'P) list =
        lock gate (fun () ->
            let limit = defaultArg limit Int32.MaxValue
            if limit < 0 then invalidArg (nameof limit) "limit must be >= 0"
            if cursor >= lastId then []
            else
                let n = int (min (uint64 limit) (lastId - cursor))
                [ for i in 0 .. n - 1 -> let id = cursor + 1UL + uint64 i in id, positions[int id - 1] ])
