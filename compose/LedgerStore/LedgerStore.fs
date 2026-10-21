namespace LedgerStore

open System
open System.IO
open System.Text.Json.Nodes
open JsonSeq
open Ledger
open Snapshot

/// 领域与 JSON 的互转，领域知识，由使用方给出。状态编解码只用于快照。
type Codec<'S, 'E> =
    {
        encodeEvent: 'E -> JsonNode
        decodeEvent: JsonNode -> Result<'E, string>
        encodeState: 'S -> JsonNode
        decodeState: JsonNode -> Result<'S, string>
    }

/// 何时保存快照。快照只是缓存，策略只影响启动快慢。
type SnapshotPolicy =
    {
        /// 距上次快照满这么多条提交就保存；None = 不按条数保存。
        everyCommits: int option
        /// Dispose 时保存。
        onClose: bool
    }

    static member Manual = { everyCommits = None; onClose = false }

    static member Default =
        {
            everyCommits = Some 1000
            onClose = true
        }

/// 启动时快照起了什么作用（诊断用）。
type SnapshotUse =
    | Used of throughId: uint64
    | Absent
    /// 有快照但没用：坏了，或与日志对不上。
    | Discarded of reason: string

type Startup =
    {
        snapshot: SnapshotUse
        replayedCommits: int
    }

type Submitted<'E, 'Err> =
    | Committed of Commit<'E>
    /// 同一命令已提交过：原提交（从日志读回）。
    | Duplicate of Commit<'E>
    | Conflict of existingId: uint64
    | Rejected of 'Err
    /// 确知没写入，可以重试。
    | NotWritten of reason: string
    /// 不知道提交 commitId 写没写进去。存储随即只读；重新打开会从日志查明它在不在。
    /// 不能当作可以重试：不带幂等戳的命令重发可能执行两次。
    | Unknown of commitId: uint64 * reason: string
    /// 存储只读（日志损坏或此前结果未知）。
    | ReadOnly of reason: string

type Health =
    | Healthy
    /// 日志在 offset 处起不可信；Repair 会截掉它及之后的内容（不可撤销）。
    | Damaged of offset: int64 * reason: string
    /// offset 处的记录由更新版本写入：不是损坏，不能修复（截掉会毁掉新版本的数据）。只读。
    | NewerFormat of offset: int64 * reason: string
    /// 提交 commitId 是否已保存不确定（见 Submitted.Unknown）。重新打开后以日志为准。
    | OutcomeUnknown of commitId: uint64 * reason: string

