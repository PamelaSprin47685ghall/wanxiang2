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

let fixed' (v: 'T) =
    { new IObservable<'T> with
        member _.Subscribe o =
            o.OnNext v
            { new IDisposable with member _.Dispose() = () } }

let manager () =
    let m = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
    m.Motion.SetReduced true
    m

let menuLook (m: ThemeManager) =
    let b k = m.Brush k :> IBrush
    { Text = b PaletteKey.Text; Muted = b PaletteKey.TextMuted; Faint = b PaletteKey.TextFaint; Danger = b PaletteKey.Danger; Accent = b PaletteKey.Accent
      Check = (fun br -> Border(Background = br)); Chevron = (fun br -> Border(Background = br))
      FontSize = m.Metric Metric.FontSmall; HintFontSize = m.Metric Metric.FontMicro
      IconSlotWidth = fixed' 16.0; IconHeight = fixed' 16.0; RightSlotMinWidth = fixed' 16.0
      HintMargin = fixed' (Thickness 0.0); ColumnSpacing = m.Space Space.Md
      ItemPadding = fixed' (Thickness 4.0); ItemRadius = fixed' (CornerRadius 4.0); ItemMinHeight = fixed' 28.0
      ItemSpacing = m.Space Space.XXs; MaxHeight = fixed' 300.0
      DividerMargin = fixed' (Thickness 2.0); HeaderMargin = fixed' (Thickness 2.0)
      SelectPadding = fixed' (Thickness 4.0); SelectRadius = fixed' (CornerRadius 4.0)
      SelectEdgeWidth = fixed' (Thickness 1.0); SelectCaptionMaxWidth = fixed' 260.0 }

let toastLook (m: ThemeManager) =
    let b k = m.Brush k :> IBrush
    { Surface = b PaletteKey.SurfaceRaised; Edge = b PaletteKey.Hairline; Text = b PaletteKey.Text; Accent = (fun _ -> b PaletteKey.TextMuted)
      AccentWidth = fixed' 2.0; AccentRadius = fixed' (CornerRadius 1.0); AccentOpacity = fixed' 1.0; EdgeWidth = fixed' (Thickness 1.0)
      Radius = fixed' (CornerRadius 4.0); Padding = fixed' (Thickness 4.0); Shadow = fixed' (BoxShadows())
      FontSize = m.Metric Metric.FontSmall; LineHeight = fixed' 18.0; ColumnSpacing = fixed' 8.0; Spacing = fixed' 8.0
      Margin = fixed' (Thickness 0.0); MaxWidth = fixed' 400.0; BodyMaxHeight = fixed' 100.0
      Fade = fixed' TimeSpan.Zero; FadeEasing = EaseOutCubicEasing() }

let pseudo (c: Control) (name: string) (on: bool) =
    let pc: IPseudoClasses = c.Classes
    pc.Set(name, on)

let color (b: IBrush) = (b :?> SolidColorBrush).Color

let host (m: ThemeManager) (content: Control) =
    let win = Window(Width = 400.0, Height = 300.0, Content = content)
    (ThemeStyles m).Install win.Styles
    win.Show()
    win

let named (root: Visual) name =
    root.GetVisualDescendants()
    |> Seq.choose (function :? Control as c when Avalonia.Automation.AutomationProperties.GetName c = name -> Some c | _ -> None)
    |> Seq.head

[<Fact>]
let ``menu items hover and press through theme classes and follow a theme switch`` () =
    Headless.run (fun () ->
        let m = manager ()
        let menu =
            (PopupMenu.buildGrouped (menuLook m) ignore
                [ "服务一", [ PopupEntry.create "甲" ignore ]
                  "服务二", [ PopupEntry.create "删除" ignore |> PopupEntry.asDanger ] ]).Value
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
            menu.GetVisualDescendants() |> Seq.choose (function :? TextBlock as t when t.Classes.Contains StyleClass.sectionLabel -> Some t | _ -> None) |> Seq.head
        let divider =
            menu.GetVisualDescendants() |> Seq.choose (function :? Border as b when b.Classes.Contains StyleClass.hairline -> Some b | _ -> None) |> Seq.head
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
        let action = strip.ActionButtons.Head :?> Border
        Assert.Same(m.Brush PaletteKey.Surface, action.Background)
        Assert.Same(m.Brush PaletteKey.Border, action.BorderBrush)
        Assert.Same(m.Brush PaletteKey.Text, (action.Child :?> TextBlock).Foreground)
        Assert.Equal((m.Metric Metric.TextButtonMinHeight).Value, action.MinHeight)
        pseudo action ":pointerover" true
        Assert.Same(m.Brush PaletteKey.Hover, action.Background)
        pseudo action ":pressed" true
        Assert.Equal((m.Metric Metric.OpacityPressed).Value, action.Opacity, 3)
        m.Apply PaletteMode.Dark
        Assert.Equal(ThemePalette.color PaletteMode.Dark PaletteKey.Hover, color action.Background)
        win.Close())
