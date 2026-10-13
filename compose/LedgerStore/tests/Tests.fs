/// 真实组合：真实的 JsonSeqFile、Ledger、SnapshotFile、DirLock，由 LedgerStore 组合，在真实文件上运行。
module LedgerStoreTests

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open Ledger
open LedgerStore

/// 计数器领域：事件是整数，状态是整数，结果不能为负。错误类型是领域自己的。
type CounterError = Negative of int

let apply (s: int) (c: Commit<int>) : Result<int, CounterError> =
    let v = s + List.sum c.events
    if v < 0 then Error(Negative v) else Ok v

let codec : Codec<int, int> =
    { encodeEvent = fun n -> JsonValue.Create n :> JsonNode
      decodeEvent = fun j -> try Ok(j.GetValue<int>()) with e -> Error e.Message
      encodeState = fun n -> JsonValue.Create n :> JsonNode
      decodeState = fun j -> try Ok(j.GetValue<int>()) with e -> Error e.Message }

type S = LedgerStore<int, int, CounterError>

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
let tempDir () = Path.Combine(Path.GetTempPath(), "ledgerstore-" + Guid.NewGuid().ToString("N"))
let openWith policy dir = new S(dir, apply, 0, codec, policy)
let openAt dir = openWith SnapshotPolicy.Manual dir
let logOf dir = Path.Combine(dir, "log.jsonseq")
let committed = function Committed c -> c | r -> failwithf "expected Committed, got %A" r

[<Fact>]
let ``commits survive reopen with and without snapshot, startup says which`` () =
    let d = tempDir ()
    do
        use s = openAt d
        Assert.Equal({ snapshot = Absent; replayedCommits = 0 }, s.Startup)
        for n in 1..3 do committed (s.Submit([ n ], t0)) |> ignore
        Assert.Equal(Ok(), s.SaveSnapshot())
        committed (s.Submit([ 10 ], t0)) |> ignore
    do
        use s = openAt d
        Assert.Equal({ snapshot = Used 3UL; replayedCommits = 1 }, s.Startup)
        Assert.Equal((4UL, 16), (s.LastId, s.State))
    File.WriteAllText(Path.Combine(d, "snapshot"), "garbage")
    do
        use s = openAt d
        match s.Startup with { snapshot = Discarded _; replayedCommits = 4 } -> () | x -> failwithf "%A" x
        Assert.Equal((4UL, 16), (s.LastId, s.State))
    File.Delete(Path.Combine(d, "snapshot"))
    use s = openAt d
    Assert.Equal({ snapshot = Absent; replayedCommits = 4 }, s.Startup)
    Assert.Equal(16, s.State)

[<Fact>]
let ``domain rejection carries the typed error and writes nothing`` () =
    let d = tempDir ()
    use s = openAt d
    match s.Submit([ -1 ], t0) with Rejected(Negative -1) -> () | r -> failwithf "%A" r
    Assert.Equal(0L, FileInfo(logOf d).Length)

[<Fact>]
let ``duplicate returns the original commit read back from the log; stamps are visible`` () =
    let d = tempDir ()
    let st = { commandId = "cmd"; hash = "h" }
    let c =
        use s = openAt d
        let c = committed (s.Submit([ 5 ], t0, st))
        match s.Submit([ 5 ], t0, st) with Duplicate o -> Assert.Equal(c, o) | r -> failwithf "%A" r
        match s.Submit([ 5 ], t0, { st with hash = "x" }) with Conflict 1UL -> () | r -> failwithf "%A" r
        c
    use s = openAt d
    match s.Submit([ 5 ], t0, st) with Duplicate o -> Assert.Equal(c, o) | r -> failwithf "%A" r
    Assert.Equal<Map<string, string * uint64>>(Map [ "cmd", ("h", 1UL) ], s.Stamps)
    Assert.Equal(1UL, s.Checkpoint.throughId)
    Assert.Equal(5, s.State)

[<Fact>]
let ``commits after cursor page through the log`` () =
    use s = openAt (tempDir ())
    let all = [ for n in 1..150 -> committed (s.Submit([ n ], t0)) ]
    let rec pages cursor acc =
        match s.CommitsAfter(cursor, 64) with
        | Ok [] -> List.rev acc
        | Ok page -> Assert.True(page.Length <= 64); pages (List.last page).id (page :: acc)
        | Error e -> failwith e
    let got = pages 0UL []
    Assert.Equal<int list>([ 64; 64; 22 ], got |> List.map List.length)
    Assert.Equal<Commit<int> list>(all, List.concat got)
    Assert.Equal(Ok [], s.CommitsAfter(150UL, 64))

