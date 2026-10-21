module Wanxiang2.ModelChip.Tests.Tests

open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Threading
open Xunit
open Wanxiang2.ModelChip
open Wanxiang2.ModelChip.Tests

[<Fact>]
let ``标签换色；读屏 Invoke 打开切换器`` () =
    Headless.run (fun () ->
        let c = ModelChip()
        let mutable opened = false

        c.OnOpen(fun anchor ->
            opened <- true
            Assert.Same(c.Host, anchor))

        Assert.Equal("未选择模型", c.Text)
        c.ApplyLabel "gpt-test"
        Assert.Equal("gpt-test", c.Text)
        let w = Window(Content = c.Host)
        w.Show()
        let peer = ControlAutomationPeer.CreatePeerForElement c.Host
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType())
        ((peer :> obj) :?> IInvokeProvider).Invoke()
        Dispatcher.UIThread.RunJobs()
        Assert.True opened
        w.Close())
