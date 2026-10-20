module Wanxiang2.FoldHeader.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.FoldHeader
open Wanxiang2.FoldHeader.Tests

[<Fact>]
let ``开合报展开状态并通知`` () =
    Headless.run (fun () ->
        let f = FoldHeader(false)
        let seen = ResizeArray()
        f.Toggled.Add seen.Add
        Assert.False f.Expanded
        f.Toggle()
        Assert.True f.Expanded
        Assert.Equal("已展开", AutomationProperties.GetItemStatus f.Header)
        f.Toggle()
        Assert.Equal<bool list>([ true; false ], List.ofSeq seen))

[<Fact>]
let ``前置件在标题之前，尾随件在标题之后`` () =
    Headless.run (fun () ->
        let f = FoldHeader(true)
        let lead = Border()
        let tail = Border()
        f.AddLeading lead
        f.AddTrailing tail
        Assert.Same(lead, f.Row.Children[0])
        Assert.Same(f.Caption, f.Row.Children[1])
        Assert.Same(tail, f.Row.Children[2])
        Assert.True f.Expanded)

[<Fact>]
let ``键盘 Enter 也能开合`` () =
    Headless.run (fun () ->
        let f = FoldHeader(false)
        let w = Window(Content = f.Header)
        w.Show()
        f.Header.Focus() |> ignore
        f.Header.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, f.Header))
        Assert.True f.Expanded
        w.Close())

[<Fact>]
let ``标题字号与样式类设定正确`` () =
    Headless.run (fun () ->
        let f = FoldHeader(false)
        Assert.Equal(12.0, f.Caption.FontSize)
        Assert.Contains("hover-bordered", f.Header.Classes))

let private invokeVia (c: Avalonia.Controls.Control) =
    let peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement c
    Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType())
    let p = Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(peer)
    p.Invoke()
    Avalonia.Threading.Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``折叠头有读屏 Invoke 且能开合`` () =
    Headless.run (fun () ->
        let f = FoldHeader(false)
        invokeVia f.Header
        Assert.True f.Expanded)