/// 打开时的读取与重放：存储与 Inspect 共用。只读，不持有任何资源。
module internal Opening =
    let decode (codec: Codec<'S, 'E>) (c: Commit<JsonNode>) : Result<Commit<'E>, string> =
        let evs = c.events |> List.map codec.decodeEvent

        match
            evs
            |> List.tryPick (function
                | Error e -> Some e
                | Ok _ -> None)
        with
        | Some e -> Error e
        | None ->
            Ok
                {
                    id = c.id
                    at = c.at
                    stamp = c.stamp
                    events =
                        evs
                        |> List.map (function
                            | Ok e -> e
                            | Error _ -> failwith "unreachable")
                }

    let parse codec (r: Record) : Result<Commit<'E>, string> =
        match r.fault with
        | Some f -> Error f
        | None ->
            match Envelope.read r.payload with
            | Envelope.Parsed c -> decode codec c
            | Envelope.Invalid why -> Error why
            | Envelope.NewerVersion v -> Error(sprintf "written by a newer version (%d)" v)

    let readOne codec (read: int64 * int64 -> Record list) (span: Span) : Result<Commit<'E>, string> =
        match read (span.start, span.``end``) with
        | [ r ] -> parse codec r
        | rs -> Error(sprintf "expected one record in [%d, %d), found %d" span.start span.``end`` rs.Length)

    /// 快照（只当缓存，对不上就丢）+ 从快照位置重放到 length。
    /// 返回账本、启动信息、健康状况、最后一条是否半写尾（可能是正在进行的写入）。
    let load
        (apply: 'S -> Commit<'E> -> Result<'S, 'Err>)
        (initial: 'S)
        (codec: Codec<'S, 'E>)
        (snap: Loaded)
        (length: int64)
        (read: int64 * int64 -> Record list)
        =
        let fresh () = Ledger<'S, 'E, 'Err>(apply, initial)

        let l, used =
            match snap with
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
                        try
                            Ok(Ledger<'S, 'E, 'Err>(apply, cp))
                        with :? ArgumentException as e ->
                            Error e.Message)
                    |> Result.bind (fun l ->
                        match l.SpanOf l.LastId with
                        | None ->
                            if l.Tail = 0L then
                                Ok l
                            else
                                Error "snapshot origin is not 0"
                        | Some s when s.``end`` > length -> Error "snapshot is ahead of the log"
                        | Some s ->
                            match readOne codec read s with
                            | Ok c when c.id = l.LastId -> Ok l
                            | Ok c -> Error(sprintf "log has commit %d where snapshot expects %d" c.id l.LastId)
                            | Error e -> Error e)

                match checkedLedger with
                | Ok l -> l, Used l.LastId
                | Error why -> fresh (), Discarded why

        let mutable n = 0
        let mutable health = Healthy
        let mutable tornTail = false

        let rec go (records: Record list) =
            match records with
            | [] -> ()
            | r :: rest ->
                let problem =
                    match parse codec r with
                    | Error why -> Some why
                    | Ok c ->
                        match
                            l.Record(
                                c,
                                {
                                    start = r.startOffset
                                    ``end`` = r.endOffset
                                }
                            )
                        with
                        | Ok() ->
                            n <- n + 1
                            None
                        | Error p -> Some(p.Describe())

                match problem with
                | None -> go rest
                | Some why ->
                    let newer =
                        r.fault.IsNone
                        && (match Envelope.read r.payload with
                            | Envelope.NewerVersion _ -> true
                            | _ -> false)

                    health <-
                        if newer then
                            NewerFormat(r.startOffset, why)
                        else
                            Damaged(r.startOffset, why)

                    tornTail <- rest.IsEmpty && r.fault = Some Frame.TornTail

        go (if l.Tail >= length then [] else read (l.Tail, length))
        l, { snapshot = used; replayedCommits = n }, health, tornTail

/// Inspect 的结果：不持锁、只读地看一眼数据目录。
type Inspection =
    {
        health: Health
        startup: Startup
        lastId: uint64
        /// 唯一的问题是日志末尾一条半写的记录。宿主正在写时也会这样，以宿主自己的 Health 为准。
        tailMayBeInFlight: bool
    }

/// 组合者 C：拥有本段合作的顺序（何时写、写完记入、何时只读、何时存快照）与在途状态。
/// 领域判断（编号、幂等、折叠）归 Ledger；字节归 JsonSeqFile；快照字节归 SnapshotFile；写者唯一归 DirLock。
/// 目录里有 log.jsonseq 与 snapshot。同步，加锁串行。
/// 'S 与 'E 应是不可变值：State 与返回的提交直接交出，不复制。
type LedgerStore<'S, 'E, 'Err>
    (dir: string, apply: 'S -> Commit<'E> -> Result<'S, 'Err>, initial: 'S, codec: Codec<'S, 'E>, policy: SnapshotPolicy)
    =
    let dirLock =
        match DirLock.DirLock.TryAcquire dir with
        | Ok l -> l
        | Error e -> invalidOp e

    let file = new JsonSeqFile(Path.Combine(dir, "log.jsonseq"))
    let snapshot = SnapshotFile(Path.Combine(dir, "snapshot"))
    let gate = obj ()
    let mutable health = Healthy
    let mutable snapshotAt = 0UL
    let mutable lastSnapshotError: string option = None

    let readOne (span: Span) = Opening.readOne codec file.Read span

    let ledger, startup =
        let l, st, h, _ =
            Opening.load apply initial codec (snapshot.Load()) file.Length file.Read

        health <- h

        snapshotAt <-
            (match st.snapshot with
             | Used id -> id
             | _ -> 0UL)

        l, st

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

    /// 不持锁、不写任何字节地检查 dir。宿主持锁、正在写时也可以调用。
    static member Inspect
        (dir: string, codec: Codec<'S, 'E>, apply: 'S -> Commit<'E> -> Result<'S, 'Err>, initial: 'S)
        : Inspection =
        let path = Path.Combine(dir, "log.jsonseq")
        // 先读快照再读日志：日志只增长，于是快照不会比读到的日志新
        let snap = SnapshotFile(Path.Combine(dir, "snapshot")).Load()

        let bytes =
            if not (File.Exists path) then
                [||]
            else
                use fs =
                    new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ||| FileShare.Delete)

                use ms = new MemoryStream()
                fs.CopyTo ms
                ms.ToArray()

        let read (a: int64, b: int64) =
            Frame.split a (ReadOnlySpan(bytes, int a, int (b - a)))

        let l, startup, health, torn =
            Opening.load apply initial codec snap bytes.LongLength read

        {
            health = health
            startup = startup
            lastId = l.LastId
            tailMayBeInFlight = torn
        }

    member _.Health = lock gate (fun () -> health)
    member _.Startup: Startup = startup
    member _.State: 'S = lock gate (fun () -> ledger.State)
    member _.LastId = lock gate (fun () -> ledger.LastId)
    member _.Stamps: Map<string, string * uint64> = lock gate (fun () -> ledger.Stamps)
    member _.Checkpoint: Checkpoint<'S> = lock gate (fun () -> ledger.Checkpoint)
    /// 最近一次快照保存的错误（成功后清空）。快照失败不影响提交。
    member _.LastSnapshotError = lock gate (fun () -> lastSnapshotError)

    member _.Submit(events: 'E list, at: DateTimeOffset, ?stamp: Stamp) : Submitted<'E, 'Err> =
        lock gate (fun () ->
            match health with
            | Damaged(o, why)
            | NewerFormat(o, why) -> ReadOnly(sprintf "log damaged at %d: %s" o why)
            | OutcomeUnknown(id, why) ->
                ReadOnly(
                    sprintf
                        "commit %d may or may not have been saved (%s); reopen the store to find out from the log"
                        id
                        why
                )
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
                            health <- OutcomeUnknown(c.id, p.Describe())
                            Unknown(c.id, p.Describe())
                    | AppendOutcome.NotWritten why ->
                        ledger.Abandon()
                        NotWritten why
                    | AppendOutcome.Unknown why ->
                        health <- OutcomeUnknown(c.id, why)
                        Unknown(c.id, why))

    /// id 大于 cursor 的提交，按 id 升序，至多 limit 条。只读已记入的提交（损坏点之前）。
    member _.CommitsAfter(cursor: uint64, limit: int) : Result<Commit<'E> list, string> =
        lock gate (fun () ->
            match ledger.SpansAfter(cursor, limit) with
            | [] -> Ok []
            | spans ->
                let first, last = snd spans.Head, snd (List.last spans)
                let records = file.Read(first.start, last.``end``)

                if records.Length <> spans.Length then
                    Error "log changed under the store"
                else
                    let folder acc (r, (id, _)) =
                        acc
                        |> Result.bind (fun xs ->
                            Opening.parse codec r
                            |> Result.bind (fun (c: Commit<'E>) ->
                                if c.id = id then
                                    Ok(c :: xs)
                                else
                                    Error(sprintf "expected commit %d, found %d" id c.id)))

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
                if policy.onClose && health = Healthy && ledger.LastId <> snapshotAt then
                    save () |> ignore)

            (file :> IDisposable).Dispose()
            (dirLock :> IDisposable).Dispose()
