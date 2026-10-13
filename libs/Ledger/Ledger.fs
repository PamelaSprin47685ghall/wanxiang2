namespace Ledger

open System

/// 打开结果。
type Opened<'S, 'E> =
    | Ready of Ledger<'S, 'E>
    | Damaged of Damage<'S>
    | Unsupported of version: int

/// 单写者事件账本：状态 'S、事件 'E 全由调用方定义。
/// 内部可变状态（当前基线、偏移索引）只在 gate 锁内读写。
and Ledger<'S, 'E> internal (log: Log, codec: Codec<'E>, apply: 'S -> Commit<'E> -> Result<'S, string>, now: unit -> DateTimeOffset, baseId: uint64, initial: Checkpoint<'S>, starts: int64 list) =
    let gate = obj ()
    let mutable cp = initial
    /// 基线 baseId 之后每条提交的起始偏移；索引 i 对应 id = baseId + 1 + i
    let starts = ResizeArray<int64>(starts)
    let committed = Event<Commit<'E>>()

    let decodeAll (records: LogRecord list) =
        records
        |> List.choose (fun r ->
            match Envelope.decode codec r.payload with
            | Envelope.Ok c -> Some c
            | _ -> None)

    /// 当前状态。
    member _.State : 'S = lock gate (fun () -> cp.state)

    /// 当前基线（可交给快照保存，下次 Open 时作为 from）。
    member _.Checkpoint : Checkpoint<'S> = lock gate (fun () -> cp)

    /// 最后一条提交的 id（空账本为 0）。
    member _.LastId : uint64 = lock gate (fun () -> cp.throughId)

    /// 每次提交成功后按提交顺序触发（在锁内同步触发；处理器不应回调 Submit）。
    [<CLIEvent>]
    member _.Committed = committed.Publish

    /// 提交一批事件。先折叠后写盘：折叠被拒则什么都不写。
    member _.Submit(events: 'E list, ?stamp: Stamp) : SubmitResult<'E> =
        lock gate (fun () ->
            let existing = stamp |> Option.bind (fun s -> cp.stamps |> Map.tryFind s.commandId |> Option.map (fun v -> s, v))
            match existing with
            | Some(s, (h, id)) when h = s.hash -> Duplicate id
            | Some(_, (_, id)) -> StampConflict id
            | None ->
                let c = { id = cp.throughId + 1UL; at = now (); stamp = stamp; events = events }
                match apply cp.state c with
                | Error msg -> Rejected msg
                | Ok s ->
                    let before = cp.offset
                    try
                        let after = log.append (Envelope.encode codec c)
                        starts.Add before
                        cp <- { state = s
                                throughId = c.id
                                offset = after
                                stamps =
                                    match stamp with
                                    | Some st -> cp.stamps |> Map.add st.commandId (st.hash, c.id)
                                    | None -> cp.stamps }
                        committed.Trigger c
                        Committed c
                    with e ->
                        (try log.truncate before with _ -> ())
                        WriteFailed e)

    /// 读出 id 大于 cursor 的提交（按 id 升序）。在锁内读，避免与追加交错。
    member _.ReadAfter(cursor: uint64) : Commit<'E> list =
        lock gate (fun () ->
            if cursor >= cp.throughId then []
            else
                let first = baseId + 1UL
                // 基线之前的提交没有偏移索引：慢路径，从头读
                let off = if cursor + 1UL >= first then starts[int (cursor + 1UL - first)] else 0L
                log.readFrom off |> decodeAll |> List.filter (fun c -> c.id > cursor))

/// 打开与修复。
[<RequireQualifiedAccess>]
module Ledger =
    /// 打开账本：从 from 续播。不改动日志；损坏时返回 Damaged，由调用方决定是否 repair。
    let load (log: Log) (codec: Codec<'E>) (apply: 'S -> Commit<'E> -> Result<'S, string>) (from: Checkpoint<'S>) (now: unit -> DateTimeOffset) : Opened<'S, 'E> =
        match Replay.run log codec apply from with
        | Replayed.Clean(cp, starts) -> Ready(new Ledger<'S, 'E>(log, codec, apply, now, from.throughId, cp, starts))
        | Replayed.Broken d -> Damaged d
        | Replayed.TooNew v -> Unsupported v

    /// 用系统时钟打开。
    let openLog log codec apply from = load log codec apply from (fun () -> DateTimeOffset.UtcNow)

    /// 修复：丢弃损坏点及其之后的所有记录（不可撤销，调用方应先备份或征得用户确认）。
    let repair (log: Log) (damage: Damage<'S>) : unit = log.truncate damage.validUntil
