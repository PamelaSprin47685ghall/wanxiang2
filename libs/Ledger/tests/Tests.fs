module LedgerTests

open System
open Xunit
open Ledger
open Toy

let committed = function Committed(_, c, _) -> c | r -> failwithf "expected Committed, got %A" r
let sp a b = { start = a; ``end`` = b }

// SPEC 1：id 从 1 起连续；重放得到相同 Checkpoint
[<Fact>]
let ``ids are contiguous and replay reproduces checkpoint`` () =
    let s = Store()
    let ids = [ for n in 1..5 -> (committed (s.Submit [ Add n ])).id ]
    Assert.Equal<uint64 list>([ 1UL..5UL ], ids)
    let l, rej = replay s.Lines
    Assert.Equal(None, rej)
    Assert.Equal(s.Ledger.Checkpoint, l.Checkpoint)

// SPEC 2：同一时刻至多一条在途；在途期间的请求等落定后按落定状态裁决
[<Fact>]
let ``one append in flight, later submits wait and are decided on settled state`` () =
    let l = L(apply, 0)
    let out = ResizeArray()
    l.Outbox.Add out.Add
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    l.Post(Submit(a, [ Add 1 ], t0, None))
    l.Post(Submit(b, [ Add -1 ], t0, None))
    Assert.Equal(1, out.Count)
    match out[0] with AppendRequested(t, c, 0L) when t = a && c.id = 1UL -> () | o -> failwithf "%A" o
    l.Post(Appended(1UL, 10L))
    match List.ofSeq out with
    | [ _; Committed(Some t1, _, s1); AppendRequested(t2, c2, 10L) ] when t1 = a && s1 = sp 0L 10L && t2 = b && c2.id = 2UL -> ()
    | o -> failwithf "%A" o

// SPEC 3：写失败 → Refused WriteFailed，状态不变，id 复用
[<Fact>]
let ``append failure refuses and keeps id`` () =
    let s = Store()
    s.Submit [ Add 1 ] |> ignore
    s.FailNext()
    match s.Submit [ Add 2 ] with Refused(_, WriteFailed _) -> () | r -> failwithf "%A" r
    Assert.Equal(1, s.Ledger.State)
    Assert.Equal(2UL, (committed (s.Submit [ Add 2 ])).id)

// SPEC 4：折叠被拒带领域自己的错误，不发 AppendRequested
[<Fact>]
let ``rejection carries the domain error and never requests an append`` () =
    let s = Store()
    match s.Submit [ Add -3 ] with Refused(_, Rejected(Negative -3)) -> () | r -> failwithf "%A" r
    Assert.DoesNotContain(s.Out, fun o -> match o with AppendRequested _ -> true | _ -> false)

// SPEC 5：幂等
[<Fact>]
let ``same stamp is duplicate with span, different hash conflicts, survives replay`` () =
    let s = Store()
    s.Submit [ Add 7 ] |> ignore
    let st = { commandId = "c1"; hash = "h1" }
    let c = committed (s.Submit([ Add 1 ], st))
    let span = (s.Ledger.SpanOf c.id).Value
    match s.Submit([ Add 1 ], st) with Refused(_, Duplicate(id, x)) when id = c.id && x = span -> () | r -> failwithf "%A" r
    match s.Submit([ Add 1 ], { st with hash = "h2" }) with Refused(_, StampConflict id) when id = c.id -> () | r -> failwithf "%A" r
    let l, _ = replay s.Lines
    match (Store l).Submit([ Add 1 ], st) with Refused(_, Duplicate(2UL, x)) when x = span -> () | r -> failwithf "%A" r

// SPEC 6：重放拒绝断档、区间缺口、重复 commandId、折叠失败、忙碌，账本不变
[<Fact>]
let ``replay rejects every kind of inconsistency without change`` () =
    let l = L(apply, 0)
    let out = ResizeArray()
    l.Outbox.Add out.Add
    let st = Some { commandId = "c"; hash = "h" }
    l.Post(Replay({ id = 1UL; at = t0; stamp = st; events = [ Add 1 ] }, sp 0L 5L))
    let before = l.Checkpoint
    let cases =
        [ { id = 3UL; at = t0; stamp = None; events = [] }, sp 5L 9L, (function IdGap(2UL, 3UL) -> true | _ -> false)
          { id = 2UL; at = t0; stamp = None; events = [] }, sp 6L 9L, (function SpanGap(5L, _) -> true | _ -> false)
          { id = 2UL; at = t0; stamp = st; events = [] }, sp 5L 9L, (function RepeatedStamp "c" -> true | _ -> false)
          { id = 2UL; at = t0; stamp = None; events = [ Add -9 ] }, sp 5L 9L, (function FoldRejected(Negative -8) -> true | _ -> false) ]
    for c, span, expect in cases do
        l.Post(Replay(c, span))
        Assert.Equal(before, l.Checkpoint)
        match Seq.last out with ReplayRejected(_, _, p) when expect p -> () | o -> failwithf "%A" o
    l.Post(Submit(Guid.NewGuid(), [ Add 1 ], t0, None))
    l.Post(Replay({ id = 2UL; at = t0; stamp = None; events = [] }, sp 5L 9L))
    match Seq.last out with ReplayRejected(_, _, Busy) -> () | o -> failwithf "%A" o

