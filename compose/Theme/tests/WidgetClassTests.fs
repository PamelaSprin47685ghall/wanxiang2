module Wanxiang2.Theme.Tests.WidgetClassTests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.VisualTree
open Xunit
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.PopupMenu
open Wanxiang2.ToastStrip

let manager () =
    let m = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
    m.Motion.ApplyReduced true
    m

let menuLook (m: ThemeManager) =
    let b k = m.Brush k :> IBrush

    {
        Text = b PaletteKey.Text
        Muted = b PaletteKey.TextMuted
        Faint = b PaletteKey.TextFaint
        Danger = b PaletteKey.Danger
        Accent = b PaletteKey.Accent
        Check = (fun br -> Border(Background = br))
        Chevron = (fun br -> Border(Background = br))
        IconSlotWidth = 16.0
        ItemMinHeight = 28.0
        MaxHeight = 300.0
    }

let toastLook (m: ThemeManager) =
    let b k = m.Brush k :> IBrush

    {
        Surface = b PaletteKey.SurfaceRaised
        Edge = b PaletteKey.Hairline
        Text = b PaletteKey.Text
        Accent = (fun _ -> b PaletteKey.TextMuted)
        AccentOpacity = 1.0
        Padding = Thickness 4.0
        Shadow = BoxShadows()
        LineHeight = 18.0
        ColumnSpacing = 8.0
        Spacing = 8.0
        Margin = Thickness 0.0
        MaxWidth = 400.0
        BodyMaxHeight = 100.0
        Fade = TimeSpan.Zero
        FadeEasing = EaseOutCubicEasing()
    }

let pseudo (c: Control) (name: string) (on: bool) =
    let pc: IPseudoClasses = c.Classes
    pc.Set(name, on)

let color (b: IBrush) = (b :?> SolidColorBrush).Color

let host (m: ThemeManager) (content: Control) =
    let win = Window(Width = 400.0, Height = 300.0, Content = content)
    (ThemeStyles m).Install win.Styles
    // 活字典合并进窗口资源：ToastStrip 等的 DynamicResource 绑定沿树取值
    if not (win.Resources.MergedDictionaries.Contains(m.Resources :> Avalonia.Controls.IResourceDictionary)) then
        win.Resources.MergedDictionaries.Add(m.Resources :> Avalonia.Controls.IResourceDictionary)

    win.Show()
    win

let named (root: Visual) name =
    root.GetVisualDescendants()
    |> Seq.choose (function
        | :? Control as c when Avalonia.Automation.AutomationProperties.GetName c = name -> Some c
        | _ -> None)
    |> Seq.head

[<Fact>]
let ``menu items hover and press through theme classes and follow a theme switch`` () =
    Headless.run (fun () ->
        let m = manager ()

        let menu =
            (PopupMenu.buildGrouped
                (menuLook m)
                ignore
                [
                    "服务一", [ PopupEntry.create "甲" ignore ]
                    "服务二", [ PopupEntry.create "删除" ignore |> PopupEntry.asDanger ]
                ])
                .Value

        let win = host m menu
        let plain = named menu "甲" :?> Border
        let danger = named menu "删除" :?> Border
        Assert.Same(Brushes.Transparent, plain.Background)
        pseudo plain ":pointerover" true
        pseudo danger ":pointerover" true
        Assert.Same(m.Brush PaletteKey.Hover, plain.Background)
        Assert.Same(m.Derived DerivedBrush.HoverDanger, danger.Background)
        pseudo plain ":pressed" true
        Assert.Equal((m.Metric Metric.OpacityPressed).Value, plain.Opacity, 3)
        pseudo plain ":pressed" false
        Assert.Equal(1.0, plain.Opacity, 3)

        let header =
            menu.GetVisualDescendants()
            |> Seq.choose (function
                | :? TextBlock as t when t.Classes.Contains StyleClass.sectionLabel -> Some t
                | _ -> None)
            |> Seq.head

        let divider =
            menu.GetVisualDescendants()
            |> Seq.choose (function
                | :? Border as b when b.Classes.Contains StyleClass.hairline -> Some b
                | _ -> None)
            |> Seq.head

        Assert.Same(m.Brush PaletteKey.TextFaint, header.Foreground)
        Assert.Equal(FontWeight.Medium, header.FontWeight)
        Assert.Equal((m.Metric Metric.FontMicro).Value, header.FontSize)
        Assert.Same(m.Brush PaletteKey.BorderSoft, divider.Background)
        let hoverLight = color plain.Background
        let dangerLight = color danger.Background
        m.Apply PaletteMode.Dark
        Assert.Equal(ThemePalette.color PaletteMode.Dark PaletteKey.Hover, color plain.Background)
        Assert.NotEqual(hoverLight, color plain.Background)
        Assert.NotEqual(dangerLight, color danger.Background)
        Assert.Equal(ThemePalette.color PaletteMode.Dark PaletteKey.TextFaint, color header.Foreground)
        win.Close())

[<Fact>]
let ``select button rests on surface and hover swaps the stroke`` () =
    Headless.run (fun () ->
        let m = manager ()
        let b = SelectButton(menuLook m, "模型")
        let win = host m b.View
        Assert.Same(m.Brush PaletteKey.Surface, b.View.Background)
        Assert.Same(m.Brush PaletteKey.Border, b.View.BorderBrush)
        pseudo b.View ":pointerover" true
        Assert.Same(m.Brush PaletteKey.Line, b.View.BorderBrush)
        m.Apply PaletteMode.Dark
        Assert.Equal(ThemePalette.color PaletteMode.Dark PaletteKey.Line, color b.View.BorderBrush)
        win.Close())

[<Fact>]
let ``toast action is a secondary button that hovers and presses`` () =
    Headless.run (fun () ->
        let m = manager ()
        let strip = ToastStrip(toastLook m)
        let win = host m strip.View
        strip.Add("已删除", ToastTone.Neutral, Some "撤销")
        let action = strip.ActionButtons.Head :?> Button
        Assert.Same(m.Brush PaletteKey.Surface, action.Background)
        Assert.Same(m.Brush PaletteKey.Border, action.BorderBrush)
        Assert.Equal((m.Metric Metric.TextButtonMinHeight).Value, action.MinHeight)
        pseudo action ":pointerover" true
        Assert.Same(m.Brush PaletteKey.Hover, action.Background)
        pseudo action ":pressed" true
        Assert.Equal((m.Metric Metric.OpacityPressed).Value, action.Opacity, 3)
        m.Apply PaletteMode.Dark
        Assert.Equal(ThemePalette.color PaletteMode.Dark PaletteKey.Hover, color action.Background)
        win.Close())
