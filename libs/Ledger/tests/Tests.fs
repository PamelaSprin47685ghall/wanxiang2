module LedgerTests

open System
open Xunit
open Ledger
open Toy

let ok = function Committed c -> c | r -> failwithf "expected Committed, got %A" r

// SPEC 1：id 从 1 起连续；重开后状态与提交一致
[<Fact>]
let ``ids are contiguous from 1 and survive reopen`` () =
    let m = MemLog()
    let l = openReady m.Log (Checkpoint.empty 0)
    let ids = [ for n in 1..5 -> (ok (l.Submit [ Add n ])).id ]
    Assert.Equal<uint64 list>([ 1UL..5UL ], ids)
    let l2 = openReady m.Log (Checkpoint.empty 0)
    Assert.Equal(15, l2.State)
    Assert.Equal(5UL, l2.LastId)

// SPEC 2：先折叠后写盘，被拒则不写
[<Fact>]
let ``rejected fold writes nothing`` () =
    let m = MemLog()
    let l = openReady m.Log (Checkpoint.empty 0)
    ok (l.Submit [ Add 1 ]) |> ignore
    match l.Submit [ Add -5 ] with Rejected _ -> () | r -> failwithf "%A" r
    Assert.Equal(1, m.Records.Count)
    Assert.Equal(2UL, (ok (l.Submit [ Add 1 ])).id)

// SPEC 3：写失败回滚，状态与 id 不变
[<Fact>]
let ``write failure rolls back and keeps id`` () =
    let m = MemLog()
    let l = openReady m.Log (Checkpoint.empty 0)
    ok (l.Submit [ Add 1 ]) |> ignore
    m.FailNextAppend <- true
    match l.Submit [ Add 2 ] with WriteFailed _ -> () | r -> failwithf "%A" r
    Assert.Equal(1, m.Records.Count)
    Assert.Equal(1, l.State)
    Assert.Equal(2UL, (ok (l.Submit [ Add 2 ])).id)
    Assert.Equal(3, (openReady m.Log (Checkpoint.empty 0)).State)

// SPEC 4：幂等
[<Fact>]
let ``same stamp is duplicate, same id different hash conflicts, survives reopen`` () =
    let m = MemLog()
    let l = openReady m.Log (Checkpoint.empty 0)
    let s = { commandId = "c1"; hash = "h1" }
    let c = ok (l.Submit([ Add 1 ], s))
    Assert.Equal(Duplicate c.id, l.Submit([ Add 1 ], s))
    Assert.Equal(StampConflict c.id, l.Submit([ Add 1 ], { s with hash = "h2" }))
    let l2 = openReady m.Log (Checkpoint.empty 0)
    Assert.Equal(Duplicate c.id, l2.Submit([ Add 1 ], s))
    Assert.Equal(1, l2.State)

// SPEC 5：损坏只报告不修改；Repair 截到有效前缀
[<Fact>]
let ``torn tail is reported without modification, repair truncates`` () =
    let m = MemLog()
    let l = openReady m.Log (Checkpoint.empty 0)
    for n in 1..3 do ok (l.Submit [ Add n ]) |> ignore
    m.Records.Add { payload = "{\"v\":1"; complete = false; startOffset = m.End; endOffset = m.End + 3L }
    let before = List.ofSeq m.Records
    match Ledger.openLog m.Log codec apply (Checkpoint.empty 0) with
    | Damaged d ->
        Assert.Equal<LogRecord list>(before, List.ofSeq m.Records)
        Assert.Equal(6, d.prefix.state)
        Assert.Equal(0, d.recordsAfter)
        Ledger.repair m.Log d
        Assert.Equal(6, (openReady m.Log (Checkpoint.empty 0)).State)
    | r -> failwithf "%A" r

[<Theory>]
[<InlineData("not json")>]
[<InlineData("""{"v":1,"id":"9","at":"2026-01-01T00:00:00+00:00","events":[]}""")>]
[<InlineData("""{"v":1,"id":"3","at":"2026-01-01T00:00:00+00:00","events":[{"add":-100}]}""")>]
let ``corrupt middle record is damage and counts records after`` (bad: string) =
    let m = MemLog()
    let l = openReady m.Log (Checkpoint.empty 0)
    for n in 1..2 do ok (l.Submit [ Add n ]) |> ignore
    m.Records.Add { payload = bad; complete = true; startOffset = m.End; endOffset = m.End + 100L }
    m.Records.Add { m.Records[0] with startOffset = m.End; endOffset = m.End + 100L }
    match Ledger.openLog m.Log codec apply (Checkpoint.empty 0) with
    | Damaged d ->
        Assert.Equal(2UL, d.prefix.throughId)
        Assert.Equal(1, d.recordsAfter)
    | r -> failwithf "%A" r

// SPEC 6：未来版本拒绝打开
[<Fact>]
let ``future version is unsupported`` () =
    let m = MemLog()
    m.Records.Add { payload = """{"v":2}"""; complete = true; startOffset = 0L; endOffset = 9L }
    match Ledger.openLog m.Log codec apply (Checkpoint.empty 0) with
    | Unsupported 2 -> ()
    | r -> failwithf "%A" r

// SPEC 7：从基线续播等于完整重放；ReadAfter 跨基线正确
[<Fact>]
let ``resume from checkpoint equals full replay and ReadAfter spans checkpoint`` () =
    let m = MemLog()
    let l = openReady m.Log (Checkpoint.empty 0)
    for n in 1..4 do ok (l.Submit([ Add n ], { commandId = string n; hash = "h" })) |> ignore
    let cp = l.Checkpoint
    for n in 5..7 do ok (l.Submit [ Add n ]) |> ignore
    let full = openReady m.Log (Checkpoint.empty 0)
    let resumed = openReady m.Log cp
    Assert.Equal(full.Checkpoint, resumed.Checkpoint)
    Assert.Equal(Duplicate 2UL, resumed.Submit([ Add 2 ], { commandId = "2"; hash = "h" }))
    Assert.Equal<uint64 list>([ 3UL..7UL ], resumed.ReadAfter 2UL |> List.map _.id)
    Assert.Equal<uint64 list>([ 6UL; 7UL ], resumed.ReadAfter 5UL |> List.map _.id)
    Assert.Empty(resumed.ReadAfter 7UL)

// SPEC 8：Committed 事件按顺序触发；并发提交 id 不重不漏
[<Fact>]
let ``concurrent submits get unique contiguous ids in event order`` () =
    let m = MemLog()
    let l = openReady m.Log (Checkpoint.empty 0)
    let seen = Collections.Concurrent.ConcurrentQueue<uint64>()
    l.Committed.Add(fun c -> seen.Enqueue c.id)
    Threading.Tasks.Parallel.For(0, 200, fun _ -> ok (l.Submit [ Add 1 ]) |> ignore) |> ignore
    Assert.Equal<uint64 list>([ 1UL..200UL ], List.ofSeq seen)
    Assert.Equal(200, (openReady m.Log (Checkpoint.empty 0)).State)
