module LedgerTests

open System
open System.Text.Json.Nodes
open Xunit
open Ledger
open Toy

let ok = function Ok x -> x | Error p -> failwithf "%O" p

// SPEC 1：id 从 1 起连续；把同样的事实按序记入新实例得到相同 Checkpoint
[<Fact>]
let ``ids are contiguous and recording the same facts reproduces checkpoint`` () =
    let l = L(apply, 0)
    let cs = [ for n in 1..5 -> commit l 10L [ Add n ] None ]
    Assert.Equal<uint64 list>([ 1UL..5UL ], cs |> List.map _.id)
    let r = L(apply, 0)
    for c in cs do ok (r.Record(c, (l.SpanOf c.id).Value))
    Assert.Equal(l.Checkpoint, r.Checkpoint)

// SPEC 2：提议在 Record 之前不改变状态；未落定时再 Propose 抛出，账本不变
[<Fact>]
let ``a proposal changes nothing until recorded and blocks a second proposal`` () =
    let l = L(apply, 0)
    let before = l.Checkpoint
    match l.Propose([ Add 1 ], t0) with Proposed c -> Assert.Equal(1UL, c.id) | d -> failwithf "%A" d
    Assert.Equal(before, l.Checkpoint)
    Assert.Throws<InvalidOperationException>(fun () -> l.Propose([ Add 2 ], t0) |> ignore) |> ignore
    Assert.Equal(before, l.Checkpoint)

// SPEC 3：Abandon 后状态不变、id 复用
[<Fact>]
let ``abandon keeps state and reuses the id`` () =
    let l = L(apply, 0)
    commit l 5L [ Add 1 ] None |> ignore
    match l.Propose([ Add 2 ], t0) with Proposed c -> Assert.Equal(2UL, c.id) | d -> failwithf "%A" d
    l.Abandon()
    Assert.Equal(1, l.State)
    Assert.Equal(2UL, (commit l 5L [ Add 2 ] None).id)
    Assert.Equal(3, l.State)

// SPEC 4：折叠被拒带领域自己的错误，不产生提议
[<Fact>]
let ``rejection carries the domain error and leaves no proposal`` () =
    let l = L(apply, 0)
    match l.Propose([ Add -3 ], t0) with Rejected(Negative -3) -> () | d -> failwithf "%A" d
    Assert.Equal(1UL, (commit l 5L [ Add 1 ] None).id)

// SPEC 5：幂等；记入重放事实后仍成立
[<Fact>]
let ``same stamp is duplicate with span, different hash conflicts, survives replay`` () =
    let l = L(apply, 0)
    commit l 5L [ Add 7 ] None |> ignore
    let st = { commandId = "c1"; hash = "h1" }
    let c = commit l 5L [ Add 1 ] (Some st)
    let span = (l.SpanOf c.id).Value
    match l.Propose([ Add 1 ], t0, st) with Duplicate(id, x) when id = c.id && x = span -> () | d -> failwithf "%A" d
    match l.Propose([ Add 1 ], t0, { st with hash = "h2" }) with StampConflict id when id = c.id -> () | d -> failwithf "%A" d
    let r = L(apply, 0)
    for id, s in l.SpansAfter 0UL do
        ok (r.Record((if id = c.id then c else { id = id; at = t0; stamp = None; events = [ Add 7 ] }), s))
    match r.Propose([ Add 1 ], t0, st) with Duplicate(2UL, x) when x = span -> () | d -> failwithf "%A" d
    Assert.Equal<Map<string, string * uint64>>(Map [ "c1", ("h1", 2UL) ], r.Stamps)

// SPEC 6：Record 拒绝断档、区间缺口、重复 commandId、折叠失败、与未落定提议不符；账本不变
[<Fact>]
let ``record rejects every kind of inconsistency without change`` () =
    let l = L(apply, 0)
    let st = Some { commandId = "c"; hash = "h" }
    ok (l.Record({ id = 1UL; at = t0; stamp = st; events = [ Add 1 ] }, sp 0L 5L))
    let before = l.Checkpoint
    let cases =
        [ { id = 3UL; at = t0; stamp = None; events = [] }, sp 5L 9L, (function IdGap(2UL, 3UL) -> true | _ -> false)
          { id = 2UL; at = t0; stamp = None; events = [] }, sp 6L 9L, (function SpanGap(5L, _) -> true | _ -> false)
          { id = 2UL; at = t0; stamp = None; events = [] }, sp 5L 5L, (function SpanGap(5L, _) -> true | _ -> false)
          { id = 2UL; at = t0; stamp = st; events = [] }, sp 5L 9L, (function RepeatedStamp "c" -> true | _ -> false)
          { id = 2UL; at = t0; stamp = None; events = [ Add -9 ] }, sp 5L 9L, (function FoldRejected(Negative -8) -> true | _ -> false) ]
    for c, span, expect in cases do
        match l.Record(c, span) with
        | Error p when expect p -> ()
        | r -> failwithf "%A" r
        Assert.Equal(before, l.Checkpoint)
    let p = match l.Propose([ Add 1 ], t0) with Proposed c -> c | d -> failwithf "%A" d
    // 提议的区间不接续也被拒，提议仍在
    match l.Record(p, sp 6L 9L) with Error(SpanGap _) -> () | r -> failwithf "%A" r
    match l.Record({ id = 3UL; at = t0; stamp = None; events = [] }, sp 5L 9L) with
    | Error(Unsettled 2UL) -> ()
    | r -> failwithf "%A" r
    Assert.Equal(before, l.Checkpoint)
    ok (l.Record(p, sp 5L 9L))
    Assert.Equal(2UL, l.LastId)

