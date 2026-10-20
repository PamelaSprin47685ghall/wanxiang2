module Wanxiang2.MarkdownView.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.LogicalTree
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang.UI
open Wanxiang2.CodeTint
open Wanxiang2.MarkdownView

/// 主题资源字典的替身：正文几何与字体全部来自资源键。
let seedResources () =
    let r = Application.Current.Resources
    r["Margin.Block"] <- box (Thickness(4.0, 4.0))
    r["Padding.Block"] <- box (Thickness(4.0, 4.0))
    r["Thickness.BorderThin"] <- box (Thickness 4.0)
    r["Thickness.BorderBottom"] <- box (Thickness(0.0, 0.0, 4.0, 4.0))
    r["Thickness.None"] <- box (Thickness 0.0)
    r["Thickness.TaskBoxEdge"] <- box (Thickness 4.0)
    r["Thickness.QuoteEdge"] <- box (Thickness(4.0, 0.0, 0.0, 0.0))
    r["Thickness.GapRightMd"] <- box (Thickness(0.0, 0.0, 4.0, 0.0))
    r["Margin.GapRightMd"] <- box (Thickness(0.0, 0.0, 4.0, 0.0))
    r["Margin.MathInline"] <- box (Thickness(4.0, 0.0))
    r["Margin.CodeExpand"] <- box (Thickness(4.0, 4.0, 0.0, 4.0))
    r["Margin.QuoteNested"] <- box (Thickness(4.0, 0.0, 0.0, 4.0))
    r["Padding.Quote"] <- box (Thickness(4.0, 4.0))
    r["Padding.CodeHeader"] <- box (Thickness(4.0, 4.0, 4.0, 4.0))
    r["Padding.MathBlock"] <- box (Thickness(0.0, 4.0))
    r["CornerRadius.RadiusXs"] <- box (CornerRadius 4.0)
    r["CornerRadius.RadiusMd"] <- box (CornerRadius 4.0)
    r["CornerRadius.QuoteRight"] <- box (CornerRadius(0.0, 4.0, 4.0, 0.0))
    r["CornerRadius.ListMarkerDot"] <- box (CornerRadius 4.0)
    r["Metric.FontMicro"] <- box 4.0
    r["Metric.BorderWidth"] <- box 4.0
    r["Metric.ListMarkerDotSize"] <- box 4.0
    r["Metric.TaskBoxSize"] <- box 4.0
    r["Metric.TaskCheckGlyphSize"] <- box 4.0
    r["Tracking.Emphasis"] <- box 4.0
    r["Space.XXs"] <- box 4.0
    r["FontFamily.Mono"] <- box (FontFamily "monospace")
    r["FontFamily.Body"] <- box FontFamily.Default
    r["Motion.Reduced"] <- box false

/// 测试外观：每个槽位一支独立画笔；图标用带底色的 Border 代替（记下画笔）。
type private Kit() =
    let brushes = Collections.Generic.Dictionary<MarkdownInk, SolidColorBrush>()
    member _.Brush k =
        match brushes.TryGetValue k with
        | true, b -> b
        | _ ->
            let b = SolidColorBrush(Color.FromUInt32(0xff000000u ||| uint32 (brushes.Count + 1)))
            brushes[k] <- b
            b
    member this.Look : MarkdownMetrics =
        { Ink = fun k -> this.Brush k :> IBrush
          Glyph = fun g brush -> Border(Background = brush, Tag = g) :> Control
          Dress = fun role b -> b.Classes.Add(match role with MarkdownButton.Icon -> "icon" | MarkdownButton.Ghost -> "ghost")
          Copy =
            { FadeOpacity = 0.6
              Hold = TimeSpan.FromSeconds 1.5
              Fade = TimeSpan.FromMilliseconds 150.0 }
          Math = { Ink = this.Brush MarkdownInk.Text } }

let private tint () =
    CodeTint { HighlightHtml = (fun _ -> None); Background = (fun f -> f ()); After = (fun _ f -> f ()); Synchronous = true }

let private make (kit: Kit) (copy: string -> unit) (openLink: string -> unit) (wrap: CodeWrapMemory) =
    seedResources ()
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
        let button = byTip "复制代码" controls :?> Button
        Assert.True button.Focusable
        Assert.Equal("复制fsharp代码", AutomationProperties.GetName button)
        invoke button
        Assert.Equal("let x = 42", copied.Trim())
        Assert.Equal("已复制", AutomationProperties.GetName button)
        Assert.Equal(box "已复制", ToolTip.GetTip button)
        // 勾号用成功色
        Assert.Same(kit.Brush MarkdownInk.Success, (button.Content :?> Border).Background)
        w.Close())

