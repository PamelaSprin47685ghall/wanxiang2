module Wanxiang2.ToastStrip.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang2.ToastStrip
open Wanxiang2.WorkspaceState

let look () =
    { Surface = SolidColorBrush(Colors.White)
      Edge = SolidColorBrush(Colors.Gray)
      Text = SolidColorBrush(Colors.Black)
      Accent = (fun tone -> if tone = ToastTone.Failure then Brushes.Red else Brushes.Gray)
      AccentOpacity = 0.6
      Padding = Thickness(16.0, 12.0)
      Shadow = BoxShadows()
      LineHeight = 18.0
      ColumnSpacing = 12.0
      Spacing = 8.0
      Margin = Thickness(24.0, 0.0, 24.0, 32.0)
      MaxWidth = 480.0
      BodyMaxHeight = 160.0
      Fade = TimeSpan.Zero
      FadeEasing = Avalonia.Animation.Easings.CubicEaseOut() }

/// 主题资源字典的替身：控件绑定资源键，这里只往应用资源写值。
let seedResources () =
    let r = Application.Current.Resources
    r["Metric.ToastAccentWidth"] <- box 2.0
    r["CornerRadius.RadiusPill"] <- box (CornerRadius 999.0)
    r["CornerRadius.RadiusMd"] <- box (CornerRadius 8.0)
    r["Thickness.BorderThin"] <- box (Thickness 1.0)
    r["Metric.FontBody"] <- box 12.0


let host w h (strip: ToastStrip) =
    let root = Grid()
    root.Children.Add strip.View
    let window = Window(Width = w, Height = h, Content = root)
    window.Show()
    Dispatcher.UIThread.RunJobs()
    strip.Fit(root.Bounds.Width, root.Bounds.Height, 12.0)
    root, window

let byName (root: Visual) name =
    root.GetVisualDescendants() |> Seq.choose (function :? Control as c when AutomationProperties.GetName c = name -> Some c | _ -> None) |> Seq.tryHead

// 迁自旧 OverlayMenuTests.toast_stays_non_focusable_and_escape_dismisses_it（Escape 那步在组合者里测）。
[<Fact>]
let ``toast stays non focusable and removal is synchronous`` () =
    Headless.run (fun () ->
        seedResources ()
        let l = look ()
        let strip = ToastStrip l
        let root, window = host 400.0 300.0 strip
        strip.Add("已保存", ToastTone.Neutral, None)
        let toast = (byName root "已保存").Value
        Assert.False toast.Focusable
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting toast)
        strip.Remove "已保存"
        Assert.True((byName root "已保存").IsNone)
        window.Close())

// 迁自旧 UiStabilityTests `toast stays inside narrow viewport and is keyboard dismissible live content`（宽度与 live 部分）。
[<Fact>]
let ``toast stays inside narrow viewport as assertive live content`` () =
    Headless.run (fun () ->
        seedResources ()
        let l = look ()
        let strip = ToastStrip l
        let root, window = host 320.0 220.0 strip
        let message = String.replicate 20 "很长的错误说明，需要在窄窗口内保持可访问。"
        strip.Add(message, ToastTone.Failure, None)
        Dispatcher.UIThread.RunJobs()
        let toast = (byName root message).Value
        Assert.False toast.IsFocused
        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting toast)
        Assert.True(toast.Bounds.Width <= root.Bounds.Width - 12.0 * 2.0 + 0.5)
        window.Close())

