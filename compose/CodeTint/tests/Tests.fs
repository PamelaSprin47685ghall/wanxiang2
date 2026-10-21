module Wanxiang2.CodeTint.Tests.Tests

open System
open System.Net
open Xunit
open Wanxiang.UI
open Wanxiang2.CodeTint

/// 可控宿主：后台工作与延时回调都排进队列，由测试逐个放行；后端是记数桩（旧 AsyncHighlightTests 的 CountingBackend）。
type private Rig(?synchronous: bool) =
    let work = Collections.Generic.Queue<unit -> unit>()
    let timers = Collections.Generic.List<int * (unit -> unit)>()
    let mutable calls = 0

    let tint =
        CodeTint
            {
                HighlightHtml =
                    fun (code, _) ->
                        calls <- calls + 1
                        let tail = if code.Length > 3 then code.Substring 3 else ""
                        Some("<span class=\"hljs-keyword\">let</span>" + WebUtility.HtmlEncode tail)
                Background = work.Enqueue
                After = fun ms f -> timers.Add(ms, f)
                Synchronous = defaultArg synchronous false
            }

    member _.Tint = tint
    member _.Calls = calls
    member _.Timers = timers

    member _.RunWork() =
        while work.Count > 0 do
            work.Dequeue () ()

    member _.FireTimers() =
        let due = List.ofSeq timers
        timers.Clear()

        for _, f in due do
            f ()

let private kinds (tokens: CodeToken list) = tokens |> List.map (fun t -> t.kind)
let private code tag = "let " + tag + " = 1"

[<Fact>]
let ``桌面端先给纯文本，后台算完再换成着色版`` () =
    let rig = Rig()
    let mutable ready = 0
    rig.Tint.Ready.Add(fun () -> ready <- ready + 1)
    let c = code "delayed"
    Assert.Equal<CodeTokenKind list>([ CodePlain ], kinds (rig.Tint.Tokenize(true, "fsharp", c)))
    rig.RunWork()
    Assert.Equal(0, ready)
    rig.FireTimers()
    Assert.Equal(1, ready)
    let highlighted = rig.Tint.Tokenize(true, "fsharp", c)
    Assert.Contains(CodeKeyword, kinds highlighted)
    // 着色不能吃字
    Assert.Equal(c, highlighted |> List.map (fun t -> t.text) |> String.concat "")

[<Fact>]
let ``流式进行中一次也不调用着色`` () =
    let rig = Rig()
    let c = code "streaming"

    for cut in 4 .. c.Length do
        rig.Tint.Tokenize(false, "fsharp", c.Substring(0, cut)) |> ignore

    rig.RunWork()
    Assert.Equal(0, rig.Calls)

[<Fact>]
let ``同一段代码只排队一次`` () =
    let rig = Rig()
    let c = code "once"

    for _ in 1..20 do
        rig.Tint.Tokenize(true, "fsharp", c) |> ignore

    rig.RunWork()
    Assert.Equal(1, rig.Calls)

[<Fact>]
let ``认不出的语言不进后台队列`` () =
    let rig = Rig()
    Assert.Equal<CodeTokenKind list>([ CodePlain ], kinds (rig.Tint.Tokenize(true, "沒有這種語言", "任意内容")))
    rig.RunWork()
    Assert.Equal(0, rig.Calls)

[<Fact>]
let ``多段代码同时算完只通知一次`` () =
    let rig = Rig()
    let mutable ready = 0
    rig.Tint.Ready.Add(fun () -> ready <- ready + 1)
    let blocks = [ for i in 1..20 -> code ("batch" + string i) ]

    for c in blocks do
        rig.Tint.Tokenize(true, "fsharp", c) |> ignore

    rig.RunWork()
    Assert.Equal(20, rig.Calls)
    // 合并窗口内只安排一次通知，窗口取 HighlightQueue 的 120ms
    Assert.Equal<int list>([ 120 ], rig.Timers |> Seq.map fst |> List.ofSeq)
    rig.FireTimers()
    Assert.Equal(1, ready)

    for c in blocks do
        Assert.Contains(CodeKeyword, kinds (rig.Tint.Tokenize(true, "fsharp", c)))

[<Fact>]
let ``synchronous host colors on the spot and caches`` () =
    let rig = Rig(true)
    let c = code "browser"
    Assert.Contains(CodeKeyword, kinds (rig.Tint.Tokenize(true, "F#", c)))
    rig.Tint.Tokenize(true, "fsharp", c) |> ignore
    Assert.Equal(1, rig.Calls)
    Assert.Empty rig.Timers

[<Fact>]
let ``empty code yields no tokens and a failing backend falls back to plain`` () =
    let rig = Rig()
    Assert.Empty(rig.Tint.Tokenize(true, "fsharp", ""))

    let failing =
        CodeTint
            {
                HighlightHtml = (fun _ -> None)
                Background = (fun f -> f ())
                After = (fun _ f -> f ())
                Synchronous = false
            }

    failing.Tokenize(true, "fsharp", "let x = 1") |> ignore
    Assert.Equal<CodeTokenKind list>([ CodePlain ], kinds (failing.Tokenize(true, "fsharp", "let x = 1")))

[<Fact>]
let ``instances do not share caches`` () =
    let a = Rig(true)
    let b = Rig(true)
    a.Tint.Tokenize(true, "fsharp", code "shared") |> ignore
    b.Tint.Tokenize(true, "fsharp", code "shared") |> ignore
    Assert.Equal(1, a.Calls)
    Assert.Equal(1, b.Calls)