// SPEC 7：回复对不上或区间不前进是协议错误；之后仍可用
[<Fact>]
let ``mismatched reply is a protocol error and ledger stays usable`` () =
    let l = L(apply, 0)
    Assert.Throws<InvalidOperationException>(fun () -> l.Post(Appended(5UL, 9L))) |> ignore
    l.Post(Submit(Guid.NewGuid(), [ Add 1 ], t0, None))
    Assert.Throws<InvalidOperationException>(fun () -> l.Post(Appended(1UL, 0L))) |> ignore
    l.Post(Appended(1UL, 4L))
    Assert.Equal(1UL, l.LastId)

// SPEC 8：在 Outbox 处理器里 Ask 直接抛出，而不是永远等待
[<Fact>]
let ``Ask from inside an Outbox handler throws instead of deadlocking`` () =
    let l = L(apply, 0)
    let caught = ResizeArray<exn>()
    l.Outbox.Add(fun o ->
        match o with
        | AppendRequested(_, c, start) ->
            if c.id = 1UL then
                try l.Ask([ Add 1 ], t0) |> ignore with e -> caught.Add e
            l.Post(Appended(c.id, start + 1L))
        | _ -> ())
    let t = Threading.Tasks.Task.Run(fun () -> l.Ask([ Add 1 ], t0))
    Assert.True(t.Wait(TimeSpan.FromSeconds 5.0), "deadlocked")
    Assert.IsType<InvalidOperationException>(Assert.Single caught) |> ignore

// SPEC 9：Checkpoint 只存末尾偏移；恢复等价；不一致时拒绝构造
[<Fact>]
let ``checkpoint stores only ends, restores equivalently, rejects inconsistency`` () =
    let s = Store()
    for n in 1..4 do s.Submit([ Add n ], { commandId = string n; hash = "h" }) |> ignore
    let cp = s.Ledger.Checkpoint
    Assert.Equal(4, cp.ends.Length)
    let r = L(apply, cp)
    Assert.Equal(cp, r.Checkpoint)
    Assert.Equal<(uint64 * Span) list>(s.Ledger.SpansAfter 0UL, r.SpansAfter 0UL)
    Assert.Throws<ArgumentException>(fun () -> L(apply, { cp with ends = [||] }) |> ignore) |> ignore
    Assert.Throws<ArgumentException>(fun () -> L(apply, { cp with ends = Array.rev cp.ends }) |> ignore) |> ignore

// SPEC 10：区间查询与分页；相邻区间首尾相接
[<Fact>]
let ``spans after cursor honour limit and are contiguous`` () =
    let s = Store()
    for n in 1..7 do s.Submit [ Add n ] |> ignore
    let l = s.Ledger
    let all = l.SpansAfter 0UL
    Assert.Equal(7, all.Length)
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

// SPEC 12：并发 Ask：每个请求恰好提交一次，Committed 按 id 顺序
[<Fact>]
let ``concurrent asks commit every submit exactly once in id order`` () =
    let s = Store()
    Threading.Tasks.Parallel.For(0, 200, fun _ -> committed (s.Submit [ Add 1 ]) |> ignore) |> ignore
    let ids = s.Out |> Seq.choose (function Committed(_, c, _) -> Some c.id | _ -> None) |> List.ofSeq
    Assert.Equal<uint64 list>([ 1UL..200UL ], ids)
    Assert.Equal(200, s.Ledger.State)

// SPEC 13：ReplayProblem 有可读描述
[<Fact>]
let ``replay problems describe themselves`` () =
    let ps : ReplayProblem<ToyError> list =
        [ IdGap(2UL, 5UL); SpanGap(5L, sp 6L 9L); RepeatedStamp "c"; FoldRejected(Negative -1); Busy ]
    let texts = ps |> List.map string
    Assert.All(texts, fun t -> Assert.False(String.IsNullOrWhiteSpace t))
    Assert.Equal(texts.Length, (List.distinct texts).Length)
    Assert.Contains("expected 2, got 5", texts[0])
    Assert.Contains("Negative -1", texts[3])

// SPEC 14：Checkpoint JSON 往返（只有状态由调用方编码）；坏输入报错
[<Fact>]
let ``checkpoint json round-trips with caller-encoded state`` () =
    let s = Store()
    for n in 1..3 do s.Submit([ Add n ], { commandId = sprintf "c%d" n; hash = "h" }) |> ignore
    let cp = s.Ledger.Checkpoint
    let text = CheckpointJson.write (cp |> Checkpoint.map (fun (n: int) -> Text.Json.Nodes.JsonValue.Create n :> Text.Json.Nodes.JsonNode))
    match CheckpointJson.read text with
    | Ok j ->
        let back = j |> Checkpoint.map _.GetValue<int>()
        Assert.Equal(cp, back)
        Assert.Equal(cp, (L(apply, back)).Checkpoint)
    | Error e -> failwith e
    for bad in [ "nope"; """{"v":2}"""; """{"v":1,"throughId":"0","origin":-1,"ends":[],"stamps":{},"state":0}""" ] do
        Assert.True((CheckpointJson.read bad).IsError, bad)
