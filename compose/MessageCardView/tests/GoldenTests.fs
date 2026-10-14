module Wanxiang2.MessageCardView.Tests.GoldenTests

open System
open System.IO
open Avalonia.Media
open Xunit
open Wanxiang2.MessageCardView
open Wanxiang2.MessageCardView.Tests

let private golden name = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", name))

/// 逐行比对，失败时指出第一处不同
let private same (expected: string) (actual: string) =
    let e = expected.Split('\n')
    let a = actual.Split('\n')
    for i in 0 .. min e.Length a.Length - 1 do
        if e.[i] <> a.[i] then
            Assert.Fail(sprintf "line %d\nold: %s\nnew: %s\n(prev: %s)" (i + 1) e.[i] a.[i] (if i > 0 then e.[i - 1] else ""))
    Assert.Equal(e.Length, a.Length)

[<Fact>]
let ``every sample card renders the same tree as the old MessageCard`` () =
    Headless.run (fun () ->
        let cards, _ = Fixture.cards ()
        let font = FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body
        let sb = Text.StringBuilder()
        for name, message, flags in Samples.all do
            let card, _ = cards.Render(message, Fixture.ctxOf flags, Fixture.actions, flags.Index)
            sb.Append("== ").Append(name).Append('\n').Append(Dump.tree font card) |> ignore
        for name, error, cont in Samples.errors do
            let card = cards.ErrorCard(error, ignore, Fixture.actions, (if cont then Some ignore else None))
            sb.Append("== ").Append(name).Append('\n').Append(Dump.tree font card) |> ignore
        same (golden "cards.txt") (sb.ToString()))