/// 崩溃矩阵的新形态：崩溃只会在追加的某个字节处中断。对新记录的每一个截断长度，
/// 重开后：历史字节不变；id 无重复无空洞；截断为 0 则健康且停在旧位置，完整则新提交在，
/// 其余为 Damaged（证据：偏移与原因），修复后从旧位置继续。
[<Fact>]
let ``crash at every byte of an append leaves the longest valid prefix`` () =
    let src = tempDir ()
    do
        use s = openAt src
        for n in 1..3 do committed (s.Submit([ n ], t0)) |> ignore
    let history = File.ReadAllBytes(logOf src)
    let full =
        do
            use s = openAt src
            committed (s.Submit([ 4 ], t0)) |> ignore
        File.ReadAllBytes(logOf src)
    let record = full[history.Length ..]
    for cut in 0 .. record.Length do
        let d = tempDir ()
        Directory.CreateDirectory d |> ignore
        File.WriteAllBytes(logOf d, Array.append history record[.. cut - 1])
        use s = openAt d
        let bytes = File.ReadAllBytes(logOf d)
        Assert.Equal<byte[]>(history, bytes[.. history.Length - 1])
        if cut = 0 then
            Assert.Equal((Healthy, 3UL), (s.Health, s.LastId))
        elif cut = record.Length then
            Assert.Equal((Healthy, 4UL, 10), (s.Health, s.LastId, s.State))
        else
            match s.Health with
            | Damaged(o, why) -> Assert.Equal(int64 history.Length, o); Assert.False(String.IsNullOrWhiteSpace why)
            | h -> failwithf "cut %d: %A" cut h
            Assert.Equal(3UL, s.LastId)
            Assert.Equal(Ok(int64 history.Length), s.Repair())
            Assert.Equal(4UL, (committed (s.Submit([ 4 ], t0))).id)
        match s.CommitsAfter(0UL, 100) with
        | Ok cs -> Assert.Equal<uint64 list>([ 1UL .. s.LastId ], cs |> List.map _.id)
        | Error e -> failwith e

[<Fact>]
let ``torn tail makes the store read-only and opening changes no bytes`` () =
    let d = tempDir ()
    do
        use s = openAt d
        for n in 1..2 do committed (s.Submit([ n ], t0)) |> ignore
    File.AppendAllText(logOf d, "\u001e{\"v\":1,\"id\":\"3\"")
    let before = File.ReadAllBytes(logOf d)
    use s = openAt d
    match s.Submit([ 1 ], t0) with ReadOnly _ -> () | r -> failwithf "%A" r
    Assert.Equal<byte[]>(before, File.ReadAllBytes(logOf d))

[<Fact>]
let ``undecodable event is damage, not a crash`` () =
    let d = tempDir ()
    do
        use s = openAt d
        committed (s.Submit([ 1 ], t0)) |> ignore
    let good = FileInfo(logOf d).Length
    File.AppendAllText(logOf d, "\u001e{\"v\":1,\"id\":\"2\",\"at\":\"2026-01-01T00:00:00+00:00\",\"events\":[\"x\"]}\n")
    use s = openAt d
    match s.Health with Damaged(o, _) -> Assert.Equal(good, o) | h -> failwithf "%A" h

[<Fact>]
let ``stale snapshot from a truncated log is discarded`` () =
    let d = tempDir ()
    do
        use s = openAt d
        for n in 1..3 do committed (s.Submit([ n ], t0)) |> ignore
        s.SaveSnapshot() |> ignore
    File.WriteAllBytes(logOf d, [||])
    use s = openAt d
    match s.Startup.snapshot with Discarded _ -> () | x -> failwithf "%A" x
    Assert.Equal((0UL, 0), (s.LastId, s.State))

[<Fact>]
let ``snapshot policy saves every k commits and on close`` () =
    let d = tempDir ()
    do
        use s = openWith { everyCommits = Some 2; onClose = false } d
        for n in 1..5 do committed (s.Submit([ n ], t0)) |> ignore
    do
        use s = openAt d
        Assert.Equal({ snapshot = Used 4UL; replayedCommits = 1 }, s.Startup)
    do
        use s = openWith { everyCommits = None; onClose = true } d
        committed (s.Submit([ 1 ], t0)) |> ignore
    use s = openAt d
    Assert.Equal({ snapshot = Used 6UL; replayedCommits = 0 }, s.Startup)
    Assert.Equal(16, s.State)

[<Fact>]
let ``unknown write outcome stays unknown and is never turned into a retry`` () =
    // 真实故障：日志指向 /dev/full，写入 ENOSPC 且无法截回。仅 Linux。
    if OperatingSystem.IsLinux() then
        let d = tempDir ()
        Directory.CreateDirectory d |> ignore
        File.CreateSymbolicLink(logOf d, "/dev/full") |> ignore
        use s = openWith SnapshotPolicy.Default d
        match s.Submit([ 1 ], t0) with Unknown _ -> () | r -> failwithf "%A" r
        match s.Health with OutcomeUnknown _ -> () | h -> failwithf "%A" h
        match s.Submit([ 1 ], t0) with ReadOnly _ -> () | r -> failwithf "%A" r
        Assert.Equal(0UL, s.LastId)

[<Fact>]
let ``snapshot save failure does not affect commits`` () =
    let d = tempDir ()
    use s = openAt d
    Directory.CreateDirectory(Path.Combine(d, "snapshot")) |> ignore   // 真实故障：目标是目录，改名失败
    committed (s.Submit([ 1 ], t0)) |> ignore
    Assert.True(s.SaveSnapshot().IsError)
    Assert.True(s.LastSnapshotError.IsSome)
    Assert.Equal(2UL, (committed (s.Submit([ 1 ], t0))).id)

