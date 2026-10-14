module Wanxiang2.ChatScroller.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ChatScroller
open Wanxiang2.ChatScroller.Tests

let private look () =
    { Ink = (fun _ -> Brushes.Blue :> IBrush)
      Gauge =
        function
        | ScrollerGauge.BottomThreshold -> Cell 48.0 :> _
        | ScrollerGauge.RevealThreshold -> Cell 120.0 :> _
        | ScrollerGauge.IconButton -> Cell 32.0 :> _
        | ScrollerGauge.CompactTarget -> Cell 44.0 :> _
        | ScrollerGauge.WideMinWidth -> Cell 96.0 :> _
        | ScrollerGauge.FontSmall -> Cell 12.5 :> _
        | ScrollerGauge.SpaceMd -> Cell 8.0 :> _
        | ScrollerGauge.SpaceXl -> Cell 12.0 :> _
        | ScrollerGauge.Space2xl -> Cell 16.0 :> _
        | ScrollerGauge.Space3xl -> Cell 20.0 :> _
      Glyph = (fun _ _ -> Border(Width = 12.0, Height = 12.0) :> Control)
      Frame = Cell(TimeSpan.FromMilliseconds 16.0)
      Reduced = Cell false
      DressLatest = ignore
      Shadow = fun () -> BoxShadows() }

let private ease () =
    let mutable from = 0.0
    let mutable to' = 0.0
    let mutable n = 0
    let mutable last = -9999.0
    { Start =
        fun _ offset target _ ->
            from <- offset
            to' <- target
            n <- 0
            last <- offset
            abs (target - offset) >= 5.0
      Frame =
        fun _ target ->
            n <- n + 1
            if n >= 2 then
                last <- target
                EaseStep.Arrived target
            else
                let y = from + (target - from) * 0.5
                last <- y
                EaseStep.Moved y
      Cancel = fun () -> n <- 99
      Pin = fun y -> last <- y
      IsOwn = fun y -> abs (y - last) < 1.0 }

let private tall () =
    let p = StackPanel(Orientation = Orientation.Vertical)
    p.Children.Add(Border(Height = 800.0))
    p.Children.Add(Border(Height = 800.0))
    p

[<Fact>]
let ``回到最新有读屏 Invoke；按下时报告焦点`` () =
    Headless.run (fun () ->
        let s = ChatScroller(look (), ease ())
        let mutable focused = false
        s.LatestPressed.Add(fun f -> focused <- f)
        s.ShowLatest { Visible = true; Text = "3 条新消息"; Wide = true }
        let w = Window(Width = 400.0, Height = 400.0, Content = s.Host)
        w.Show()
        Assert.True s.LatestButton.IsVisible
        Assert.Equal("3 条新消息", AutomationProperties.GetName s.LatestButton)
        let label = (s.LatestButton.Child :?> StackPanel).Children[1]
        Assert.True label.IsVisible
        let peer = ControlAutomationPeer.CreatePeerForElement s.LatestButton
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType())
        ((peer :> obj) :?> IInvokeProvider).Invoke()
        Dispatcher.UIThread.RunJobs()
        Assert.False focused
        w.Close())

[<Fact>]
let ``平滑滚动两拍到达；滚轮打断`` () =
    Headless.run (fun () ->
        let s = ChatScroller(look (), ease ())
        s.Content <- tall ()
        let w = Window(Width = 400.0, Height = 300.0, Content = s.Host)
        w.Show()
        w.UpdateLayout()
        let mutable arrived = -1.0
        Assert.True(s.SmoothTo((fun () -> 200.0), ignore, fun y -> arrived <- y))
        Assert.True s.IsSmoothing
        s.Step()
        Assert.True(s.Offset > 0.0 && s.Offset < 200.0)
        s.Step()
        Assert.False s.IsSmoothing
        Assert.Equal(200.0, arrived)
        Assert.True(s.SmoothTo((fun () -> 50.0), ignore, ignore))
        let props = PointerPointProperties()
        let args =
            PointerWheelEventArgs(
                s.Viewer,
                new Pointer(1, PointerType.Mouse, true),
                s.Viewer,
                Point(),
                0UL,
                props,
                KeyModifiers.None,
                Vector(0.0, 1.0))
        args.RoutedEvent <- InputElement.PointerWheelChangedEvent
        s.Viewer.RaiseEvent args
        Assert.False s.IsSmoothing
        w.Close())

[<Fact>]
let ``贴底时隐藏浮钮；前置锚点补偿偏移`` () =
    Headless.run (fun () ->
        let s = ChatScroller(look (), ease ())
        s.Content <- tall ()
        let w = Window(Width = 400.0, Height = 300.0, Content = s.Host)
        w.Show()
        w.UpdateLayout()
        s.ScrollToEndNow()
        Dispatcher.UIThread.RunJobs()
        s.ShowLatest { Visible = true; Text = "回到最新"; Wide = false }
        s.SetOffset(s.Extent - s.Viewport - 10.0)
        Dispatcher.UIThread.RunJobs()
        Assert.False s.LatestButton.IsVisible
        s.BeginPrependAnchor()
        Assert.True s.PrependAnchorPending
        let before = s.Offset
        (s.Content :?> StackPanel).Children.Insert(0, Border(Height = 200.0))
        w.UpdateLayout()
        s.RestorePrependAnchorDeferred()
        Dispatcher.UIThread.RunJobs()
        Assert.False s.PrependAnchorPending
        Assert.True(s.Offset >= before)
        w.Close())
