module LedgerTests

open System
open Xunit
open Ledger
open Toy

let committed = function Committed(_, c, _) -> c | r -> failwithf "expected Committed, got %A" r

// SPEC 1：id 从 1 起连续；重放得到相同 Checkpoint
[<Fact>]
let ``ids are contiguous and replay reproduces checkpoint`` () =
    let s = Store()
    let ids = [ for n in 1..5 -> (committed (s.Submit [ Add n ])).id ]
    Assert.Equal<uint64 list>([ 1UL..5UL ], ids)
    let l, rej = replay s.Lines
    Assert.Equal(None, rej)
    Assert.Equal(s.Ledger.Checkpoint, l.Checkpoint)

// SPEC 2：同一时刻至多一条在途；在途期间的请求排队，落定后按落定状态裁决
[<Fact>]
let ``one append in flight, later submits wait and are decided on settled state`` () =
    let l = Ledger<int, Ev, int>(apply, 0)
    let out = ResizeArray()
    l.Outbox.Add out.Add
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    l.Post(Submit(a, [ Add 1 ], t0, None))
    l.Post(Submit(b, [ Add -1 ], t0, None))   // 只有在 Add 1 落定后才合法
    Assert.Equal(1, out.Count)
    match out[0] with AppendRequested(t, c) when t = a && c.id = 1UL -> () | o -> failwithf "%A" o
    Assert.Equal(0UL, l.LastId)
    l.Post(Appended(1UL, 0))
    match List.ofSeq out with
    | [ _; Committed(Some t1, _, 0); AppendRequested(t2, c2) ] when t1 = a && t2 = b && c2.id = 2UL -> ()
    | o -> failwithf "%A" o

// SPEC 3：写失败回复 → Refused WriteFailed，账本不变，id 复用
[<Fact>]
let ``append failure refuses and keeps id`` () =
    let s = Store()
    s.Submit [ Add 1 ] |> ignore
    s.FailNext()
    match s.Submit [ Add 2 ] with Refused(_, WriteFailed _) -> () | r -> failwithf "%A" r
    Assert.Equal(1, s.Ledger.State)
    Assert.Equal(2UL, (committed (s.Submit [ Add 2 ])).id)

// SPEC 4：折叠被拒 → Refused Rejected，不发 AppendRequested
[<Fact>]
let ``rejected fold never requests an append`` () =
    let s = Store()
    match s.Submit [ Add -1 ] with Refused(_, Rejected _) -> () | r -> failwithf "%A" r
    Assert.DoesNotContain(s.Out, fun o -> match o with AppendRequested _ -> true | _ -> false)
    Assert.Empty s.Lines

// SPEC 5：幂等
[<Fact>]
let ``same stamp is duplicate with position, different hash conflicts, survives replay`` () =
    let s = Store()
    s.Submit [ Add 7 ] |> ignore
    let st = { commandId = "c1"; hash = "h1" }
    let c = committed (s.Submit([ Add 1 ], st))
    match s.Submit([ Add 1 ], st) with Refused(_, Duplicate(id, 1)) when id = c.id -> () | r -> failwithf "%A" r
    match s.Submit([ Add 1 ], { st with hash = "h2" }) with Refused(_, StampConflict id) when id = c.id -> () | r -> failwithf "%A" r
    let l, _ = replay s.Lines
    match (Store l).Submit([ Add 1 ], st) with Refused(_, Duplicate(2UL, 1)) -> () | r -> failwithf "%A" r

// SPEC 6：重放拒绝断档、重复 commandId、折叠失败、忙碌，账本不变
[<Fact>]
let ``replay rejects gaps, repeated stamps, failing folds and busy ledger`` () =
    let l = Ledger<int, Ev, int>(apply, 0)
    let out = ResizeArray()
    l.Outbox.Add out.Add
    let st = Some { commandId = "c"; hash = "h" }
    l.Post(Replay({ id = 1UL; at = t0; stamp = st; events = [ Add 1 ] }, 0))
    let before = l.Checkpoint
    for c in [ { id = 3UL; at = t0; stamp = None; events = [] }
               { id = 2UL; at = t0; stamp = st; events = [] }
               { id = 2UL; at = t0; stamp = None; events = [ Add -9 ] } ] do
        l.Post(Replay(c, 9))
        Assert.Equal(before, l.Checkpoint)
    Assert.Equal(3, out |> Seq.filter (function ReplayRejected _ -> true | _ -> false) |> Seq.length)
    l.Post(Submit(Guid.NewGuid(), [ Add 1 ], t0, None))   // 在途，不回复
    l.Post(Replay({ id = 2UL; at = t0; stamp = None; events = [] }, 1))
    match Seq.last out with ReplayRejected(_, _, why) -> Assert.Contains("busy", why) | o -> failwithf "%A" o

