module Wanxiang2.ReadingLook.Tests.Tests

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.CodeTint
open Wanxiang2.MarkdownView
open Wanxiang2.ReadingLook

let private golden name = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", name))

let private noTint () =
    CodeTint { HighlightHtml = (fun _ -> None); Background = (fun f -> f ()); After = (fun _ f -> f ()); Synchronous = true }

/// 一套真实的主题管理者 + 样式，装进应用样式（每个测试会话只装一次也无害）
let private themed () =
    let manager = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
    let styles = ThemeStyles manager
    styles.Install Application.Current.Styles
    manager, styles

let private view (manager, styles) (math: string * bool -> string option) =
    MarkdownView(
        ReadingLook.markdown manager styles,
        { CopyText = ignore; OpenLink = ignore; MathHtml = math; Tint = noTint (); Wrap = CodeWrapMemory false },
        14.5,
        false)

/// 逐行比对，失败时指出第一处不同（整段 Assert.Equal 输出太长难读）
let private same (expected: string) (actual: string) =
    let e = expected.Split('\n')
    let a = actual.Split('\n')
    for i in 0 .. min e.Length a.Length - 1 do
        if e.[i] <> a.[i] then
            Assert.Fail(sprintf "line %d\nold: %s\nnew: %s\n(prev: %s)" (i + 1) e.[i] a.[i] (if i > 0 then e.[i - 1] else ""))
    Assert.Equal(e.Length, a.Length)

[<Fact>]
let ``every sample renders the same tree as the old MarkdownRenderer`` () =
    Headless.run (fun () ->
        let theme = themed ()
        let table = MathHtml.table (Path.Combine(__SOURCE_DIRECTORY__, "golden", "figure-html.txt"))
        let sb = Text.StringBuilder()
        for label, math in [ "plain", (fun _ -> None); "rich", (fun key -> Map.tryFind key table) ] do
            for name, raw in Samples.all do
                sb.Append("== ").Append(label).Append(' ').Append(name).Append('\n').Append(Dump.tree (FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body) ((view theme math).RenderText raw)) |> ignore
        same (golden "markdown.txt") (sb.ToString()))

let rec private descendants (c: Control) : Control seq =
    seq {
        yield c
        for child in Avalonia.LogicalTree.LogicalExtensions.GetLogicalChildren c do
            match child with
            | :? Control as cc -> yield! descendants cc
            | _ -> ()
    }

[<Fact>]
let ``theme switch recolors shown code in place and metric edits reach shown blocks`` () =
    Headless.run (fun () ->
        let manager, styles = themed ()
        let control = (view (manager, styles) (fun _ -> None)).RenderText "```fsharp\nlet x = 1\n```"
        let win = Window(Width = 600.0, Height = 400.0, Content = control)
        win.Show()
        let card = (control :?> StackPanel).Children[0] :?> Border
        let code = descendants control |> Seq.pick (function :? SelectableTextBlock as s -> Some s | _ -> None)
        let light = (card.Background :?> ISolidColorBrush).Color
        manager.Apply PaletteMode.Dark
        manager.CompleteTransition()
        Assert.Equal(ThemePalette.color PaletteMode.Dark PaletteKey.CodeBg, (card.Background :?> ISolidColorBrush).Color)
        Assert.NotEqual(light, (card.Background :?> ISolidColorBrush).Color)
        manager.SetMetric(Wanxiang2.ThemeMetrics.Metric.BlockPaddingX, 21.0)
        Assert.Equal(21.0, code.Margin.Left)
        win.Close())

[<Fact>]
let ``code header buttons wear the shared icon button classes from the theme`` () =
    Headless.run (fun () ->
        let manager, styles = themed ()
        let control = (view (manager, styles) (fun _ -> None)).RenderText "```\nx\n```"
        let win = Window(Width = 600.0, Height = 400.0, Content = control)
        win.Show()
        let copy = descendants control |> Seq.find (fun c -> (match ToolTip.GetTip c with :? string as t -> t = "复制代码" | _ -> false)) :?> Border
        Assert.Contains(StyleClass.iconButton, copy.Classes)
        Assert.Contains(StyleClass.surface, copy.Classes)
        // 命中区按布局规则取方形（本地值），圆角来自主题样式
        Assert.Equal(30.0, copy.Width)
        manager.SetSpace(Space.RadiusMd, 5.0)
        Assert.Equal(CornerRadius 5.0, copy.CornerRadius)
        win.Close())