[<Fact>]
let ``undo action carries accelerator help and focus returns home on removal`` () =
    Headless.run (fun () ->
        seedResources ()
        let l = look ()
        let strip = ToastStrip l
        let root, window = host 400.0 300.0 strip
        let home = Button(Content = "home")
        root.Children.Add home
        strip.WireFocusHome(fun () -> home.Focus() |> ignore)
        let invoked = ResizeArray()
        let focused = ResizeArray()
        strip.ActionInvoked.Add invoked.Add
        strip.ActionFocused.Add focused.Add
        strip.Add("已删除", ToastTone.Neutral, Some "撤销")
        Dispatcher.UIThread.RunJobs()
        let b = strip.ActionButtons |> List.exactlyOne
        Assert.Equal("Ctrl+Z", AutomationProperties.GetAcceleratorKey b)
        Assert.Equal("按 Ctrl / ⌘ + Z 直接撤销；按 F6 或 Tab 可移到这里", AutomationProperties.GetHelpText b)
        Assert.True b.IsTabStop
        Assert.True(strip.FocusAction())
        Assert.True b.IsFocused
        Assert.Equal<string list>([ "已删除" ], List.ofSeq focused)
        b.RaiseEvent(KeyEventArgs(Key = Key.Enter, RoutedEvent = InputElement.KeyDownEvent))
        Assert.Equal<string list>([ "已删除" ], List.ofSeq invoked)
        strip.Remove "已删除"
        Assert.True home.IsFocused
        Assert.False(strip.FocusAction())
        window.Close())

[<Fact>]
let ``theme resources reach shown toasts and static layout values apply at construction`` () =
    Headless.run (fun () ->
        seedResources ()
        let l = look ()
        let strip = ToastStrip l
        let root, window = host 800.0 300.0 strip
        strip.Add("一", ToastTone.Info, None)
        let toast = (byName root "一").Value :?> Border
        // 静态参数在构造时落地
        Assert.Equal(Thickness(16.0, 12.0), toast.Padding)
        Assert.Equal(480.0, toast.MaxWidth)
        // 主题几何走资源键：改字典值即随动（声明式，无手工订阅）
        Assert.Equal(CornerRadius 8.0, toast.CornerRadius)
        Application.Current.Resources["CornerRadius.RadiusMd"] <- box (CornerRadius 6.0)
        Assert.Equal(CornerRadius 6.0, toast.CornerRadius)
        Application.Current.Resources["Thickness.BorderThin"] <- box (Thickness 2.0)
        Assert.Equal(Thickness 2.0, toast.BorderThickness)
        Assert.Equal<string list>([ "一" ], strip.Messages)
        window.Close())

// 新增：提示条动作键使用原生 Button，角色 Button，激活与 Enter 同一路径。
[<Fact>]
let ``toast action is native button and activates`` () =
    Headless.run (fun () ->
        seedResources ()
        let l = look ()
        let strip = ToastStrip l
        let _, window = host 400.0 300.0 strip
        let invoked = ResizeArray()
        strip.ActionInvoked.Add invoked.Add
        strip.Add("已删除", ToastTone.Neutral, Some "撤销")
        Dispatcher.UIThread.RunJobs()
        let b = strip.ActionButtons |> List.exactlyOne :?> Button
        Assert.Equal("撤销", b.Content :?> string)
        b.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent))
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<string list>([ "已删除" ], Seq.toList invoked)
        window.Close())

[<Fact>]
let ``render idempotently reconciles toasts list`` () =
    Headless.run (fun () ->
        seedResources ()
        let l = look ()
        let strip = ToastStrip l
        let _root, window = host 400.0 300.0 strip
        let now = DateTimeOffset.UtcNow
        let vs1: ToastVS list = [ { Id = 1; Text = "消息一"; Tone = ToastTone.Info; UndoAction = None; ExpiresAt = now.AddSeconds 5.0 } ]
        strip.Render vs1
        Assert.Equal<string list>([ "消息一" ], strip.Messages)
        let vs2: ToastVS list = [ { Id = 2; Text = "消息二"; Tone = ToastTone.Success; UndoAction = Some "undo-1"; ExpiresAt = now.AddSeconds 5.0 } ]
        strip.Render vs2
        Assert.Equal<string list>([ "消息二" ], strip.Messages)
        strip.Render []
        Assert.Empty strip.Messages
        window.Close())
