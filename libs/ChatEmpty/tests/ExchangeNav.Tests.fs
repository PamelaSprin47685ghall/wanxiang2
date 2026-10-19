module Wanxiang2.ExchangeNav.Tests.Tests

open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ExchangeNav
open Wanxiang2.ExchangeNav.Tests

[<Fact>]
let ``端点可用态可读写；换对话清掉`` () =
    Headless.run (fun () ->
        let n = ExchangeNav()
        n.SetJumpEnds(true, false)
        Assert.True n.CanJumpPrevious
        Assert.False n.CanJumpNext
        n.Reset()
        Assert.False n.CanJumpPrevious
        Assert.False n.CanJumpNext)

[<Fact>]
let ``更早一页失败横幅：读屏 Invoke 重试并隐藏`` () =
    Headless.run (fun () ->
        let n = ExchangeNav()
        let mutable retried = false
        n.ShowOlderFailure("加载超时", fun () -> retried <- true)
        Assert.True n.OlderFailureVisible
        Assert.Equal("加载超时", n.OlderFailureText)
        let w = Window(Content = n.FailureBanner)
        w.Show()
        let peer = ControlAutomationPeer.CreatePeerForElement n.RetryButton
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType())
        Assert.Equal("重试加载更早消息", AutomationProperties.GetName n.RetryButton)
        ((peer :> obj) :?> IInvokeProvider).Invoke()
        Dispatcher.UIThread.RunJobs()
        Assert.True retried
        Assert.False n.OlderFailureVisible
        w.Close())
