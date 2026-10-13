namespace LedgerStore

open System
open System.IO
open System.Text.Json.Nodes
open JsonSeq
open Ledger
open Snapshot

/// 提交的结果，对使用者如实区分成功、拒绝、确知未写和未知。
type Submitted =
    | Committed of Commit<JsonNode>
    /// 同一命令已提交过：原提交（从日志读回）。
    | Duplicate of Commit<JsonNode>
    | Conflict of existingId: uint64
    | Rejected of reason: string
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

/// 组合者 C：拥有本段合作的顺序（何时写、写完记入、何时只读）与在途状态（Health）。
/// 领域判断（编号、幂等、折叠）归 Ledger；字节归 JsonSeqFile；快照字节归 SnapshotFile；写者唯一归 DirLock。
/// 一个目录：log.jsonseq、snapshot。同步、加锁串行。
type LedgerStore(dir: string, apply: JsonNode -> Commit<JsonNode> -> Result<JsonNode, string>, initial: JsonNode) =
    let lock' =
        match DirLock.DirLock.TryAcquire dir with
        | Ok l -> l
        | Error e -> invalidOp e
    let file = new JsonSeqFile(Path.Combine(dir, "log.jsonseq"))
    let snapshot = SnapshotFile(Path.Combine(dir, "snapshot"))
    let gate = obj ()
    let mutable health = Healthy

    let readOne (span: Span) =
        match file.Read(span.start, span.``end``) with
        | [ { fault = None; payload = p } ] ->
            match Envelope.read p with
            | Envelope.Parsed c -> Some c
            | _ -> None
        | _ -> None

    /// 快照只是缓存：与日志对不上就丢弃。
    let fromSnapshot () =
        match snapshot.Load() with
        | Loaded text ->
            match CheckpointJson.read text with
            | Ok cp ->
                let ok =
                    try
                        let l = Ledger<JsonNode, JsonNode, string>(apply, cp)
                        match l.SpanOf l.LastId with
                        | None -> cp.origin = 0L
                        | Some s -> s.``end`` <= file.Length && (readOne s |> Option.exists (fun c -> c.id = l.LastId))
                    with :? ArgumentException -> false
                if ok then Some(Ledger<JsonNode, JsonNode, string>(apply, cp)) else None
            | Error _ -> None
        | Missing | Corrupt _ -> None

    let ledger =
        let l = fromSnapshot () |> Option.defaultWith (fun () -> Ledger<JsonNode, JsonNode, string>(apply, initial))
        let rec go (records: Record list) =
            match records with
            | [] -> ()
            | r :: rest ->
                let sp = { start = r.startOffset; ``end`` = r.endOffset }
                let problem =
                    match r.fault with
                    | Some f -> Some f
                    | None ->
                        match Envelope.read r.payload with
                        | Envelope.Parsed c -> (match l.Record(c, sp) with Ok() -> None | Error p -> Some(p.Describe()))
                        | Envelope.Invalid why -> Some why
                        | Envelope.NewerVersion v -> Some(sprintf "written by a newer version (%d)" v)
                match problem with
                | None -> go rest
                | Some why -> health <- Damaged(r.startOffset, why)
        go (file.ReadFrom l.Tail)
        l

    member _.Health = lock gate (fun () -> health)
    member _.State : JsonNode = lock gate (fun () -> ledger.State.DeepClone())
    member _.LastId = lock gate (fun () -> ledger.LastId)

    member _.Submit(events: JsonNode list, at: DateTimeOffset, ?stamp: Stamp) : Submitted =
        lock gate (fun () ->
            match health with
            | Damaged(o, why) -> ReadOnly(sprintf "log damaged at %d: %s" o why)
            | OutcomeUnknown why -> ReadOnly(sprintf "previous write outcome unknown: %s" why)
            | Healthy ->
            let events = events |> List.map _.DeepClone()
            let decision =
                match stamp with
                | Some s -> ledger.Propose(events, at, s)
                | None -> ledger.Propose(events, at)
            match decision with
            | Decision.Duplicate(_, span) ->
                match readOne span with
                | Some c -> Duplicate c
                | None -> ReadOnly "duplicate's original commit is unreadable"
            | StampConflict id -> Conflict id
            | Decision.Rejected e -> Rejected e
            | Proposed c ->
                let start = ledger.Tail
                match file.Append(Envelope.write c) with
                | Appended e ->
                    match ledger.Record(c, { start = start; ``end`` = e }) with
                    | Ok() -> Committed c
                    | Error p ->
                        health <- OutcomeUnknown(p.Describe())
                        Unknown(p.Describe())
                | AppendOutcome.NotWritten why ->
                    ledger.Abandon()
                    NotWritten why
                | AppendOutcome.Unknown why ->
                    health <- OutcomeUnknown why
                    Unknown why)

    /// 保存快照（缓存）。
    member _.SaveSnapshot() =
        lock gate (fun () ->
            snapshot.Save(CheckpointJson.write ledger.Checkpoint))

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
            (file :> IDisposable).Dispose()
            (lock' :> IDisposable).Dispose()
