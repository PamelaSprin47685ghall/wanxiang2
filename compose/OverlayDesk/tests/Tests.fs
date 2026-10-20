module Wanxiang2.OverlayDesk.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang2.OverlayLayer
open Wanxiang2.ToastStrip
open Wanxiang2.PopupMenu
open Wanxiang2.OverlayDesk
open Wanxiang2.WorkspaceState

let inset = 12.0

let look () =
    // 主题把视口留白写进资源字典：层按它钳位，提示条带也按它留白。
    Application.Current.Resources["Margin.OverlayInset"] <- box (Thickness inset)
    { Layer =
        { Scrim = SolidColorBrush(Color.FromArgb(80uy, 0uy, 0uy, 0uy))
          Surface = Brushes.White
          Edge = Brushes.Gray
          DialogShadow = BoxShadows()
          PopupShadow = BoxShadows()
          PopupGap = 4.0
          PopupMinWidth = 180.0
          ScrimFade = TimeSpan.Zero
          ScrimEasing = Avalonia.Animation.Easings.CubicEaseOut() }
      Toasts =
        { Surface = Brushes.White
          Edge = Brushes.Gray
          Text = Brushes.Black
          Accent = (fun _ -> Brushes.Gray)
          AccentOpacity = 0.6
          Padding = Thickness(16.0, 12.0)
          Shadow = BoxShadows()
          LineHeight = 18.0
          ColumnSpacing = 12.0
          Spacing = 8.0
          Margin = Thickness(24.0, 0.0, 24.0, 32.0)
          MaxWidth = 480.0
          BodyMaxHeight = 160.0
          // 减弱动效：主题给 0
          Fade = TimeSpan.Zero
          FadeEasing = Avalonia.Animation.Easings.CubicEaseOut() }
      Menu =
        { Text = Brushes.Black
          Muted = Brushes.Gray
          Faint = Brushes.LightGray
          Danger = Brushes.Red
          Accent = Brushes.Blue
          Check = (fun b -> Border(Width = 12.0, Height = 12.0, Background = b))
          Chevron = (fun b -> Border(Width = 10.0, Height = 10.0, Background = b))
          IconSlotWidth = 16.0
          ItemMinHeight = 32.0
          MaxHeight = 200.0 }
      MenuMinWidth = 200.0
      GroupedMenuMinWidth = 240.0
      DwellFailureMs = 9000.0
      DwellWarningMs = 6500.0
      DwellDefaultMs = 4000.0
      DismissTick = TimeSpan.FromMilliseconds 100.0 }

let show (root: Control) w h =
    let window = Window(Width = w, Height = h, Content = root)
    window.Show()
    Dispatcher.UIThread.RunJobs()
    window

let named (root: Visual) name =
    root.GetVisualDescendants() |> Seq.choose (function :? Control as c when AutomationProperties.GetName c = name -> Some c | _ -> None) |> Seq.tryHead

let key (c: Control) k =
    c.RaiseEvent(KeyEventArgs(Key = k, RoutedEvent = InputElement.KeyDownEvent))
    Dispatcher.UIThread.RunJobs()

// 迁自旧 OverlayMenuTests。
[<Fact>]
let ``toast_stays_non_focusable_and_escape_dismisses_it`` () =
    Headless.run (fun () ->
        let root = Grid()
        let desk = OverlayDesk(root, look ())
        desk.Toast("已保存", ToastTone.Neutral)
        let toast = named root "已保存"
        Assert.True toast.IsSome
        Assert.False toast.Value.Focusable
        Assert.True(desk.HandleEscape())
        Assert.True((named root "已保存").IsNone)
        Assert.False(desk.HandleEscape()))

// 迁自旧 OverlayMenuTests（减弱动效由主题给 0 的淡入时长表达）。
[<Fact>]
let ``toast_removes_immediately_under_reduced_motion`` () =
    Headless.run (fun () ->
        let root = Grid()
        let desk = OverlayDesk(root, look ())
        desk.Toast("即时", ToastTone.Warning)
        Assert.True(desk.HandleEscape())
        Assert.True((named root "即时").IsNone))

