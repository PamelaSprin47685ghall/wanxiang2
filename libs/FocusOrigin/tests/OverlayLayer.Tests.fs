module Wanxiang2.OverlayLayer.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.OverlayLayer

let inset = 12.0

let look () =
    let i = Cell inset
    i,
    { Scrim = SolidColorBrush(Color.FromArgb(80uy, 0uy, 0uy, 0uy))
      Surface = SolidColorBrush(Colors.White)
      Edge = SolidColorBrush(Colors.Gray)
      EdgeWidth = Cell(Thickness 1.0)
      DialogRadius = Cell(CornerRadius 8.0)
      PopupRadius = Cell(CornerRadius 6.0)
      DialogPadding = Cell(Thickness 16.0)
      PopupPadding = Cell(Thickness 2.0)
      DialogShadow = Cell(BoxShadows())
      PopupShadow = Cell(BoxShadows())
      ViewportInset = i
      PopupGap = Cell 4.0
      PopupMinWidth = Cell 180.0
      ScrimFade = Cell TimeSpan.Zero
      ScrimEasing = Avalonia.Animation.Easings.CubicEaseOut() }

/// 与 PopupPlacement.place 相同的规则（下方放不下翻到上方，再钳进视口）；组合者实际填入的是那个库。
let place ax ay aw ah (w: float) (h: float) vw vh (inset: float) gap alignRight =
    let below = ay + ah + gap
    let x = if alignRight then ax + aw - w else ax
    let y = if below + h > vh - inset && ay - gap - h >= inset then ay - gap - h else below
    let clamp v lo hi = if hi < lo then lo else max lo (min v hi)
    clamp x inset (vw - inset - w), clamp y inset (vh - inset - h)

let show (root: Control) w h =
    let window = Window(Width = w, Height = h, Content = root)
    window.Show()
    Dispatcher.UIThread.RunJobs()
    window

let button text = Button(Content = text)

// 迁自旧 UiStabilityTests。
[<Fact>]
let ``dialog is constrained to the live viewport and restores focus`` () =
    Headless.run (fun () ->
        let root = Grid()
        let trigger = button "打开"
        root.Children.Add trigger
        let _, l = look ()
        let overlay = OverlayLayer(root, l, place)
        overlay.WireDismiss()
        let window = show root 320.0 240.0
        trigger.Focus NavigationMethod.Tab |> ignore
        Dispatcher.UIThread.RunJobs()
        let field = TextBox()
        let content = StackPanel()
        content.Children.Add field
        content.Children.Add(Border(Width = 900.0, Height = 900.0))
        overlay.ShowDialog(content, 520.0)
        Dispatcher.UIThread.RunJobs()
        let dialog = root.Children[2] :?> Border
        Assert.Same(overlay.DialogCard, dialog)
        Assert.True(dialog.Width <= root.Bounds.Width - inset * 2.0 + 0.5)
        Assert.True(dialog.MaxHeight <= root.Bounds.Height - inset * 2.0 + 0.5)
        Assert.True field.IsFocused
        window.Width <- 240.0
        window.Height <- 180.0
        Dispatcher.UIThread.RunJobs()
        Assert.True(dialog.MaxWidth <= root.Bounds.Width - inset * 2.0 + 0.5)
        overlay.CloseDialog()
        Dispatcher.UIThread.RunJobs()
        Assert.True trigger.IsFocused
        window.Close())

[<Fact>]
let ``popup flips and clamps inside the viewport then restores anchor focus`` () =
    Headless.run (fun () ->
        let root = Grid()
        let anchor = button "锚点"
        anchor.HorizontalAlignment <- HorizontalAlignment.Right
        anchor.VerticalAlignment <- VerticalAlignment.Bottom
        anchor.Margin <- Thickness 6.0
        root.Children.Add anchor
        let _, l = look ()
        let overlay = OverlayLayer(root, l, place)
        overlay.WireDismiss()
        let window = show root 320.0 220.0
        anchor.Focus NavigationMethod.Tab |> ignore
        Dispatcher.UIThread.RunJobs()
        let first = button "第一项"
        let tall = StackPanel()
        tall.Children.Add first
        tall.Children.Add(Border(Width = 700.0, Height = 700.0))
        let content = ScrollViewer(Content = tall, HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled)
        overlay.ShowPopup(anchor, content, true, 420.0)
        Dispatcher.UIThread.RunJobs()
        let popup = root.Children[4] :?> Border
        Assert.Same(overlay.PopupCard, popup)
        Assert.True(popup.MaxWidth <= root.Bounds.Width - inset * 2.0 + 0.5)
        Assert.True(popup.MaxHeight <= root.Bounds.Height - inset * 2.0 + 0.5)
        Assert.True(popup.Margin.Left >= inset - 0.5)
        Assert.True(popup.Margin.Top >= inset - 0.5)
        Assert.True(popup.Margin.Left + popup.Bounds.Width <= root.Bounds.Width - inset + 0.5)
        Assert.True(popup.Margin.Top + popup.Bounds.Height <= root.Bounds.Height - inset + 0.5)
        Assert.True first.IsFocused
        overlay.ClosePopup()
        Dispatcher.UIThread.RunJobs()
        Assert.True anchor.IsFocused
        window.Close())