[<Fact>]
let ``second store on the same directory is refused`` () =
    let d = tempDir ()
    use _s = openAt d
    Assert.Throws<InvalidOperationException>(fun () -> openAt d |> ignore) |> ignore

[<Fact>]
let ``concurrent submits each commit exactly once`` () =
    use s = openAt (tempDir ())
    Threading.Tasks.Parallel.For(0, 100, fun _ -> committed (s.Submit([ 1 ], t0)) |> ignore) |> ignore
    Assert.Equal((100UL, 100), (s.LastId, s.State))

let inspect d = S.Inspect(d, codec, apply, 0)

[<Fact>]
let ``inspect reads without the lock and writes nothing, also while the host holds it`` () =
    let d = tempDir ()
    Assert.Equal({ health = Healthy; startup = { snapshot = Absent; replayedCommits = 0 }; lastId = 0UL; tailMayBeInFlight = false }, inspect d)
    use s = openAt d
    for n in 1..3 do committed (s.Submit([ n ], t0)) |> ignore
    s.SaveSnapshot() |> ignore
    committed (s.Submit([ 1 ], t0)) |> ignore
    let before = File.ReadAllBytes(logOf d)
    let i = inspect d
    Assert.Equal({ health = Healthy; startup = { snapshot = Used 3UL; replayedCommits = 1 }; lastId = 4UL; tailMayBeInFlight = false }, i)
    Assert.Equal<byte[]>(before, File.ReadAllBytes(logOf d))
    // 宿主仍持锁、仍可写
    Assert.Equal(5UL, (committed (s.Submit([ 1 ], t0))).id)

[<Fact>]
let ``inspect while the host writes sees a consistent prefix`` () =
    let d = tempDir ()
    use s = openWith { everyCommits = Some 50; onClose = false } d
    let writer = Threading.Tasks.Task.Run(fun () -> for _ in 1..400 do committed (s.Submit([ 1 ], t0)) |> ignore)
    let mutable last = 0UL
    while not writer.IsCompleted do
        let i = inspect d
        match i.health with
        | Healthy -> ()
        | Damaged _ when i.tailMayBeInFlight -> ()
        | h -> failwithf "%A" h
        Assert.True(i.lastId >= last)
        last <- i.lastId
    writer.Wait()
    Assert.Equal(400UL, (inspect d).lastId)

[<Fact>]
let ``inspect reports damage with evidence; torn tail is flagged as possibly in flight`` () =
    let d = tempDir ()
    do
        use s = openAt d
        committed (s.Submit([ 1 ], t0)) |> ignore
    let good = FileInfo(logOf d).Length
    File.AppendAllText(logOf d, "\u001e{\"v\":1")
    match inspect d with
    | { health = Damaged(o, _); lastId = 1UL; tailMayBeInFlight = true } -> Assert.Equal(good, o)
    | i -> failwithf "%A" i
    File.AppendAllText(logOf d, "\u001e{}\n")
    match inspect d with
    | { health = Damaged(o, _); tailMayBeInFlight = false } -> Assert.Equal(good, o)
    | i -> failwithf "%A" i

[<Fact>]
let ``unknown outcome names the commit that may exist`` () =
    if OperatingSystem.IsLinux() then
        let d = tempDir ()
        Directory.CreateDirectory d |> ignore
        File.CreateSymbolicLink(logOf d, "/dev/full") |> ignore
        use s = openAt d
        match s.Submit([ 1 ], t0) with Unknown(1UL, _) -> () | r -> failwithf "%A" r
        match s.Health with OutcomeUnknown(1UL, _) -> () | h -> failwithf "%A" h
        match s.Submit([ 1 ], t0) with ReadOnly why -> Assert.Contains("commit 1 may or may not have been saved", why) | r -> failwithf "%A" r

/// 更新版本写入的记录不是损坏：Health 是 NewerFormat，只读，Repair 拒绝截断（截掉会毁掉新版本的数据）。
[<Fact>]
let ``a record written by a newer version is read-only and never repaired`` () =
    let d = tempDir ()
    do
        use s = openAt d
        committed (s.Submit([ 1 ], t0)) |> ignore
    let good = FileInfo(logOf d).Length
    File.AppendAllText(logOf d, "\u001e{\"v\":99,\"id\":\"2\",\"at\":\"2026-01-01T00:00:00Z\",\"events\":[1]}\n")
    let size = FileInfo(logOf d).Length
    use s = openAt d
    match s.Health with NewerFormat(o, why) -> Assert.Equal(good, o); Assert.Contains("newer version", why) | h -> failwithf "%A" h
    Assert.True(Result.isError (s.Repair()))
    match s.Submit([ 1 ], t0) with ReadOnly why -> Assert.Contains("newer version", why) | r -> failwithf "%A" r
    Assert.Equal(size, FileInfo(logOf d).Length)
