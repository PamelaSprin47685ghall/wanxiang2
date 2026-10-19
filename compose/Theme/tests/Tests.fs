module Wanxiang2.Theme.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Styling
open Xunit
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

/// 手动时钟 + 手动推帧的管理者
let manual () =
    let mutable now = t0
    let m = new ThemeManager((fun () -> now), false)
    m, (fun ms -> now <- t0.AddMilliseconds ms)

let light k = ThemePalette.color PaletteMode.Light k
let dark k = ThemePalette.color PaletteMode.Dark k

[<Fact>]
let ``switching the theme recolors controls that were built before the switch`` () =
    Headless.run (fun () ->
        let m, at = manual ()
        let styles = ThemeStyles m
        let button = Border(Child = TextBlock(Text = "保存"))
        button.Classes.Add StyleClass.buttonPrimary
        let card = Border(Background = m.Brush PaletteKey.SurfaceContainer)
        let panel = StackPanel()
        panel.Children.Add button
        panel.Children.Add card
        let win = Window(Content = panel)
        styles.Install win.Styles
        win.Show()
        Assert.Same(m.Brush PaletteKey.Accent, button.Background)
        Assert.Equal(light PaletteKey.Accent, (button.Background :?> SolidColorBrush).Color)
        at 0.0
        m.Apply PaletteMode.Dark
        at 200.0
        m.Pump()
        Assert.False m.IsTransitioning
        Assert.Same(m.Brush PaletteKey.Accent, button.Background)
        Assert.Equal(dark PaletteKey.Accent, (button.Background :?> SolidColorBrush).Color)
        Assert.Equal(dark PaletteKey.SurfaceContainer, (card.Background :?> SolidColorBrush).Color)
        Assert.Equal(dark PaletteKey.TextOnAccent, ((button.Child :?> TextBlock).Foreground :?> SolidColorBrush).Color)
        win.Close())

[<Fact>]
let ``a second switch mid-tween takes over from the current color`` () =
    Headless.run (fun () ->
        let m, at = manual ()
        let canvas = m.Brush PaletteKey.Canvas
        at 0.0
        m.Apply PaletteMode.Dark
        let g1 = m.Generation
        at 100.0
        m.Pump()
        let mid = canvas.Color
        Assert.NotEqual(light PaletteKey.Canvas, mid)
        Assert.NotEqual(dark PaletteKey.Canvas, mid)
        m.Apply PaletteMode.Light
        Assert.True(m.Generation > g1)
        // 接管那一刻颜色不跳：新一段从中间色起步
        m.Pump()
        Assert.Equal(mid, canvas.Color)
        // 过期代次的迟到帧不停帧
        at 150.0
        m.Pump g1
        Assert.True m.IsTransitioning
        at 300.0
        m.Pump()
        Assert.Equal(light PaletteKey.Canvas, canvas.Color)
        Assert.False m.IsTransitioning)

[<Fact>]
let ``reduced motion and complete transition land at once`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        m.Motion.SetReduced true
        m.Apply PaletteMode.Dark
        Assert.Equal(dark PaletteKey.Text, (m.Brush PaletteKey.Text).Color)
        m.Motion.SetReduced false
        m.Apply PaletteMode.Light
        m.CompleteTransition()
        Assert.Equal(light PaletteKey.Text, (m.Brush PaletteKey.Text).Color))

[<Fact>]
let ``hover over a raised surface follows the theme`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        let before = (m.Derived DerivedBrush.HoverRaised).Color
        m.Motion.SetReduced true
        m.Apply PaletteMode.Dark
        Assert.NotEqual(before, (m.Derived DerivedBrush.HoverRaised).Color))

[<Fact>]
let ``changing a metric restyles controls already on screen`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        let styles = ThemeStyles m
        let track = Border()
        track.Classes.Add StyleClass.toggleTrack
        let win = Window(Content = track)
        styles.Install win.Styles
        win.Show()
        Assert.Equal(ThemeMetrics.initial Metric.ToggleTrackWidth, track.Width)
        m.SetMetric(Metric.ToggleTrackWidth, 40.0)
        Assert.Equal(40.0, track.Width)
        track.Classes.Add StyleClass.toggleOn
        Assert.Same(m.Brush PaletteKey.Accent, track.Background)
        win.Close())

[<Fact>]
let ``live values push to bound controls`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        let b = Border()
        b.Bind(Layout.Layoutable.WidthProperty, m.Metric Metric.IconButton) |> ignore
        Assert.Equal(30.0, b.Width)
        m.SetMetric(Metric.IconButton, 32.0)
        Assert.Equal(32.0, b.Width))

