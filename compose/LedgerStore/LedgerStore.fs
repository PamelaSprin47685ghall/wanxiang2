namespace LedgerStore

open System
open System.IO
open System.Text.Json.Nodes
open JsonSeq
open Ledger
open Snapshot

/// 领域与 JSON 的互转，领域知识，由使用方给出。状态编解码只用于快照。
type Codec<'S, 'E> =
    { encodeEvent: 'E -> JsonNode
      decodeEvent: JsonNode -> Result<'E, string>
      encodeState: 'S -> JsonNode
      decodeState: JsonNode -> Result<'S, string> }

/// 何时保存快照。快照只是缓存，策略只影响启动快慢。
type SnapshotPolicy =
    { /// 距上次快照满这么多条提交就保存；None = 不按条数保存。
      everyCommits: int option
      /// Dispose 时保存。
      onClose: bool }
    static member Manual = { everyCommits = None; onClose = false }
    static member Default = { everyCommits = Some 1000; onClose = true }

/// 启动时快照起了什么作用（诊断用）。
type SnapshotUse =
    | Used of throughId: uint64
    | Absent
    /// 有快照但没用：坏了，或与日志对不上。
    | Discarded of reason: string

type Startup = { snapshot: SnapshotUse; replayedCommits: int }

type Submitted<'E, 'Err> =
    | Committed of Commit<'E>
    /// 同一命令已提交过：原提交（从日志读回）。
    | Duplicate of Commit<'E>
    | Conflict of existingId: uint64
    | Rejected of 'Err
    /// 确知没写入，可以重试。
    | NotWritten of reason: string
    /// 不知道写没写进去。存储随即只读；重新打开会从日志查明。不能当作可以重试。
    | Unknown of reason: string
    /// 存储只读（日志损坏或此前结果未知）。
    | ReadOnly of reason: string

type Health =
    | Healthy
    /// 日志在 offset 处起不可信；Repair 会截掉它及之后的内容（不可撤销）。
    | Damaged of offset: int64 * reason: string
    | OutcomeUnknown of reason: string

/// 组合者 C：拥有本段合作的顺序（何时写、写完记入、何时只读、何时存快照）与在途状态。
/// 领域判断（编号、幂等、折叠）归 Ledger；字节归 JsonSeqFile；快照字节归 SnapshotFile；写者唯一归 DirLock。
/// 目录里有 log.jsonseq 与 snapshot。同步，加锁串行。
/// 'S 与 'E 应是不可变值：State 与返回的提交直接交出，不复制。
type LedgerStore<'S, 'E, 'Err>
    (dir: string, apply: 'S -> Commit<'E> -> Result<'S, 'Err>, initial: 'S, codec: Codec<'S, 'E>, policy: SnapshotPolicy) =
    let dirLock =
        match DirLock.DirLock.TryAcquire dir with
        | Ok l -> l
        | Error e -> invalidOp e
    let file = new JsonSeqFile(Path.Combine(dir, "log.jsonseq"))
    let snapshot = SnapshotFile(Path.Combine(dir, "snapshot"))
    let gate = obj ()
    let mutable health = Healthy
    let mutable snapshotAt = 0UL
    let mutable lastSnapshotError : string option = None

    let decode (c: Commit<JsonNode>) : Result<Commit<'E>, string> =
        let evs = c.events |> List.map codec.decodeEvent
        match evs |> List.tryPick (function Error e -> Some e | Ok _ -> None) with
        | Some e -> Error e
        | None -> Ok { id = c.id; at = c.at; stamp = c.stamp; events = evs |> List.map (function Ok e -> e | Error _ -> failwith "unreachable") }

    let parse (r: Record) : Result<Commit<'E>, string> =
        match r.fault with
        | Some f -> Error f
        | None ->
            match Envelope.read r.payload with
            | Envelope.Parsed c -> decode c
            | Envelope.Invalid why -> Error why
            | Envelope.NewerVersion v -> Error(sprintf "written by a newer version (%d)" v)

    let readOne (span: Span) =
        match file.Read(span.start, span.``end``) with
        | [ r ] -> parse r
        | rs -> Error(sprintf "expected one record in [%d, %d), found %d" span.start span.``end`` rs.Length)

    let fresh () = Ledger<'S, 'E, 'Err>(apply, initial)

    /// 快照只是缓存：与日志对不上就丢弃。
    let fromSnapshot () : Ledger<'S, 'E, 'Err> * SnapshotUse =
        match snapshot.Load() with
        | Missing -> fresh (), Absent
        | Corrupt why -> fresh (), Discarded why
        | Loaded text ->
            let checkedLedger =
                CheckpointJson.read text
                |> Result.bind (fun cp ->
                    match cp.state with
                    | null -> Error "snapshot has no state"
                    | s -> codec.decodeState s |> Result.map (fun st -> Checkpoint.map (fun _ -> st) cp))
                |> Result.bind (fun cp ->
                    try Ok(Ledger<'S, 'E, 'Err>(apply, cp)) with :? ArgumentException as e -> Error e.Message)
                |> Result.bind (fun l ->
                    match l.SpanOf l.LastId with
                    | None -> if l.Tail = 0L then Ok l else Error "snapshot origin is not 0"
                    | Some s when s.``end`` > file.Length -> Error "snapshot is ahead of the log"
                    | Some s ->
                        match readOne s with
                        | Ok c when c.id = l.LastId -> Ok l
                        | Ok c -> Error(sprintf "log has commit %d where snapshot expects %d" c.id l.LastId)
                        | Error e -> Error e)
            match checkedLedger with
            | Ok l -> l, Used l.LastId
            | Error why -> fresh (), Discarded why

    let ledger, startup =
        let l, used = fromSnapshot ()
        let mutable n = 0
        let rec go (records: Record list) =
            match records with
            | [] -> ()
            | r :: rest ->
                let problem =
                    match parse r with
                    | Error why -> Some why
                    | Ok c ->
                        match l.Record(c, { start = r.startOffset; ``end`` = r.endOffset }) with
                        | Ok() -> n <- n + 1; None
                        | Error p -> Some(p.Describe())
                match problem with
                | None -> go rest
                | Some why -> health <- Damaged(r.startOffset, why)
        go (file.ReadFrom l.Tail)
        snapshotAt <- (match used with Used id -> id | _ -> 0UL)
        l, { snapshot = used; replayedCommits = n }

    let save () =
        try
            snapshot.Save(CheckpointJson.write (ledger.Checkpoint |> Checkpoint.map codec.encodeState))
            snapshotAt <- ledger.LastId
            lastSnapshotError <- None
            Ok()
        with e ->
            lastSnapshotError <- Some e.Message
            Error e.Message

    new(dir, apply, initial, codec) = new LedgerStore<'S, 'E, 'Err>(dir, apply, initial, codec, SnapshotPolicy.Default)

    member _.Health = lock gate (fun () -> health)
    member _.Startup : Startup = startup
    member _.State : 'S = lock gate (fun () -> ledger.State)
    member _.LastId = lock gate (fun () -> ledger.LastId)
    member _.Stamps : Map<string, string * uint64> = lock gate (fun () -> ledger.Stamps)
    member _.Checkpoint : Checkpoint<'S> = lock gate (fun () -> ledger.Checkpoint)
    /// 最近一次快照保存的错误（成功后清空）。快照失败不影响提交。
    member _.LastSnapshotError = lock gate (fun () -> lastSnapshotError)

    member _.Submit(events: 'E list, at: DateTimeOffset, ?stamp: Stamp) : Submitted<'E, 'Err> =
        lock gate (fun () ->
            match health with
            | Damaged(o, why) -> ReadOnly(sprintf "log damaged at %d: %s" o why)
            | OutcomeUnknown why -> ReadOnly(sprintf "previous write outcome unknown: %s" why)
            | Healthy ->
            let decision =
                match stamp with
                | Some s -> ledger.Propose(events, at, s)
                | None -> ledger.Propose(events, at)
            match decision with
            | Decision.Duplicate(_, span) ->
                match readOne span with
                | Ok c -> Duplicate c
                | Error e -> ReadOnly(sprintf "duplicate's original commit is unreadable: %s" e)
            | StampConflict id -> Conflict id
            | Decision.Rejected e -> Rejected e
            | Proposed c ->
                let start = ledger.Tail
                match file.Append(Envelope.write (Commit.map codec.encodeEvent c)) with
                | Appended e ->
                    match ledger.Record(c, { start = start; ``end`` = e }) with
                    | Ok() ->
                        match policy.everyCommits with
                        | Some k when ledger.LastId - snapshotAt >= uint64 k -> save () |> ignore
                        | _ -> ()
                        Committed c
                    | Error p ->
                        health <- OutcomeUnknown(p.Describe())
                        Unknown(p.Describe())
                | AppendOutcome.NotWritten why ->
                    ledger.Abandon()
                    NotWritten why
                | AppendOutcome.Unknown why ->
                    health <- OutcomeUnknown why
                    Unknown why)

    /// id 大于 cursor 的提交，按 id 升序，至多 limit 条。只读已记入的提交（损坏点之前）。
    member _.CommitsAfter(cursor: uint64, limit: int) : Result<Commit<'E> list, string> =
        lock gate (fun () ->
            match ledger.SpansAfter(cursor, limit) with
            | [] -> Ok []
            | spans ->
                let first, last = snd spans.Head, snd (List.last spans)
                let records = file.Read(first.start, last.``end``)
                if records.Length <> spans.Length then Error "log changed under the store"
                else
                    let folder acc (r, (id, _)) =
                        acc |> Result.bind (fun xs ->
                            parse r |> Result.bind (fun (c: Commit<'E>) ->
                                if c.id = id then Ok(c :: xs) else Error(sprintf "expected commit %d, found %d" id c.id)))
                    List.zip records spans |> List.fold folder (Ok []) |> Result.map List.rev)

    /// 立即保存快照。
    member _.SaveSnapshot() : Result<unit, string> = lock gate save

    /// 用户明确要求时才调用：截掉损坏点及之后的字节。只在 Damaged 时有效。
    member _.Repair() : Result<int64, string> =
        lock gate (fun () ->
            match health with
            | Damaged(o, _) ->
                file.Truncate o
                health <- Healthy
                Ok o
            | h -> Error(sprintf "nothing to repair: %A" h))

    interface IDisposable with
        member _.Dispose() =
            lock gate (fun () ->
                if policy.onClose && health = Healthy && ledger.LastId <> snapshotAt then save () |> ignore)
            (file :> IDisposable).Dispose()
            (dirLock :> IDisposable).Dispose()
