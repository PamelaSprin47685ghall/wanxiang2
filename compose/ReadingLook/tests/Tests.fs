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
    // 合并活字典本体：正文控件的 DynamicResource 绑定要沿树查到主题资源（W7 外观退役）
    if not (Application.Current.Resources.MergedDictionaries.Contains (manager.Resources :> Avalonia.Controls.IResourceDictionary)) then
        Application.Current.Resources.MergedDictionaries.Add (manager.Resources :> Avalonia.Controls.IResourceDictionary)
    manager, styles

let private view (manager, styles) (math: string * bool -> string option) =
    MarkdownView(
        ReadingLook.markdown manager styles,
        { CopyText = ignore; OpenLink = ignore; MathHtml = math; Tint = noTint (); Wrap = CodeWrapMemory false },
        14.5,
        false)

let rec private descendants (c: Control) : Control seq =
    seq {
        yield c
        for child in Avalonia.LogicalTree.LogicalExtensions.GetLogicalChildren c do
            match child with
            | :? Control as cc -> yield! descendants cc
            | _ -> ()
    }

[<Fact>]
let ``markdown interactive links produce accessible actionable buttons`` () =
    Headless.run (fun () ->
        let theme = themed ()
        let mutable opened = None
        let view =
            MarkdownView(
                ReadingLook.markdown (fst theme) (snd theme),
                { CopyText = ignore; OpenLink = (fun url -> opened <- Some url); MathHtml = (fun _ -> None); Tint = noTint (); Wrap = CodeWrapMemory false },
                14.5,
                false)
        let control = view.RenderText "请见 [文档](https://example.com/docs)"
        let link = descendants control |> Seq.find (function :? Button as b -> b.Classes.Contains StyleClass.surface || Avalonia.Automation.AutomationProperties.GetName b = "文档" | _ -> false) :?> Button
        Assert.True link.Focusable
        Assert.Equal("文档", Avalonia.Automation.AutomationProperties.GetName link)
        Assert.Equal("https://example.com/docs", Avalonia.Automation.AutomationProperties.GetHelpText link)
        Assert.Equal("https://example.com/docs", ToolTip.GetTip link :?> string)
        link.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, link))
        Assert.Equal(Some "https://example.com/docs", opened))

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
        manager.ApplyMetric(Wanxiang2.ThemeMetrics.Metric.BlockPaddingX, 21.0)
        Assert.Equal(21.0, code.Margin.Left)
        win.Close())

[<Fact>]
let ``code header buttons wear the shared icon button classes from the theme`` () =
    Headless.run (fun () ->
        let manager, styles = themed ()
        let control = (view (manager, styles) (fun _ -> None)).RenderText "```\nx\n```"
        let win = Window(Width = 600.0, Height = 400.0, Content = control)
        win.Show()
        let copy = descendants control |> Seq.find (fun c -> (match ToolTip.GetTip c with :? string as t -> t = "复制代码" | _ -> false)) :?> Button
        Assert.Contains(StyleClass.iconButton, copy.Classes)
        Assert.Contains(StyleClass.surface, copy.Classes)
        // 命中区按布局规则取方形（本地值），圆角来自主题样式
        Assert.Equal(30.0, copy.Width)
        manager.ApplySpace(Space.RadiusMd, 5.0)
        Assert.Equal(CornerRadius 5.0, copy.CornerRadius)
        win.Close())
