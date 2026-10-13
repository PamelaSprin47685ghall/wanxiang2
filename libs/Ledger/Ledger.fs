namespace Ledger

open System

/// 事件账本。私有状态：领域状态、id 序列、幂等索引、区间索引、至多一条未落定的提议。
/// 普通同步对象：不碰磁盘、不读时钟、不知道谁来持久化。不是线程安全的；并发由持有它的组合者串行化。
///
/// 协议（组合者 C 安排顺序）：
///   Propose → Proposed c → C 持久化 c → Record(c, span)  —— 成立
///                                     → Abandon()        —— C 确知没写入
///   重放：对介质里的每条提交 Record(c, span)。
/// 结果未知（不知道写没写进去）时 C 既不 Record 也不 Abandon：丢弃本实例，从介质重建。
type Ledger<'S, 'E, 'Err>(apply: 'S -> Commit<'E> -> Result<'S, 'Err>, from: Checkpoint<'S>) =
    let mutable state = from.state
    let mutable lastId = from.throughId
    let mutable stamps = from.stamps
    let ends = ResizeArray<int64>(from.ends)
    let mutable pending : (Commit<'E> * 'S) option = None

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

    /// 空账本，介质从偏移 origin 开始（通常为 0）。
    new(apply, initial: 'S, ?origin: int64) =
        Ledger<'S, 'E, 'Err>(apply, { state = initial; throughId = 0UL; stamps = Map.empty; origin = defaultArg origin 0L; ends = [||] })

    /// 裁决下一条提交。Proposed 之后，在 Record 或 Abandon 之前再次 Propose 是组合者的程序错误，抛出。
    member _.Propose(events: 'E list, at: DateTimeOffset, ?stamp: Stamp) : Decision<'E, 'Err> =
        match pending with
        | Some(c, _) -> invalidOp (sprintf "proposal %d is not settled; record or abandon it first" c.id)
        | None ->
        match stamp |> Option.bind (fun s -> stamps |> Map.tryFind s.commandId |> Option.map (fun v -> s, v)) with
        | Some(s, (h, id)) when h = s.hash -> Duplicate(id, spanOf id)
        | Some(_, (_, id)) -> StampConflict id
        | None ->
            let c = { id = lastId + 1UL; at = at; stamp = stamp; events = events }
            match apply state c with
            | Error e -> Rejected e
            | Ok s ->
                pending <- Some(c, s)
                Proposed c

    /// 组合者确知提议没有写入介质：丢弃它。没有提议时什么也不做。
    member _.Abandon() : unit = pending <- None

    /// 事实：提交 c 已持久化在 span。可以是刚才的提议，也可以是重放读出的一条。
    /// 不成立时返回问题，账本不变。
    member _.Record(c: Commit<'E>, span: Span) : Result<unit, RecordProblem<'Err>> =
        let check () =
            if c.id <> lastId + 1UL then Error(IdGap(lastId + 1UL, c.id))
            elif span.start <> tail () || span.``end`` <= span.start then Error(SpanGap(tail (), span))
            else Ok()
        let folded =
            match pending with
            | Some(p, s) when p.id = c.id -> check () |> Result.map (fun () -> s)
            | Some(p, _) -> Error(Unsettled p.id)
            | None ->
                check ()
                |> Result.bind (fun () ->
                    match c.stamp with
                    | Some st when stamps.ContainsKey st.commandId -> Error(RepeatedStamp st.commandId)
                    | _ -> apply state c |> Result.mapError FoldRejected)
        folded
        |> Result.map (fun s ->
            pending <- None
            state <- s
            lastId <- c.id
            ends.Add span.``end``
            match c.stamp with
            | Some st -> stamps <- stamps |> Map.add st.commandId (st.hash, c.id)
            | None -> ())

    // —— 查询 ——
    member _.State : 'S = state
    member _.LastId : uint64 = lastId
    /// 下一条提交的起点（上一条的末尾）。
    member _.Tail : int64 = tail ()

    /// 可保存的完整状态（数组是副本）；以后用 Ledger(apply, checkpoint) 恢复。不含未落定的提议。
    member _.Checkpoint : Checkpoint<'S> =
        { state = state; throughId = lastId; stamps = stamps; origin = from.origin; ends = ends.ToArray() }

    member _.SpanOf(id: uint64) : Span option =
        if id = 0UL || id > lastId then None else Some(spanOf id)

    /// id 大于 cursor 的提交的 (id, 区间)，按 id 升序，至多 limit 条（缺省不限）。
    member _.SpansAfter(cursor: uint64, ?limit: int) : (uint64 * Span) list =
        let limit = defaultArg limit Int32.MaxValue
        if limit < 0 then invalidArg (nameof limit) "limit must be >= 0"
        if cursor >= lastId then []
        else
            let n = int (min (uint64 limit) (lastId - cursor))
            [ for i in 0 .. n - 1 -> let id = cursor + 1UL + uint64 i in id, spanOf id ]