// 迁自旧 UiStabilityTests。
[<Fact>]
let ``toast stays inside narrow viewport and is keyboard dismissible live content`` () =
    Headless.run (fun () ->
        let root = Grid()
        let desk = OverlayDesk(root, look ())
        desk.WireDismiss()
        let window = show root 320.0 220.0
        let message = String.replicate 20 "很长的错误说明，需要在窄窗口内保持可访问。"
        desk.Toast(message, ToastTone.Failure)
        Dispatcher.UIThread.RunJobs()
        let toast = (named root message).Value
        Assert.False toast.Focusable
        Assert.False toast.IsFocused
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting toast)
        Assert.True(toast.Bounds.Width <= root.Bounds.Width - inset * 2.0 + 0.5)
        Assert.True(desk.HandleEscape())
        Dispatcher.UIThread.RunJobs()
        Assert.True((named root message).IsNone)
        window.Close())

[<Fact>]
let ``renderToasts updates strip from ViewState without local timers`` () =
    Headless.run (fun () ->
        let root = Grid()
        let desk = OverlayDesk(root, look ())
        desk.WireDismiss()
        let window = show root 400.0 300.0
        let now = DateTimeOffset.UtcNow
        let t1: ToastVS = { Id = 1; Text = "状态一"; Tone = ToastTone.Info; UndoAction = None; ExpiresAt = now.AddSeconds 5.0 }
        desk.RenderToasts [ t1 ]
        Assert.True((named root "状态一").IsSome)
        desk.RenderToasts []
        Assert.True((named root "状态一").IsNone)
        window.Close())

[<Fact>]
let ``render overlayVS closed dismisses dialog and popup`` () =
    Headless.run (fun () ->
        let root = Grid()
        let desk = OverlayDesk(root, look ())
        desk.ShowDialog(TextBlock(Text = "对话框内容"), 200.0)
        Assert.True desk.Layer.IsDialogOpen
        desk.Render OverlayVS.Closed
        Assert.False desk.Layer.IsDialogOpen)

// 迁自旧 UiStabilityTests `escape precedence closes topmost overlay first and guards pending dialog` 的提示条那一层。
[<Fact>]
let ``escape closes popup then dialog then newest toast`` () =
    Headless.run (fun () ->
        let root = Grid()
        let anchor = Button(Content = "a")
        root.Children.Add anchor
        let desk = OverlayDesk(root, look ())
        desk.WireDismiss()
        let window = show root 400.0 300.0
        desk.Toast("旧", ToastTone.Neutral)
        desk.Toast("新", ToastTone.Neutral)
        desk.ShowDialog(TextBlock(Text = "d"), 200.0)
        desk.ShowMenu(anchor, false, [ PopupEntry.create "项" ignore ])
        Dispatcher.UIThread.RunJobs()
        Assert.True(desk.HandleEscape())
        Assert.False desk.Layer.IsPopupOpen
        Assert.True desk.Layer.IsDialogOpen
        Assert.True(desk.HandleEscape())
        Assert.False desk.Layer.IsDialogOpen
        Assert.True(desk.HandleEscape())
        Assert.True((named root "新").IsNone)
        Assert.True((named root "旧").IsSome)
        window.Close())

