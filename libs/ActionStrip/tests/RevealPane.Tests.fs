module Wanxiang2.RevealPane.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Xunit
open Wanxiang2.RevealPane
open Wanxiang2.RevealPane.Tests

let private show (c: Control) =
    let w = Window(Width = 400.0, Height = 800.0, Content = c)
    w.Show()
    w.UpdateLayout()
    Avalonia.Threading.Dispatcher.UIThread.RunJobs()
    w.UpdateLayout()
    w

let private tall () = Border(Height = 900.0, Width = 100.0)

[<Fact>]
let ``短内容不出展开按钮`` () =
    Headless.run (fun () ->
        let p = RevealPane(Border(Height = 50.0), 360.0, true)
        let w = show p.Host
        Assert.False p.Toggle.IsVisible
        w.Close())

[<Fact>]
let ``超高内容出按钮，展开后不限高，再按收起`` () =
    Headless.run (fun () ->
        let p = RevealPane(tall (), 360.0, true)
        let w = show p.Host
        Assert.True p.Toggle.IsVisible
        Assert.Equal(360.0, p.Scroller.MaxHeight)
        p.ToggleFull()
        Assert.True p.IsFull
        Assert.True(Double.IsPositiveInfinity p.Scroller.MaxHeight)
        Assert.Equal("收起", Automation.AutomationProperties.GetName p.Toggle)
        p.ToggleFull()
        Assert.Equal("展开全部", Automation.AutomationProperties.GetName p.Toggle)
        w.Close())

[<Fact>]
let ``隐藏时退出全文态`` () =
    Headless.run (fun () ->
        let p = RevealPane(tall (), 360.0, true)
        let w = show p.Host
        p.ToggleFull()
        p.SetVisible false
        Assert.False p.IsFull
        Assert.False p.Host.IsVisible
        Assert.Equal(360.0, p.Scroller.MaxHeight)
        w.Close())

[<Fact>]
let ``起始隐藏时首次布局也不亮按钮`` () =
    Headless.run (fun () ->
        let p = RevealPane(tall (), 360.0, false)
        let w = show p.Host
        Assert.False p.Toggle.IsVisible
        p.SetVisible true
        w.UpdateLayout()
        Avalonia.Threading.Dispatcher.UIThread.RunJobs()
        Assert.True p.Toggle.IsVisible
        w.Close())

[<Fact>]
let ``视口安全留白为 8.0`` () =
    Headless.run (fun () ->
        let p = RevealPane(tall (), 360.0, true)
        Assert.Equal(8.0, p.Scroller.Margin.Right)
        Assert.Contains("ghost", p.Toggle.Classes))

let private invokeVia (c: Avalonia.Controls.Control) =
    let peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement c
    Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType())
    let p = Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(peer)
    p.Invoke()
    Avalonia.Threading.Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``展开按钮有读屏 Invoke 且能切换`` () =
    Headless.run (fun () ->
        let p = RevealPane(tall (), 360.0, true)
        let w = show p.Host
        invokeVia p.Toggle
        Assert.True p.IsFull
        w.Close())