[<Fact>]
let ``code block copy confirmation dips toward fade opacity and keeps the confirmed label`` () =
    Headless.run (fun () ->
        let run reduced =
            let kit = Kit()
            let view = make kit ignore ignore (CodeWrapMemory false)
            let controls = view.RenderText "```fsharp\nlet x = 42\n```"
            let w = show controls
            // 减弱动效走资源键：激活时刻现读，无订阅
            Application.Current.Resources["Motion.Reduced"] <- box reduced
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
        // 减弱动效：激活时刻不淡出（Opacity 基值满格），补间本身仍在但不发生颜色差
        Assert.Equal(1.0, reduced, 3)
        Assert.Equal(TimeSpan.FromMilliseconds 150.0, reducedFade))

[<Fact>]
let ``wrap toggle flips the block and is remembered for later blocks`` () =
    Headless.run (fun () ->
        let kit = Kit()
        let memory = CodeWrapMemory false
        let view = make kit ignore ignore memory
        let first = view.RenderText "```\nx\n```"
        let w = show first
        let toggle = byTip "长行折行 / 横向滚动" first :?> Button
        Assert.Equal("长行折行", AutomationProperties.GetName toggle)
        invoke toggle
        Assert.True memory.Wrapped
        Assert.Equal("取消折行", AutomationProperties.GetName toggle)
        Assert.Same(kit.Brush MarkdownInk.Accent, (toggle.Content :?> Border).Background)
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
                | :? Button as b -> (match b.Content with :? TextBlock as t when t.Text = "展开全部" -> Some b | _ -> None)
                | _ -> None)
        let scroll = descendants control |> Seq.pick (function :? ScrollViewer as s -> Some s | _ -> None)
        Assert.Equal(LayoutPolicy.expandedDetailMaxHeight, scroll.MaxHeight)
        Assert.Equal("已折叠", AutomationProperties.GetItemStatus expand)
        key expand Key.Enter
        Assert.Equal("收起", (expand.Content :?> TextBlock).Text)
        Assert.Equal("已展开", AutomationProperties.GetItemStatus expand)
        Assert.True(Double.IsPositiveInfinity scroll.MaxHeight)
        key expand Key.Enter
        Assert.Equal("展开全部", (expand.Content :?> TextBlock).Text)
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
        seedResources ()
        let view = MarkdownView((Kit()).Look, { CopyText = ignore; OpenLink = ignore; MathHtml = (fun _ -> None); Tint = tint (); Wrap = CodeWrapMemory false }, 14.0, true)
        let doc =
            "# 标题一\n正文包含 **粗体**、*斜体*、`行内代码` 和 [链接](https://example.com)。\n\n> 引用块测试\n\n- [x] 任务完成\n- [ ] 任务待办\n\n| 列 A | 列 B |\n|---|---|\n| 1 | 2 |\n\n```fsharp\nlet x = 42\n```\n\n$$E = mc^2$$\n"
        let w = show (view.RenderText doc)
        w.Close())

[<Fact>]
let ``theme resource changes reach shown blocks without any manual wiring`` () =
    Headless.run (fun () ->
        let kit = Kit()
        let view = make kit ignore ignore (CodeWrapMemory false)
        let control = view.RenderText "```\nx\n```"
        let root = control :?> StackPanel
        let card = root.Children[0] :?> Border
        let w = show control
        Application.Current.Resources["CornerRadius.RadiusMd"] <- box (CornerRadius 11.0)
        Assert.Equal(CornerRadius 11.0, card.CornerRadius)
        // 代码壳描边与内衬同源：改 Thickness.BorderThin 即随动
        Application.Current.Resources["Thickness.BorderThin"] <- box (Thickness 2.0)
        Assert.Equal(Thickness 2.0, card.BorderThickness)
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
        // 资源查找要挂在可视树上：先上屏再读字体
        let w = show block
        let text = block.Children[0] :?> SelectableTextBlock
        let run = text.Inlines |> Seq.pick (function :? Documents.Run as r when r.Text = "x^2" -> Some r | _ -> None)
        Assert.Equal(FontStyle.Italic, run.FontStyle)
        Assert.Same(kit.Brush MarkdownInk.InlineCode, run.Background)
        Assert.Equal("monospace", run.FontFamily.Name)
        w.Close())

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