// 迁自旧 UiStabilityTests。
[<Fact>]
let ``forty item menu keeps long labels bounded and supports directional focus`` () =
    Headless.run (fun () ->
        let root = Grid()
        let anchor = Button(Content = "打开菜单", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom)
        root.Children.Add anchor
        let desk = OverlayDesk(root, look ())
        desk.WireDismiss()
        let window = show root 360.0 320.0
        anchor.Focus NavigationMethod.Tab |> ignore
        let longLabel = "菜单项 01 " + String.replicate 18 "非常长的名称"
        let entries =
            [ for i in 1..40 ->
                  let label = if i = 1 then longLabel else sprintf "菜单项 %02d" i
                  PopupEntry.create label ignore |> (if i % 3 = 0 then PopupEntry.withIcon (fun b -> Border(Background = b)) else id) ]
        desk.ShowMenu(anchor, true, entries)
        Dispatcher.UIThread.RunJobs()
        let first = (named root longLabel).Value
        let second = (named root "菜单项 02").Value
        Assert.True first.IsFocused
        Assert.True(first.Classes.Contains "focus-by-program")
        key first Key.Down
        Assert.True second.IsFocused
        key second Key.End
        Assert.True (named root "菜单项 40").Value.IsFocused
        let popup = root.Children[4] :?> Border
        Assert.Same(desk.Layer.PopupCard, popup)
        Assert.True(first.Bounds.Width <= popup.Bounds.Width + 0.5)
        let scroller = popup.GetVisualDescendants() |> Seq.choose (function :? ScrollViewer as s -> Some s | _ -> None) |> Seq.head
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height)
        window.Close())

[<Fact>]
let ``activating a menu entry closes the popup before running the action`` () =
    Headless.run (fun () ->
        let root = Grid()
        let anchor = Button(Content = "a")
        root.Children.Add anchor
        let desk = OverlayDesk(root, look ())
        desk.WireDismiss()
        let window = show root 400.0 300.0
        let mutable openDuringAction = true
        desk.ShowMenu(anchor, false, [ PopupEntry.create "重命名" (fun () -> openDuringAction <- desk.Layer.IsPopupOpen) ])
        Dispatcher.UIThread.RunJobs()
        key (named root "重命名").Value Key.Enter
        Assert.False openDuringAction
        Assert.False desk.Layer.IsPopupOpen
        window.Close())

[<Fact>]
let ``select button opens grouped options with down`` () =
    Headless.run (fun () ->
        let root = Grid()
        let desk = OverlayDesk(root, look ())
        desk.WireDismiss()
        let b = desk.SelectButton("模型", fun () -> [ "服务", [ PopupEntry.create "甲" ignore ] ])
        root.Children.Add b.View
        let window = show root 400.0 300.0
        key b.View Key.Down
        Assert.True desk.Layer.IsPopupOpen
        Assert.True((named root "甲").IsSome)
        window.Close())

[<Fact>]
let ``undo toast action runs once and dedupe keeps the newest`` () =
    Headless.run (fun () ->
        let root = Grid()
        let desk = OverlayDesk(root, look ())
        let window = show root 400.0 300.0
        let mutable undone = 0
        desk.ToastWith("已删除", ToastTone.Neutral, Some("撤销", fun () -> undone <- undone + 1))
        desk.ToastWith("已删除", ToastTone.Neutral, Some("撤销", fun () -> undone <- undone + 10))
        Assert.Equal(1, desk.ToastActionButtons.Length)
        Assert.True(desk.HasToastAction "撤销")
        Assert.True(desk.TryInvokeToastAction "撤销")
        Assert.Equal(10, undone)
        Assert.False(desk.TryInvokeToastAction "撤销")
        for i in 1..4 do desk.Toast(sprintf "第%d" i, ToastTone.Info)
        Assert.True((named root "第1").IsNone)
        Assert.True((named root "第4").IsSome)
        window.Close())

[<Fact>]
let ``focused toast action returns focus home when invoked`` () =
    Headless.run (fun () ->
        let root = Grid()
        let home = Button(Content = "输入框")
        root.Children.Add home
        let desk = OverlayDesk(root, look ())
        desk.WireFocusHome(fun () -> home.Focus() |> ignore)
        let window = show root 400.0 300.0
        desk.ToastWith("已归档", ToastTone.Success, Some("撤销", ignore))
        Dispatcher.UIThread.RunJobs()
        Assert.True(desk.FocusToastAction())
        let b = desk.ToastActionButtons.Head
        b.RaiseEvent(KeyEventArgs(Key = Key.Enter, RoutedEvent = InputElement.KeyDownEvent))
        Assert.True home.IsFocused
        Assert.True((named root "已归档").IsNone)
        window.Close())
