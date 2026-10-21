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

let private ease () =
    let mutable from = 0.0
    let mutable to' = 0.0
    let mutable n = 0
    let mutable last = -9999.0

    {
        Start =
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
        IsOwn = fun y -> abs (y - last) < 1.0
    }

let private tall () =
    let p = StackPanel(Orientation = Orientation.Vertical)
    p.Children.Add(Border(Height = 800.0))
    p.Children.Add(Border(Height = 800.0))
    p

[<Fact>]
let ``回到最新有读屏 Invoke；按下时报告焦点`` () =
    Headless.run (fun () ->
        let s = ChatScroller(ease ())
        let mutable focused = false
        s.LatestPressed.Add(fun f -> focused <- f)

        s.ShowLatest
            {
                Visible = true
                Text = "3 条新消息"
                Wide = true
            }

        let w = Window(Width = 400.0, Height = 400.0, Content = s.Host)
        w.Show()
        Assert.True s.LatestButton.IsVisible
        Assert.Equal("3 条新消息", AutomationProperties.GetName s.LatestButton)
        let label = (s.LatestButton.Content :?> StackPanel).Children[1]
        Assert.True label.IsVisible
        let peer = ControlAutomationPeer.CreatePeerForElement s.LatestButton
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType())
        ((peer :> obj) :?> IInvokeProvider).Invoke()
        Dispatcher.UIThread.RunJobs()
        Assert.False focused
        w.Close())

[<Fact>]
let ``平滑滚动直设 Offset 到达；滚轮触发 wheeled`` () =
    Headless.run (fun () ->
        let s = ChatScroller(ease ())
        s.Content <- tall ()
        let w = Window(Width = 400.0, Height = 300.0, Content = s.Host)
        w.Show()
        w.UpdateLayout()
        let mutable arrived = -1.0
        Assert.True(s.SmoothTo((fun () -> 200.0), ignore, fun y -> arrived <- y))
        Assert.Equal(200.0, arrived)
        Assert.Equal(200.0, s.Offset)
        Assert.False s.IsSmoothing
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
                Vector(0.0, 1.0)
            )

        args.RoutedEvent <- InputElement.PointerWheelChangedEvent
        s.Viewer.RaiseEvent args
        w.Close())

[<Fact>]
let ``贴底时隐藏浮钮；前置锚点补偿偏移`` () =
    Headless.run (fun () ->
        let s = ChatScroller(ease ())
        s.Content <- tall ()
        let w = Window(Width = 400.0, Height = 300.0, Content = s.Host)
        w.Show()
        w.UpdateLayout()
        s.ScrollToEndNow()
        Dispatcher.UIThread.RunJobs()

        s.ShowLatest
            {
                Visible = true
                Text = "回到最新"
                Wide = false
            }

        s.ApplyOffset(s.Extent - s.Viewport - 10.0)
        Dispatcher.UIThread.RunJobs()
        Assert.False s.LatestButton.IsVisible
        w.Close())

[<Fact>]
let ``PinOffset 写的上移不算用户上滚；ApplyOffset 写的算`` () =
    Headless.run (fun () ->
        let s = ChatScroller(ease ())
        s.Content <- tall ()
        let w = Window(Width = 400.0, Height = 300.0, Content = s.Host)
        w.Show()
        w.UpdateLayout()
        s.ScrollToEndNow()
        Dispatcher.UIThread.RunJobs()
        let notes = ResizeArray<bool>()
        s.Scrolled.Add(fun n -> notes.Add n.UserUp)
        s.PinOffset(s.Offset - 300.0)
        Dispatcher.UIThread.RunJobs()
        s.ApplyOffset(s.Offset - 300.0)
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<bool list>([ false; true ], List.ofSeq notes)
        w.Close())