// SPEC 7：状态有主人：输入与返回的数组不与内部共享；错误之后仍可用
[<Fact>]
let ``checkpoint arrays are copies in and out; errors leave the ledger usable`` () =
    let l = L(apply, 0)
    for _ in 1..3 do commit l 4L [ Add 1 ] None |> ignore
    let cp = l.Checkpoint
    let input = Array.copy cp.ends
    let r = L(apply, { cp with ends = input })
    input[0] <- 999L
    Assert.Equal(cp, r.Checkpoint)
    let out = r.Checkpoint
    out.ends[0] <- 999L
    Assert.Equal(cp, r.Checkpoint)
    Assert.True((r.Record({ id = 9UL; at = t0; stamp = None; events = [] }, sp 0L 1L)).IsError)
    Assert.Equal(4UL, (commit r 4L [ Add 1 ] None).id)

// SPEC 8：实例之间不共享状态
[<Fact>]
let ``instances are isolated`` () =
    let a, b = L(apply, 0), L(apply, 0)
    commit a 4L [ Add 1 ] None |> ignore
    Assert.Equal(0UL, b.LastId)
    match b.Propose([ Add 1 ], t0) with Proposed c -> Assert.Equal(1UL, c.id) | d -> failwithf "%A" d

// SPEC 9：Checkpoint 只存末尾偏移；恢复等价；不一致时拒绝构造
[<Fact>]
let ``checkpoint stores only ends, restores equivalently, rejects inconsistency`` () =
    let l = L(apply, 0)
    for n in 1..4 do commit l 3L [ Add n ] (Some { commandId = string n; hash = "h" }) |> ignore
    let cp = l.Checkpoint
    Assert.Equal(4, cp.ends.Length)
    let r = L(apply, cp)
    Assert.Equal(cp, r.Checkpoint)
    Assert.Equal<(uint64 * Span) list>(l.SpansAfter 0UL, r.SpansAfter 0UL)
    Assert.Throws<ArgumentException>(fun () -> L(apply, { cp with ends = [||] }) |> ignore) |> ignore
    Assert.Throws<ArgumentException>(fun () -> L(apply, { cp with ends = Array.rev cp.ends }) |> ignore) |> ignore

// SPEC 10：区间查询与分页；相邻区间首尾相接
[<Fact>]
let ``spans after cursor honour limit and are contiguous`` () =
    let l = L(apply, 0, 100L)
    for n in 1..7 do commit l (int64 n) [ Add n ] None |> ignore
    let all = l.SpansAfter 0UL
    Assert.Equal(7, all.Length)
    Assert.Equal(100L, (snd all[0]).start)
    for (_, a), (_, b) in List.pairwise all do Assert.Equal(a.``end``, b.start)
    Assert.Equal<(uint64 * Span) list>(all |> List.skip 2 |> List.take 2, l.SpansAfter(2UL, 2))
    Assert.Empty(l.SpansAfter 7UL)
    Assert.Empty(l.SpansAfter(0UL, 0))
    Assert.Equal(Some(snd all[4]), l.SpanOf 5UL)
    Assert.Equal(None, l.SpanOf 8UL)
    Assert.Equal(None, l.SpanOf 0UL)

// SPEC 11：外壳往返；更高版本 NewerVersion；坏输入 Invalid
[<Fact>]
let ``envelope round-trips, newer version and garbage are distinguished`` () =
    let c = { id = UInt64.MaxValue; at = t0; stamp = Some { commandId = "x"; hash = "y" }; events = [ Add 1; Reset ] }
    match Envelope.read (Envelope.write (Commit.map writeEv c)) with
    | Envelope.Parsed cj -> Assert.Equal(Ok c, ofJson cj)
    | r -> failwithf "%A" r
    Assert.Equal(Envelope.NewerVersion 2, Envelope.read """{"v":2}""")
    for bad in [ "nope"; """{"v":1,"id":"1","at":"2026-01-01T00:00:00+00:00","events":[null]}"""; """{"v":0}""" ] do
        match Envelope.read bad with Envelope.Invalid _ -> () | r -> failwithf "%s -> %A" bad r

// SPEC 12：RecordProblem 有可读描述
[<Fact>]
let ``record problems describe themselves`` () =
    let ps : RecordProblem<ToyError> list =
        [ IdGap(2UL, 5UL); SpanGap(5L, sp 6L 9L); RepeatedStamp "c"; FoldRejected(Negative -1); Unsettled 3UL ]
    let texts = ps |> List.map string
    Assert.All(texts, fun t -> Assert.False(String.IsNullOrWhiteSpace t))
    Assert.Equal(texts.Length, (List.distinct texts).Length)
    Assert.Contains("expected 2, got 5", texts[0])
    Assert.Contains("Negative -1", texts[3])

// SPEC 13：Checkpoint JSON 往返（只有状态由调用方编码）；坏输入报错
[<Fact>]
let ``checkpoint json round-trips with caller-encoded state`` () =
    let l = L(apply, 0)
    for n in 1..3 do commit l 6L [ Add n ] (Some { commandId = sprintf "c%d" n; hash = "h" }) |> ignore
    let cp = l.Checkpoint
    let text = CheckpointJson.write (cp |> Checkpoint.map (fun (n: int) -> JsonValue.Create n :> JsonNode))
    match CheckpointJson.read text with
    | Ok j ->
        let back = j |> Checkpoint.map _.GetValue<int>()
        Assert.Equal(cp, back)
        Assert.Equal(cp, (L(apply, back)).Checkpoint)
    | Error e -> failwith e
    for bad in [ "nope"; """{"v":2}"""; """{"v":1,"throughId":"0","origin":-1,"ends":[],"stamps":{},"state":0}""" ] do
        Assert.True((CheckpointJson.read bad).IsError, bad)
