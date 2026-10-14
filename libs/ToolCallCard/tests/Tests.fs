module Wanxiang2.ToolCallCard.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.ToolCallCard
open Wanxiang2.ToolCallCard.Tests

let private look =
    { Ink = (function ToolInk.Warning -> Brushes.Orange :> IBrush | ToolInk.Danger -> Brushes.Red | ToolInk.Accent -> Brushes.Blue | _ -> Brushes.Gray)
      Gauge = (fun _ -> Cell 4.0)
      BodyFont = Cell(FontFamily "Body")
      MonoFont = Cell(FontFamily "Mono")
      Glyph = fun g b -> Border(Tag = g, Background = b)
      Dress = fun b -> b.Classes.Add "surface"
      TechLineHeight = fun s -> s * 1.52
      Reduced = Cell true }

let private copies = ResizeArray<string * string>()
let private revealed = ResizeArray<bool>()
let private parts =
    { Copy = fun tip name _ -> copies.Add(tip, name); Border(Tag = tip) :> Control
      Reveal = fun c v -> (Border(Child = c, IsVisible = v) :> Control), (fun b -> revealed.Add b) }

let private data tone err =
    { CallName = "builtin_echo"; DisplayName = "回显（调试）"; Tone = tone; Badge = "等待批准"
      ArgCount = 1; Preview = "text: hi"; HumanSummary = "回显 hi"; Error = err
      Sections = ToolCallCard.sections id "{\"text\":\"hi\"}" None tone
      RawPayload = ToolCallCard.rawPayload "{\"text\":\"hi\"}" None tone }

[<Fact>]
let ``等审批用警示色和信息图标，不出对勾`` () =
    Headless.run (fun () ->
        let c = ToolCallCard(look, data ToolTone.Awaiting None, parts)
        let dock = (c.Header.Child :?> StackPanel).Children[0] :?> DockPanel
        let left = dock.Children[0] :?> StackPanel
        Assert.Equal(box ToolGlyph.Wrench, left.Children[0].Tag)
        let pill = left.Children[2] :?> Border
        let pillRow = pill.Child :?> StackPanel
        Assert.Equal(box ToolGlyph.Info, pillRow.Children[0].Tag)
        Assert.Same(Brushes.Orange, (pillRow.Children[1] :?> TextBlock).Foreground)
        Assert.Equal("调用工具 · builtin_echo（等待批准）", AutomationProperties.GetItemStatus c.Header))

[<Fact>]
let ``头部开合详情，摘要让位，读屏名跟着变`` () =
    Headless.run (fun () ->
        revealed.Clear()
        let c = ToolCallCard(look, data ToolTone.Success None, parts)
        Assert.Equal("展开工具调用 builtin_echo", AutomationProperties.GetName c.Header)
        Assert.True c.Summary.IsVisible
        c.Toggle()
        Assert.True c.Expanded
        Assert.False c.Summary.IsVisible
        Assert.Equal<bool list>([ true ], List.ofSeq revealed)
        Assert.Equal("收起工具调用 builtin_echo", AutomationProperties.GetName c.Header)
        Assert.Equal("工具调用 builtin_echo", AutomationProperties.GetName c.Host)
        Assert.Equal("tool-human-summary", AutomationProperties.GetAutomationId((c.Header.Child :?> StackPanel).Children[1])))

[<Fact>]
let ``失败出横幅，结果一节标异常`` () =
    Headless.run (fun () ->
        let d = { data ToolTone.Failed (Some "boom") with Sections = ToolCallCard.sections id "{}" (Some "err") ToolTone.Failed }
        let c = ToolCallCard(look, d, parts)
        Assert.True c.Banner.IsVisible
        Assert.Equal(2, c.Details.Children.Count)
        let header = (c.Details.Children[1] :?> StackPanel).Children[0] :?> DockPanel
        Assert.Equal("执行结果 (异常)", (header.Children[0] :?> TextBlock).Text))

[<Fact>]
let ``状态未知时补一节说明没收到结果`` () =
    let s = ToolCallCard.sections id "" None ToolTone.Unknown
    Assert.Equal(1, s.Length)
    Assert.Equal("未收到工具执行结果（生成已结束）", s.Head.Raw)
    Assert.Equal("// 结果 (Result)\n未收到工具执行结果（生成已结束）", ToolCallCard.rawPayload "" None ToolTone.Unknown)
    Assert.Equal("// 参数 (Arguments)\n{}\n\n// 结果 (Result)\nok", ToolCallCard.rawPayload "{}" (Some "ok") ToolTone.Success)

[<Fact>]
let ``复制按钮文案照旧`` () =
    Headless.run (fun () ->
        copies.Clear()
        ToolCallCard(look, data ToolTone.Success None, parts) |> ignore
        Assert.Contains(("复制调用参数", "复制工具调用参数"), copies)
        Assert.Contains(("复制工具参数与结果", "复制工具 builtin_echo 参数与结果"), copies))
