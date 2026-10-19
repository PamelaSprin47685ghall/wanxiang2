module Wanxiang2.MarkdownView.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Avalonia.LogicalTree
open Xunit
open Wanxiang.UI
open Wanxiang2.CodeTint
open Wanxiang2.MarkdownView

/// 测试外观：每个槽位一支独立画笔、一个可改的活值；图标用带底色的 Border 代替（记下画笔）。
type private Kit() =
    let brushes = Collections.Generic.Dictionary<MarkdownInk, SolidColorBrush>()
    let gauges = Collections.Generic.Dictionary<MarkdownGauge, Cell<float>>()
    let reduced = Cell false
    let opacity = Cell 0.6
    member _.Brush k =
        match brushes.TryGetValue k with
        | true, b -> b
        | _ ->
            let b = SolidColorBrush(Color.FromUInt32(0xff000000u ||| uint32 (brushes.Count + 1)))
            brushes[k] <- b
            b
    member _.Gauge k =
        match gauges.TryGetValue k with
        | true, c -> c
        | _ ->
            let c = Cell(match k with MarkdownGauge.CopyFadeMs -> 150.0 | _ -> 4.0)
            gauges[k] <- c
            c
    member _.Reduced = reduced
    member _.FadeOpacity = opacity
    member this.Look : MarkdownMetrics =
        { Ink = fun k -> this.Brush k :> IBrush
          Gauge = fun k -> this.Gauge k :> IObservable<float>
          BodyFont = Cell(FontFamily.Default)
          MonoFont = Cell(FontFamily "monospace")
          Glyph = fun g brush -> Border(Background = brush, Tag = g) :> Control
          Dress = fun role b -> b.Classes.Add(match role with MarkdownButton.Icon -> "icon" | MarkdownButton.Ghost -> "ghost")
          Reduced = reduced
          Copy =
            { FadeOpacity = opacity
              Hold = Cell(TimeSpan.FromSeconds 1.5)
              Fade = Cell(TimeSpan.FromMilliseconds 150.0) }
          Math =
            { Ink = this.Brush MarkdownInk.Text
              BodyFont = Cell(FontFamily.Default)
              BlockPadding = Cell 8.0 } }

let private tint () =
    CodeTint { HighlightHtml = (fun _ -> None); Background = (fun f -> f ()); After = (fun _ f -> f ()); Synchronous = true }

let private make (kit: Kit) (copy: string -> unit) (openLink: string -> unit) (wrap: CodeWrapMemory) =
    MarkdownView(kit.Look, { CopyText = copy; OpenLink = openLink; MathHtml = (fun _ -> None); Tint = tint (); Wrap = wrap }, 14.5, false)

let rec private descendants (c: Control) : Control seq =
    seq {
        yield c
        for child in c.GetLogicalChildren() do
            match child with
            | :? Control as cc -> yield! descendants cc
            | _ -> ()
        match c with
        | :? TextBlock as t when not (isNull t.Inlines) ->
            for i in t.Inlines do
                match i with
                | :? Documents.InlineUIContainer as u -> yield! descendants u.Child
                | _ -> ()
        | _ -> ()
    }

let private show (c: Control) =
    let w = Window(Width = 600.0, Height = 400.0, Content = c)
    w.Show()
    Dispatcher.UIThread.RunJobs()
    w

let private byTip tip (root: Control) =
    descendants root |> Seq.find (fun c -> match ToolTip.GetTip c with :? string as t -> t = tip | _ -> false)

