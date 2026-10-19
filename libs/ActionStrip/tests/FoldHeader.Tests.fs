module Wanxiang2.FoldHeader.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.FoldHeader
open Wanxiang2.FoldHeader.Tests

let private caption = Cell 12.0
let private look =
    { Muted = Brushes.Gray
      Gauge = (function FoldGauge.CaptionSize -> caption :> IObservable<float> | FoldGauge.RotateMs -> Cell 160.0 | _ -> Cell 4.0)
      Chevron = fun b -> Border(Background = b, Width = 10.0, Height = 10.0)
      Dress = fun b -> b.Classes.Add "surface"
      Reduced = Cell true }

[<Fact>]
let ``开合转箭头、报展开状态并通知`` () =
    Headless.run (fun () ->
        let f = FoldHeader(look, false)
        let seen = ResizeArray()
        f.Toggled.Add seen.Add
        Assert.Equal(0.0, f.Rotation.GetBaseValue(Avalonia.Media.RotateTransform.AngleProperty).Value)
        f.Toggle()
        Assert.True f.Expanded
        Assert.Equal(90.0, f.Rotation.GetBaseValue(Avalonia.Media.RotateTransform.AngleProperty).Value)
        Assert.Equal("已展开", AutomationProperties.GetItemStatus f.Header)
        f.Toggle()
        Assert.Equal<bool list>([ true; false ], List.ofSeq seen))

[<Fact>]
let ``前置件在箭头之后，尾随件在标题之后`` () =
    Headless.run (fun () ->
        let f = FoldHeader(look, true)
        let lead = Border()
        let tail = Border()
        f.AddLeading lead
        f.AddTrailing tail
        Assert.Same(lead, f.Row.Children[1])
        Assert.Same(f.Caption, f.Row.Children[2])
        Assert.Same(tail, f.Row.Children[3])
        Assert.Equal(90.0, f.Rotation.GetBaseValue(Avalonia.Media.RotateTransform.AngleProperty).Value))

[<Fact>]
let ``键盘 Enter 也能开合`` () =
    Headless.run (fun () ->
        let f = FoldHeader(look, false)
        let w = Window(Content = f.Header)
        w.Show()
        f.Header.Focus() |> ignore
        f.Header.RaiseEvent(Avalonia.Input.KeyEventArgs(RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter))
        Assert.True f.Expanded
        w.Close())

[<Fact>]
let ``标题字号是活的`` () =
    Headless.run (fun () ->
        let f = FoldHeader(look, false)
        let w = Window(Content = f.Header)
        w.Show()
        caption.Set 14.0
        Assert.Equal(14.0, f.Caption.FontSize)
        Assert.Contains("surface", f.Header.Classes)
        w.Content <- null
        Assert.Equal(0, caption.Count)
        caption.Set 12.0
        w.Close())

let private invokeVia (c: Avalonia.Controls.Control) =
    let peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement c
    Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType())
    let p = Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(peer)
    p.Invoke()
    Avalonia.Threading.Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``折叠头有读屏 Invoke 且能开合`` () =
    Headless.run (fun () ->
        let f = FoldHeader(look, false)
        invokeVia f.Header
        Assert.True f.Expanded)
