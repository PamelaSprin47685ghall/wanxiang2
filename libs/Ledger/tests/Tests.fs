module LedgerTests

open System
open Xunit
open Ledger
open Toy

let accepted = function Ok(Accept c) -> c | r -> failwithf "expected Accept, got %A" r

let replay (lines: string seq) =
    let l = Ledger<int, Ev, int>(apply, 0)
    let errs =
        lines |> Seq.mapi (fun i line ->
            match Envelope.read line with
            | Envelope.Parsed cj -> ofJson cj |> Result.bind (fun c -> l.Record(c, i))
            | other -> Error(sprintf "%A" other))
        |> Seq.tryPick (function Error e -> Some e | Ok() -> None)
    l, errs

// SPEC 1：id 从 1 起连续；重放得到同样的状态
[<Fact>]
let ``ids are contiguous and replay reproduces state`` () =
    let s = Store()
    let ids = [ for n in 1..5 -> (accepted (s.Submit [ Add n ])).id ]
    Assert.Equal<uint64 list>([ 1UL..5UL ], ids)
    let l, err = replay s.Lines
    Assert.Equal(None, err)
    Assert.Equal(15, l.State)
    Assert.Equal(s.Ledger.Checkpoint, l.Checkpoint)

// SPEC 2：Decide 不改变账本；被拒的不产生提交
[<Fact>]
let ``decide does not change the ledger and rejection is reported`` () =
    let l = Ledger<int, Ev, int>(apply, 0)
    let a = l.Decide([ Add 1 ], t0)
    let b = l.Decide([ Add 1 ], t0)
    Assert.Equal(a, b)
    Assert.Equal(0UL, l.LastId)
    match l.Decide([ Add -1 ], t0) with Rejected _ -> () | r -> failwithf "%A" r

// SPEC 3：Record 拒绝 id 断档、重复 commandId、折叠失败，且账本不变
[<Fact>]
let ``record rejects gaps, repeated stamps and failing folds without change`` () =
    let l = Ledger<int, Ev, int>(apply, 0)
    let st = Some { commandId = "c"; hash = "h" }
    Assert.True(l.Record({ id = 1UL; at = t0; stamp = st; events = [ Add 1 ] }, 0).IsOk)
    let before = l.Checkpoint
    for c in [ { id = 3UL; at = t0; stamp = None; events = [] }
               { id = 2UL; at = t0; stamp = st; events = [] }
               { id = 2UL; at = t0; stamp = None; events = [ Add -9 ] } ] do
        Assert.True((l.Record(c, 1)).IsError)
        Assert.Equal(before, l.Checkpoint)

// SPEC 4：幂等裁决给出原提交的位置
[<Fact>]
let ``same stamp is duplicate with position, different hash conflicts`` () =
    let s = Store()
    s.Submit [ Add 7 ] |> ignore
    let st = { commandId = "c1"; hash = "h1" }
    let c = accepted (s.Submit([ Add 1 ], st))
    Assert.Equal(Ok(Duplicate(c.id, 1)), s.Submit([ Add 1 ], st))
    Assert.Equal(Ok(StampConflict c.id), s.Submit([ Add 1 ], { st with hash = "h2" }))
    let l, _ = replay s.Lines
    Assert.Equal(Duplicate(2UL, 1), l.Decide([ Add 1 ], t0, st))

// SPEC 5：Checkpoint 恢复与原账本等价；位置数不符时拒绝构造
[<Fact>]
let ``checkpoint restores an equivalent ledger and rejects inconsistent positions`` () =
    let s = Store()
    for n in 1..4 do s.Submit([ Add n ], { commandId = string n; hash = "h" }) |> ignore
    let r = Ledger<int, Ev, int>(apply, s.Ledger.Checkpoint)
    Assert.Equal(s.Ledger.Checkpoint, r.Checkpoint)
    Assert.Equal(s.Ledger.Decide([ Add 1 ], t0), r.Decide([ Add 1 ], t0))
    Assert.Throws<ArgumentException>(fun () ->
        Ledger<int, Ev, int>(apply, { s.Ledger.Checkpoint with positions = [] }) |> ignore) |> ignore

// SPEC 6：位置查询与分页
[<Fact>]
let ``positions after cursor honour limit`` () =
    let s = Store()
    for n in 1..7 do s.Submit [ Add n ] |> ignore
    let l = s.Ledger
    Assert.Equal<(uint64 * int) list>([ 3UL, 2; 4UL, 3 ], l.PositionsAfter(2UL, 2))
    Assert.Equal<(uint64 * int) list>([ 7UL, 6 ], l.PositionsAfter(6UL, 10))
    Assert.Empty(l.PositionsAfter(7UL))
    Assert.Empty(l.PositionsAfter(0UL, 0))
    Assert.Equal(Some 4, l.PositionOf 5UL)
    Assert.Equal(None, l.PositionOf 8UL)
    Assert.Equal(None, l.PositionOf 0UL)

// SPEC 7：外壳往返（含 uint64 最大值）；版本更高时报告 NewerVersion；坏输入 Invalid
[<Fact>]
let ``envelope round-trips, newer version and garbage are distinguished`` () =
    let c = { id = UInt64.MaxValue; at = t0; stamp = Some { commandId = "x"; hash = "y" }; events = [ Add 1; Reset ] }
    let text = Envelope.write (Commit.map writeEv c)
    match Envelope.read text with
    | Envelope.Parsed cj -> Assert.Equal(Ok c, ofJson cj)
    | r -> failwithf "%A" r
    Assert.Equal(Envelope.NewerVersion 2, Envelope.read """{"v":2}""")
    for bad in [ "nope"; """{"v":1,"id":"1","at":"2026-01-01T00:00:00+00:00","events":[null]}"""; """{"v":0}""" ] do
        match Envelope.read bad with Envelope.Invalid _ -> () | r -> failwithf "%s -> %A" bad r

// SPEC 8：Committed 按 id 顺序触发，带位置
[<Fact>]
let ``committed fires in id order with position`` () =
    let s = Store()
    let seen = ResizeArray()
    s.Ledger.Committed.Add(fun (c, p) -> seen.Add(c.id, p))
    for n in 1..3 do s.Submit [ Add n ] |> ignore
    Assert.Equal<(uint64 * int) list>([ 1UL, 0; 2UL, 1; 3UL, 2 ], List.ofSeq seen)