[<Fact>]
let ``state transitions are zero under reduced motion`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        let normal = m.StateTransitions false
        Assert.Equal(1, normal.Count)
        Assert.Equal(TimeSpan.FromMilliseconds 200.0, (normal[0] :?> Animation.BrushTransition).Duration)
        m.Motion.SetReduced true
        Assert.Equal(TimeSpan.Zero, ((m.StateTransitions true)[1] :?> Animation.BrushTransition).Duration))

[<Fact>]
let ``button classes carry the old geometry`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        let styles = ThemeStyles m
        let secondary = Border(Child = TextBlock(Text = "取消"))
        secondary.Classes.Add StyleClass.buttonSecondary
        let icon = Border()
        icon.Classes.Add StyleClass.iconButton
        let panel = StackPanel()
        panel.Children.Add secondary
        panel.Children.Add icon
        let win = Window(Content = panel)
        styles.Install win.Styles
        styles.Install win.Styles
        win.Show()
        // 旧 Ui.button：Padding(space2xl, textButtonPaddingY)、MinHeight 34、圆角 Radius.md、1px 描边
        Assert.Equal(Thickness(16.0, 6.0), secondary.Padding)
        Assert.Equal(34.0, secondary.MinHeight)
        Assert.Equal(CornerRadius 8.0, secondary.CornerRadius)
        Assert.Equal(Thickness 1.0, secondary.BorderThickness)
        Assert.Same(m.Brush PaletteKey.Border, secondary.BorderBrush)
        Assert.Equal(12.5, (secondary.Child :?> TextBlock).FontSize)
        // 旧 Ui.iconButton：30 见方
        Assert.Equal(30.0, icon.Width)
        m.SetSpace(Space.RadiusMd, 10.0)
        Assert.Equal(CornerRadius 10.0, secondary.CornerRadius)
        win.Close())

[<Fact>]
let ``native Button recolors foreground and background when ThemePalette changes`` () =
    Headless.run (fun () ->
        let m, at = manual ()
        let styles = ThemeStyles m
        let btn = Button(Content = "测试按钮")
        btn.Classes.Add StyleClass.primary
        let win = Window(Content = btn)
        styles.InstallAll win
        win.Show()

        // 初始为浅色模式 Accent
        Assert.Same(m.Brush PaletteKey.Accent, btn.Background)
        Assert.Same(m.Brush PaletteKey.TextOnAccent, btn.Foreground)
        Assert.Equal(light PaletteKey.Accent, (btn.Background :?> SolidColorBrush).Color)
        Assert.Equal(light PaletteKey.TextOnAccent, (btn.Foreground :?> SolidColorBrush).Color)

        // 切换到深色模式
        at 0.0
        m.Apply PaletteMode.Dark
        at 200.0
        m.Pump()

        Assert.Same(m.Brush PaletteKey.Accent, btn.Background)
        Assert.Same(m.Brush PaletteKey.TextOnAccent, btn.Foreground)
        Assert.Equal(dark PaletteKey.Accent, (btn.Background :?> SolidColorBrush).Color)
        Assert.Equal(dark PaletteKey.TextOnAccent, (btn.Foreground :?> SolidColorBrush).Color)
        win.Close())

[<Fact>]
let ``native Button focus-visible and focus adorner removal works`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        let styles = ThemeStyles m
        let btn = Button(Content = "操作")
        btn.Classes.Add StyleClass.secondary
        let win = Window(Content = btn)
        styles.InstallAll win
        win.Show()

        // 默认无蓝框 FocusAdorner
        Assert.Null btn.FocusAdorner

        // 伪类 :focus-visible 激活后，描边变成 Accent
        let pc: IPseudoClasses = btn.Classes
        pc.Set(":focus-visible", true)
        Assert.Same(m.Brush PaletteKey.Accent, btn.BorderBrush)

        pc.Set(":focus-visible", false)
        Assert.Same(m.Brush PaletteKey.Border, btn.BorderBrush)
        win.Close())

[<Fact>]
let ``native TextBox uses ControlStroke and respects a11y 3:1 ratio`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        let styles = ThemeStyles m
        let tb = TextBox(Text = "输入内容")
        let win = Window(Content = tb)
        styles.InstallAll win
        win.Show()

        // 静止态描边为 ControlStroke
        Assert.Same(m.Brush PaletteKey.ControlStroke, tb.BorderBrush)
        Assert.Same(m.Brush PaletteKey.Surface, tb.Background)
        Assert.Same(m.Brush PaletteKey.Text, tb.Foreground)

        // 聚焦时描边切到 Accent
        let pc: IPseudoClasses = tb.Classes
        pc.Set(":focus", true)
        Assert.Same(m.Brush PaletteKey.Accent, tb.BorderBrush)

        pc.Set(":focus", false)
        Assert.Same(m.Brush PaletteKey.ControlStroke, tb.BorderBrush)
        win.Close())

[<Fact>]
let ``reduced motion sets declarative animations duration to zero`` () =
    Headless.run (fun () ->
        let m, _ = manual ()
        let styles = ThemeStyles m
        // 查找呼吸与旋转动效样式
        let breathe =
            styles.Styles
            |> Seq.choose (function :? Style as s when s.Animations.Count > 0 && (string s.Selector).Contains(StyleClass.animBreathe) -> Some (s.Animations[0] :?> Animation.Animation) | _ -> None)
            |> Seq.head
        let spin =
            styles.Styles
            |> Seq.choose (function :? Style as s when s.Animations.Count > 0 && (string s.Selector).Contains(StyleClass.animSpin) -> Some (s.Animations[0] :?> Animation.Animation) | _ -> None)
            |> Seq.head

        Assert.True(breathe.Duration > TimeSpan.Zero)
        Assert.True(spin.Duration > TimeSpan.Zero)

        // 开启减弱动效
        m.Motion.SetReduced true

        Assert.Equal(TimeSpan.Zero, breathe.Duration)
        Assert.Equal(TimeSpan.Zero, spin.Duration)

        // 恢复正常动效
        m.Motion.SetReduced false

        Assert.True(breathe.Duration > TimeSpan.Zero)
        Assert.True(spin.Duration > TimeSpan.Zero))
