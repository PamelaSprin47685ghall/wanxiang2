module Wanxiang2.UsageFoot.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Media
open Xunit
open Wanxiang2.UsageFoot
open Wanxiang2.UsageFoot.Tests

let private look =
    { Ink = (function FootInk.Text -> Brushes.Black :> IBrush | FootInk.TextMuted -> Brushes.Gray | FootInk.TextFaint -> Brushes.Silver)
      Gauge = (fun _ -> Cell 11.0)
      MonoFont = Cell(FontFamily "Mono") }

let private empty = { Prompt = None; Completion = None; Total = None; DurationMs = None; Cached = None; Detail = "d" }

let private text (c: Control) =
    (c :?> TextBlock).Inlines |> Seq.map (fun i -> (i :?> Run).Text) |> String.concat ""

[<Fact>]
let ``入出齐全且合计一致时不重复总数`` () =
    Headless.run (fun () ->
        let c = (UsageFoot.usage look { empty with Prompt = Some 1200; Completion = Some 30; DurationMs = Some 2500L; Cached = Some 800 }).Value
        Assert.Equal("入 1,200 · 出 30 · 2.5s · 缓存 800", text c)
        Assert.Equal("Token 使用情况：输入 1,200，输出 30，总计 1,230", AutomationProperties.GetName c))

[<Fact>]
let ``合计与明细不一致时补总数，只有合计时写 tokens`` () =
    Headless.run (fun () ->
        Assert.Equal("入 1 · 出 2 · 总 9", text (UsageFoot.usage look { empty with Prompt = Some 1; Completion = Some 2; Total = Some 9 }).Value)
        Assert.Equal("tokens 9 · 800ms", text (UsageFoot.usage look { empty with Total = Some 9; DurationMs = Some 800L }).Value)
        Assert.True((UsageFoot.usage look empty).IsNone))

[<Fact>]
let ``悬停只提亮数字`` () =
    Headless.run (fun () ->
        let c = (UsageFoot.usage look { empty with Prompt = Some 1 }).Value :?> TextBlock
        c.RaiseEvent(Avalonia.Input.PointerEventArgs(Avalonia.Input.InputElement.PointerEnteredEvent, c, new Avalonia.Input.Pointer(1, Avalonia.Input.PointerType.Mouse, true), c, Avalonia.Point(), 0UL, Avalonia.Input.PointerPointProperties.None, Avalonia.Input.KeyModifiers.None))
        let runs = c.Inlines |> Seq.cast<Run> |> List.ofSeq
        Assert.Same(Brushes.Silver, runs[0].Foreground)
        Assert.Same(Brushes.Black, runs[1].Foreground))

[<Fact>]
let ``脚注：时间悬停给完整时间，两者合一行`` () =
    Headless.run (fun () ->
        let stamp = Some { Short = "14:20"; Full = "2026-10-14 14:20:05" }
        let only = (UsageFoot.line look stamp None true).Value :?> TextBlock
        Assert.Equal("2026-10-14 14:20:05", AutomationProperties.GetName only)
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Right, only.HorizontalAlignment)
        let both = (UsageFoot.line look stamp (Some { empty with Prompt = Some 1 }) false).Value :?> StackPanel
        Assert.Equal(3, both.Children.Count)
        Assert.True((UsageFoot.line look None (Some empty) false).IsNone))