let private invoke (c: Control) =
    ((ControlAutomationPeer.CreatePeerForElement c :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

let private key (c: Control) k = c.RaiseEvent(KeyEventArgs(Key = k, RoutedEvent = InputElement.KeyDownEvent))

[<Fact>]
let ``MarkdownRenderer wide table (>4 columns) wraps in horizontal scroll viewer`` () =
    Headless.run (fun () ->
        let view = make (Kit()) ignore ignore (CodeWrapMemory false)
        let wide = view.RenderText "| A | B | C | D | E | F |\n|---|---|---|---|---|---|\n| 1 | 2 | 3 | 4 | 5 | 6 |"
        let sv = descendants wide |> Seq.pick (function :? ScrollViewer as s -> Some s | _ -> None)
        Assert.Equal(ScrollBarVisibility.Auto, sv.HorizontalScrollBarVisibility)
        Assert.Equal(ScrollBarVisibility.Disabled, sv.VerticalScrollBarVisibility)
        let grid = Assert.IsAssignableFrom<Grid>(sv.Content)
        Assert.Equal(6, grid.ColumnDefinitions.Count)
        for col in grid.ColumnDefinitions do Assert.Equal(120.0, col.MinWidth)
        let narrow = view.RenderText "| Col1 | Col2 |\n|------|------|\n| val1 | val2 |"
        Assert.Empty(descendants narrow |> Seq.filter (fun c -> c :? ScrollViewer)))

[<Fact>]
let ``Markdown hyperlink responds to Enter and Space key to openLink`` () =
    Headless.run (fun () ->
        let opened = ResizeArray<string>()
        let view = make (Kit()) ignore opened.Add (CodeWrapMemory false)
        let rendered = view.Render [ MdParagraph [ MdText("Click this ", false, false, false, false); MdLink("example link", "https://wanxiang.ai") ] ]
        let w = show rendered
        let link = descendants rendered |> Seq.find (fun c -> AutomationProperties.GetName c = "example link")
        Assert.True link.Focusable
        key link Key.Enter
        key link Key.Space
        invoke link
        // 新实现每次激活只打开一次（旧实现键盘激活会触发两次：链接自己的 KeyDown 与 onClick 的 KeyDown 各开一次）
        Assert.Equal<string list>([ "https://wanxiang.ai"; "https://wanxiang.ai"; "https://wanxiang.ai" ], List.ofSeq opened)
        w.Close())

[<Fact>]
let ``long links split into chunks with one tab stop and one announced name`` () =
    Headless.run (fun () ->
        let view = make (Kit()) ignore ignore (CodeWrapMemory false)
        let text = String('a', 95)
        let rendered = view.Render [ MdParagraph [ MdLink(text, "https://x.y") ] ]
        let chunks = descendants rendered |> Seq.filter (fun c -> c :? ActionSpot) |> List.ofSeq
        Assert.True(chunks.Length >= 3)
        Assert.Equal(1, chunks |> List.filter (fun c -> c.Focusable) |> List.length)
        Assert.Equal(text, AutomationProperties.GetName chunks.Head)
        for c in chunks.Tail do Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView c))

[<Fact>]
let ``Markdown code block copy button confirms visually with check icon and tooltip`` () =
    Headless.run (fun () ->
        let kit = Kit()
        let mutable copied = ""
        let view = make kit (fun t -> copied <- t) ignore (CodeWrapMemory false)
        let controls = view.RenderText "```fsharp\nlet x = 42\n```"
        let w = show controls
        let button = byTip "复制代码" controls :?> Border
        Assert.True button.Focusable
        Assert.Equal("复制fsharp代码", AutomationProperties.GetName button)
        invoke button
        Assert.Equal("let x = 42", copied.Trim())
        Assert.Equal("已复制", AutomationProperties.GetName button)
        Assert.Equal(box "已复制", ToolTip.GetTip button)
        // 勾号用成功色
        Assert.Same(kit.Brush MarkdownInk.Success, (button.Child :?> Border).Background)
        w.Close())

[<Fact>]
let ``code block copy confirmation dips toward fade opacity and keeps the confirmed label`` () =
    Headless.run (fun () ->
        let run reduced =
            let kit = Kit()
            kit.Reduced.Set reduced
            let view = make kit ignore ignore (CodeWrapMemory false)
            let controls = view.RenderText "```fsharp\nlet x = 42\n```"
            let w = show controls
            let button = byTip "复制代码" controls
            invoke button
            // 过渡在跑时 Opacity 读到的是补间中的值，目标值看本地基值
            Assert.InRange(button.Opacity, 0.6 - 0.001, 1.001)
            let opacity = button.GetBaseValue(Visual.OpacityProperty).GetValueOrDefault(1.0)
            let fade =
                button.Transitions |> Seq.pick (function :? Animation.DoubleTransition as d when d.Property = Visual.OpacityProperty -> Some d.Duration | _ -> None)
            w.Close()
            opacity, fade
        let normal, fade = run false
        Assert.Equal(0.6, normal, 3)
        Assert.Equal(TimeSpan.FromMilliseconds 150.0, fade)
        let reduced, reducedFade = run true
        Assert.Equal(1.0, reduced, 3)
        Assert.Equal(TimeSpan.Zero, reducedFade))

[<Fact>]
let ``wrap toggle flips the block and is remembered for later blocks`` () =
    Headless.run (fun () ->
        let kit = Kit()
        let memory = CodeWrapMemory false
        let view = make kit ignore ignore memory
        let first = view.RenderText "```\nx\n```"
        let w = show first
        let toggle = byTip "长行折行 / 横向滚动" first :?> Border
        Assert.Equal("长行折行", AutomationProperties.GetName toggle)
        invoke toggle
        Assert.True memory.Wrapped
        Assert.Equal("取消折行", AutomationProperties.GetName toggle)
        Assert.Same(kit.Brush MarkdownInk.Accent, (toggle.Child :?> Border).Background)
        let code = descendants first |> Seq.pick (function :? SelectableTextBlock as s when s.Text = null || s.Inlines.Count > 0 -> Some s | _ -> None)
        Assert.Equal(TextWrapping.Wrap, code.TextWrapping)
        let second = view.RenderText "```\ny\n```"
        Assert.Equal("取消折行", AutomationProperties.GetName (byTip "长行折行 / 横向滚动" second))
        w.Close())

[<Fact>]
let ``huge code block expand button reports its expanded state`` () =
    Headless.run (fun () ->
        let view = make (Kit()) ignore ignore (CodeWrapMemory false)
        let lines = [ for i in 1 .. 130 -> sprintf "let value%d = %d" i i ] |> String.concat "\n"
        let control = view.RenderText(sprintf "```fsharp\n%s\n```" lines)
        let expand =
            descendants control
            |> Seq.pick (function
                | :? Border as b -> (match b.Child with :? TextBlock as t when t.Text = "展开全部" -> Some b | _ -> None)
                | _ -> None)
        let scroll = descendants control |> Seq.pick (function :? ScrollViewer as s -> Some s | _ -> None)
        Assert.Equal(LayoutPolicy.expandedDetailMaxHeight, scroll.MaxHeight)
        Assert.Equal("已折叠", AutomationProperties.GetItemStatus expand)
        key expand Key.Enter
        Assert.Equal("收起", (expand.Child :?> TextBlock).Text)
        Assert.Equal("已展开", AutomationProperties.GetItemStatus expand)
        Assert.True(Double.IsPositiveInfinity scroll.MaxHeight)
        key expand Key.Enter
        Assert.Equal("展开全部", (expand.Child :?> TextBlock).Text)
        Assert.Equal("已折叠", AutomationProperties.GetItemStatus expand))

[<Fact>]
let ``MarkdownRenderer task boxes expose checkbox automation state`` () =
    Headless.run (fun () ->
        let view = make (Kit()) ignore ignore (CodeWrapMemory false)
        let control = view.RenderText "- [x] 已完成的一步\n- [ ] 还没做的一步"
        let names = descendants control |> Seq.map AutomationProperties.GetName |> Seq.filter (String.IsNullOrEmpty >> not) |> List.ofSeq
        Assert.Contains("已完成任务", names)
        Assert.Contains("未完成任务", names)
        let roles = descendants control |> Seq.choose (fun c -> let o = AutomationProperties.GetControlTypeOverride c in if o.HasValue then Some o.Value else None)
        Assert.Contains(AutomationControlType.CheckBox, roles))

[<Fact>]
let ``markdown renderer renders complex markdown with code, tables and math without error`` () =
    Headless.run (fun () ->
        let view = MarkdownView((Kit()).Look, { CopyText = ignore; OpenLink = ignore; MathHtml = (fun _ -> None); Tint = tint (); Wrap = CodeWrapMemory false }, 14.0, true)
        let doc =
            "# 标题一\n正文包含 **粗体**、*斜体*、`行内代码` 和 [链接](https://example.com)。\n\n> 引用块测试\n\n- [x] 任务完成\n- [ ] 任务待办\n\n| 列 A | 列 B |\n|---|---|\n| 1 | 2 |\n\n```fsharp\nlet x = 42\n```\n\n$$E = mc^2$$\n"
        let w = show (view.RenderText doc)
        w.Close())

[<Fact>]
let ``live gauges follow while shown and stop after removal`` () =
    Headless.run (fun () ->
        let kit = Kit()
        let view = make kit ignore ignore (CodeWrapMemory false)
        let control = view.RenderText "```\nx\n```"
        let root = control :?> StackPanel
        let card = root.Children[0] :?> Border
        let w = show control
        kit.Gauge(MarkdownGauge.RadiusMd).Set 11.0
        Assert.Equal(CornerRadius 11.0, card.CornerRadius)
        let before = kit.Gauge(MarkdownGauge.RadiusMd).Count
        w.Content <- null
        Assert.True(kit.Gauge(MarkdownGauge.RadiusMd).Count < before)
        kit.Gauge(MarkdownGauge.RadiusMd).Set 2.0
        Assert.Equal(CornerRadius 11.0, card.CornerRadius)
        Assert.Equal(0, kit.Gauge(MarkdownGauge.RadiusMd).Count)
        w.Close())

[<Fact>]
let ``last block bottom margin is cleared and quote edges are tight`` () =
    Headless.run (fun () ->
        let view = make (Kit()) ignore ignore (CodeWrapMemory false)
        let root = view.RenderText "a\n\n> b\n>\n> c" :?> StackPanel
        let w = show root
        Assert.Equal(Thickness(0.0, 0.0, 0.0, ReadingRhythm.paragraphGap), root.Children[0].Margin)
        Assert.Equal(0.0, root.Children[1].Margin.Bottom)
        let inner = ((root.Children[1] :?> Border).Child :?> StackPanel).Children
        Assert.Equal(0.0, inner[0].Margin.Top)
        Assert.Equal(0.0, inner[inner.Count - 1].Margin.Bottom)
        w.Close())

[<Fact>]
let ``stream keeps finished blocks mounted and matches a full render`` () =
    Headless.run (fun () ->
        let view = make (Kit()) ignore ignore (CodeWrapMemory false)
        let stream = MarkdownStream view
        let w = show stream.Panel
        let panel = stream.Panel :?> StackPanel
        stream.Update "第一段\n\n第二"
        let first = panel.Children[0]
        Assert.Equal(2, stream.BlockCount)
        Assert.Equal(0.0, panel.Children[1].Margin.Bottom)
        stream.Update "第一段\n\n第二段继续\n\n```\ncode"
        Assert.Same(first, panel.Children[0])
        Assert.Equal(3, stream.BlockCount)
        // 原末块的底边距恢复，新末块清零
        Assert.Equal(ReadingRhythm.paragraphGap, panel.Children[1].Margin.Bottom)
        Assert.Equal(0.0, panel.Children[2].Margin.Bottom)
        // 同一原文不重排
        let third = panel.Children[2]
        stream.Update "第一段\n\n第二段继续\n\n```\ncode"
        Assert.Same(third, panel.Children[2])
        let full = view.RenderText "第一段\n\n第二段继续\n\n```\ncode" :?> StackPanel
        Assert.Equal(full.Children.Count, panel.Children.Count)
        for i in 0 .. full.Children.Count - 1 do
            Assert.Equal(full.Children[i].GetType(), panel.Children[i].GetType())
            Assert.Equal(full.Children[i].Margin, panel.Children[i].Margin)
        w.Close())

[<Fact>]
let ``inline math without html falls back to italic mono source`` () =
    Headless.run (fun () ->
        let kit = Kit()
        let view = make kit ignore ignore (CodeWrapMemory false)
        let block = view.RenderText "a $x^2$ b" :?> StackPanel
        let text = block.Children[0] :?> SelectableTextBlock
        let run = text.Inlines |> Seq.pick (function :? Documents.Run as r when r.Text = "x^2" -> Some r | _ -> None)
        Assert.Equal(FontStyle.Italic, run.FontStyle)
        Assert.Same(kit.Brush MarkdownInk.InlineCode, run.Background)
        Assert.Equal("monospace", run.FontFamily.Name))

[<Fact>]
let ``every clickable in markdown exposes Invoke as a button and activates`` () =
    Headless.run (fun () ->
        let kit = Kit()
        let mutable opened = ""
        let longCode = String.replicate 130 "x\n"
        let view = make kit ignore (fun u -> opened <- u) (CodeWrapMemory false)
        let controls = view.RenderText("[站点](https://example.com)\n\n```\n" + longCode + "```")
        let w = show controls
        let spots = descendants controls |> Seq.filter (fun c -> c :? ActionSpot) |> List.ofSeq
        Assert.NotEmpty spots
        for s in spots do
            let peer = ControlAutomationPeer.CreatePeerForElement s
            // 链接保持原 Hyperlink 角色，其余为按钮
            Assert.Contains(peer.GetAutomationControlType(), [ AutomationControlType.Button; AutomationControlType.Hyperlink ])
            Assert.IsAssignableFrom<IInvokeProvider>(peer) |> ignore
        let link = spots |> List.find (fun s -> AutomationProperties.GetName s = "站点")
        invoke link
        Assert.Equal("https://example.com", opened)
        let expand = spots |> List.find (fun s -> AutomationProperties.GetName s = "展开全部")
        invoke expand
        Assert.Equal("收起", AutomationProperties.GetName expand)
        w.Close())
