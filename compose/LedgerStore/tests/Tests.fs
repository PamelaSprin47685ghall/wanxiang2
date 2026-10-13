/// 真实组合：真实的 JsonSeqFile、Ledger、SnapshotFile、DirLock，由 LedgerStore 组合，在真实文件上运行。
module LedgerStoreTests

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open Ledger
open LedgerStore

/// 计数器：事件是整数，结果不能为负。
let apply (s: JsonNode) (c: Commit<JsonNode>) : Result<JsonNode, string> =
    let n = c.events |> List.sumBy _.GetValue<int>()
    let v = s.GetValue<int>() + n
    if v < 0 then Error(sprintf "negative %d" v) else Ok(JsonValue.Create v)

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
let ev (n: int) = JsonValue.Create n :> JsonNode
let tempDir () = Path.Combine(Path.GetTempPath(), "ledgerstore-" + Guid.NewGuid().ToString("N"))
let openAt dir = new LedgerStore(dir, apply, JsonValue.Create 0)
let value (s: LedgerStore) = s.State.GetValue<int>()
let committed = function Committed c -> c | r -> failwithf "expected Committed, got %A" r

[<Fact>]
let ``commits survive reopen with and without snapshot`` () =
    let d = tempDir ()
    do
        use s = openAt d
        for n in 1..3 do committed (s.Submit([ ev n ], t0)) |> ignore
        s.SaveSnapshot()
        committed (s.Submit([ ev 10 ], t0)) |> ignore
    do
        use s = openAt d
        Assert.Equal(Healthy, s.Health)
        Assert.Equal((4UL, 16), (s.LastId, value s))
    File.Delete(Path.Combine(d, "snapshot"))
    use s = openAt d
    Assert.Equal((4UL, 16), (s.LastId, value s))

[<Fact>]
let ``domain rejection writes nothing`` () =
    let d = tempDir ()
    use s = openAt d
    match s.Submit([ ev -1 ], t0) with Rejected _ -> () | r -> failwithf "%A" r
    Assert.Equal(0L, FileInfo(Path.Combine(d, "log.jsonseq")).Length)

[<Fact>]
let ``duplicate returns the original commit read back from the log, also after reopen`` () =
    let d = tempDir ()
    let st = { commandId = "cmd"; hash = "h" }
    let c =
        use s = openAt d
        let c = committed (s.Submit([ ev 5 ], t0, st))
        match s.Submit([ ev 5 ], t0, st) with Duplicate o -> Assert.Equal(c.id, o.id) | r -> failwithf "%A" r
        match s.Submit([ ev 5 ], t0, { st with hash = "x" }) with Conflict 1UL -> () | r -> failwithf "%A" r
        c
    use s = openAt d
    match s.Submit([ ev 5 ], t0, st) with Duplicate o -> Assert.Equal(c.id, o.id) | r -> failwithf "%A" r
    Assert.Equal(5, value s)

[<Fact>]
let ``torn tail after a crash makes the store read-only until the user repairs`` () =
    let d = tempDir ()
    do
        use s = openAt d
        for n in 1..2 do committed (s.Submit([ ev n ], t0)) |> ignore
    let log = Path.Combine(d, "log.jsonseq")
    let good = FileInfo(log).Length
    File.AppendAllText(log, "\u001e{\"v\":1,\"id\":\"3\"")   // 崩溃时半写的一条
    use s = openAt d
    match s.Health with Damaged(o, _) -> Assert.Equal(good, o) | h -> failwithf "%A" h
    match s.Submit([ ev 1 ], t0) with ReadOnly _ -> () | r -> failwithf "%A" r
    Assert.Equal(good + 16L, FileInfo(log).Length)        // 打开不改任何字节
    Assert.Equal(Ok good, s.Repair())
    Assert.Equal(3UL, (committed (s.Submit([ ev 1 ], t0))).id)
    Assert.Equal(4, value s)

[<Fact>]
let ``stale snapshot from a truncated log is ignored`` () =
    let d = tempDir ()
    do
        use s = openAt d
        for n in 1..3 do committed (s.Submit([ ev n ], t0)) |> ignore
        s.SaveSnapshot()
    File.WriteAllBytes(Path.Combine(d, "log.jsonseq"), [||])
    use s = openAt d
    Assert.Equal((0UL, 0), (s.LastId, value s))

[<Fact>]
let ``unknown write outcome stays unknown and is never turned into a retry`` () =
    // 真实故障：日志指向 /dev/full，写入 ENOSPC 且无法截回。仅 Linux。
    if OperatingSystem.IsLinux() then
        let d = tempDir ()
        Directory.CreateDirectory d |> ignore
        File.CreateSymbolicLink(Path.Combine(d, "log.jsonseq"), "/dev/full") |> ignore
        use s = openAt d
        match s.Submit([ ev 1 ], t0) with Unknown _ -> () | r -> failwithf "%A" r
        match s.Health with OutcomeUnknown _ -> () | h -> failwithf "%A" h
        match s.Submit([ ev 1 ], t0) with ReadOnly _ -> () | r -> failwithf "%A" r
        Assert.Equal(0UL, s.LastId)

[<Fact>]
let ``second store on the same directory is refused`` () =
    let d = tempDir ()
    use _s = openAt d
    Assert.Throws<InvalidOperationException>(fun () -> openAt d |> ignore) |> ignore

[<Fact>]
let ``inputs and returned state are not aliased`` () =
    let o = JsonObject()
    o["k"] <- JsonValue.Create 1
    let applyObj (st: JsonNode) (c: Commit<JsonNode>) : Result<JsonNode, string> =
        let a = st.AsArray().DeepClone().AsArray()
        for e in c.events do a.Add(e.DeepClone())
        Ok a
    let d = tempDir ()
    use s = new LedgerStore(d, applyObj, JsonArray())
    committed (s.Submit([ o ], t0)) |> ignore
    o["k"] <- JsonValue.Create 99            // 调用方事后改输入
    let st = s.State
    st.AsArray().Clear()                     // 调用方改返回值
    Assert.Equal("""[{"k":1}]""", s.State.ToJsonString())

[<Fact>]
let ``concurrent submits each commit exactly once`` () =
    use s = openAt (tempDir ())
    Threading.Tasks.Parallel.For(0, 100, fun _ -> committed (s.Submit([ ev 1 ], t0)) |> ignore) |> ignore
    Assert.Equal((100UL, 100), (s.LastId, value s))