[<Fact>]
let ``escape precedence closes topmost overlay first and guards pending dialog`` () =
    Headless.run (fun () ->
        let root = Grid()
        let _, l = look ()
        let overlay = OverlayLayer(root, l, place)
        overlay.WireDismiss()
        let window = show root 360.0 260.0
        let anchor = button "锚点"
        root.Children.Add anchor
        anchor.Focus NavigationMethod.Tab |> ignore
        overlay.ShowDialog(button "对话框按钮", 300.0)
        Dispatcher.UIThread.RunJobs()
        overlay.ShowPopup(anchor, button "菜单项", false)
        Dispatcher.UIThread.RunJobs()
        Assert.True overlay.IsDialogOpen
        Assert.True overlay.IsPopupOpen
        Assert.True(overlay.HandleEscape())
        Assert.False overlay.IsPopupOpen
        Assert.True overlay.IsDialogOpen
        Assert.True(overlay.HandleEscape())
        Assert.False overlay.IsDialogOpen
        let mutable guard = false
        overlay.ShowDialog(button "保存中", 300.0, canDismiss = (fun () -> guard))
        Dispatcher.UIThread.RunJobs()
        Assert.True(overlay.HandleEscape())
        Assert.True overlay.IsDialogOpen
        guard <- true
        Assert.True(overlay.HandleEscape())
        Assert.False overlay.IsDialogOpen
        // 都关了：交给下一层（提示条）
        Assert.False(overlay.HandleEscape())
        window.Close())

let private wheelAt (target: Control) (root: Control) =
    let pointer = new Pointer(1, PointerType.Mouse, true)
    Dispatcher.UIThread.RunJobs()
    // 目标中心换算到 root 坐标：处理器读的就是相对 root 的点。
    let b = target.Bounds
    let translated = target.TranslatePoint(Point(b.Width / 2.0, b.Height / 2.0), root)
    let center = if translated.HasValue then translated.Value else Point(b.X + b.Width / 2.0, b.Y + b.Height / 2.0)
    let e =
        PointerWheelEventArgs(
            target, pointer, root, center, 0UL,
            PointerPointProperties(), KeyModifiers.None, Vector(0.0, -120.0))
    e.RoutedEvent <- InputElement.PointerWheelChangedEvent
    e.Source <- target
    root.RaiseEvent e
    e

// 迁自旧 DismissFocusTests（原用例经 Menu.show 打开；这里直接放一个按钮作浮层内容）。
[<Fact>]
let ``wheel outside an open popup dismisses it without scrolling the content`` () =
    Headless.run (fun () ->
        let root = Grid()
        root.Children.Add(Border(Width = 400.0, Height = 300.0))
        let _, l = look ()
        let overlay = OverlayLayer(root, l, place)
        overlay.WireDismiss()
        let anchor = Border(Width = 120.0, Height = 40.0, HorizontalAlignment = HorizontalAlignment.Right)
        root.Children.Add anchor
        let window = show root 400.0 300.0
        overlay.ShowPopup(anchor, button "重命名", false)
        Dispatcher.UIThread.RunJobs()
        let e = wheelAt root root
        Assert.False(overlay.IsPopupOpen)
        Assert.True(e.Handled)
        window.Close())

[<Fact>]
let ``wheel inside an open popup is left to the popup itself`` () =
    Headless.run (fun () ->
        let root = Grid()
        root.Children.Add(Border(Width = 400.0, Height = 300.0))
        let _, l = look ()
        let overlay = OverlayLayer(root, l, place)
        overlay.WireDismiss()
        let anchor = Border(Width = 120.0, Height = 40.0, HorizontalAlignment = HorizontalAlignment.Right)
        root.Children.Add anchor
        let window = show root 400.0 300.0
        let item = button "重命名"
        overlay.ShowPopup(anchor, item, false)
        Dispatcher.UIThread.RunJobs()
        let e = wheelAt item root
        Assert.True(overlay.IsPopupOpen)
        Assert.False(e.Handled)
        window.Close())

[<Fact>]
let ``对话框里 Tab 圈在内容里循环；嵌套对话框关闭后恢复外层`` () =
    Headless.run (fun () ->
        let root = Grid()
        let _, l = look ()
        let overlay = OverlayLayer(root, l, place)
        let window = show root 400.0 300.0
        let a, b = button "甲", button "乙"
        let panel = StackPanel()
        panel.Children.Add a
        panel.Children.Add b
        let mutable outerClosed = 0
        overlay.ShowDialog(panel, 300.0, onClosed = (fun () -> outerClosed <- outerClosed + 1))
        Dispatcher.UIThread.RunJobs()
        Assert.True a.IsFocused
        b.Focus NavigationMethod.Tab |> ignore
        b.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Tab, Source = b))
        Assert.True a.IsFocused
        a.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Tab, KeyModifiers = KeyModifiers.Shift, Source = a))
        Assert.True b.IsFocused
        overlay.ShowDialog(button "内层", 200.0)
        Dispatcher.UIThread.RunJobs()
        overlay.CloseDialog()
        Dispatcher.UIThread.RunJobs()
        Assert.True overlay.IsDialogOpen
        Assert.Equal(0, outerClosed)
        overlay.CloseDialog()
        Assert.Equal(1, outerClosed)
        Assert.False overlay.IsDialogOpen
        window.Close())

[<Fact>]
let ``视口留白是活的：主题改值后重新钳位`` () =
    Headless.run (fun () ->
        let root = Grid()
        let i, l = look ()
        let overlay = OverlayLayer(root, l, place)
        overlay.WireDismiss()
        let window = show root 320.0 240.0
        overlay.ShowDialog(Border(Width = 900.0, Height = 900.0), 900.0)
        Dispatcher.UIThread.RunJobs()
        i.Set 40.0
        Assert.True(overlay.DialogCard.MaxWidth <= root.Bounds.Width - 80.0 + 0.5)
        window.Close())