// SPEC 7：回复不存在的在途提交是协议错误；之后账本仍可用
[<Fact>]
let ``mismatched reply is a protocol error and ledger stays usable`` () =
    let s = Store()
    Assert.Throws<InvalidOperationException>(fun () -> s.Ledger.Post(Appended(5UL, 0))) |> ignore
    Assert.Equal(1UL, (committed (s.Submit [ Add 1 ])).id)

// SPEC 8：Checkpoint 恢复等价；位置数不符拒绝构造
[<Fact>]
let ``checkpoint restores an equivalent ledger and rejects inconsistent positions`` () =
    let s = Store()
    for n in 1..4 do s.Submit([ Add n ], { commandId = string n; hash = "h" }) |> ignore
    let r = Ledger<int, Ev, int>(apply, s.Ledger.Checkpoint)
    Assert.Equal(s.Ledger.Checkpoint, r.Checkpoint)
    Assert.Throws<ArgumentException>(fun () ->
        Ledger<int, Ev, int>(apply, { s.Ledger.Checkpoint with positions = [] }) |> ignore) |> ignore

// SPEC 9：位置查询与分页
[<Fact>]
let ``positions after cursor honour limit`` () =
    let s = Store()
    for n in 1..7 do s.Submit [ Add n ] |> ignore
    let l = s.Ledger
    Assert.Equal<(uint64 * int) list>([ 3UL, 2; 4UL, 3 ], l.PositionsAfter(2UL, 2))
    Assert.Equal<(uint64 * int) list>([ 7UL, 6 ], l.PositionsAfter(6UL, 10))
    Assert.Empty(l.PositionsAfter 7UL)
    Assert.Empty(l.PositionsAfter(0UL, 0))
    Assert.Equal(Some 4, l.PositionOf 5UL)
    Assert.Equal(None, l.PositionOf 8UL)
    Assert.Equal(None, l.PositionOf 0UL)

// SPEC 10：外壳往返（含 uint64 最大值）；更高版本 NewerVersion；坏输入 Invalid
[<Fact>]
let ``envelope round-trips, newer version and garbage are distinguished`` () =
    let c = { id = UInt64.MaxValue; at = t0; stamp = Some { commandId = "x"; hash = "y" }; events = [ Add 1; Reset ] }
    match Envelope.read (Envelope.write (Commit.map writeEv c)) with
    | Envelope.Parsed cj -> Assert.Equal(Ok c, ofJson cj)
    | r -> failwithf "%A" r
    Assert.Equal(Envelope.NewerVersion 2, Envelope.read """{"v":2}""")
    for bad in [ "nope"; """{"v":1,"id":"1","at":"2026-01-01T00:00:00+00:00","events":[null]}"""; """{"v":0}""" ] do
        match Envelope.read bad with Envelope.Invalid _ -> () | r -> failwithf "%s -> %A" bad r

// SPEC 11：并发投递：id 不重不漏，Committed 按 id 顺序
[<Fact>]
let ``concurrent posts commit every submit exactly once in id order`` () =
    let s = Store()
    Threading.Tasks.Parallel.For(0, 200, fun _ -> s.Ledger.Post(Submit(Guid.NewGuid(), [ Add 1 ], t0, None))) |> ignore
    let ids = s.Out |> Seq.choose (function Committed(_, c, _) -> Some c.id | _ -> None) |> List.ofSeq
    Assert.Equal<uint64 list>([ 1UL..200UL ], ids)
    Assert.Equal(200, s.Ledger.State)
