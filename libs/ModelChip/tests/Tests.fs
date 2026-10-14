module Wanxiang2.ModelChip.Tests.Tests

open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ModelChip
open Wanxiang2.ModelChip.Tests

let private look () =
    { Ink =
        function
        | ChipInk.Text -> Brushes.Black :> IBrush
        | ChipInk.TextMuted -> Brushes.Gray
        | ChipInk.Hairline -> Brushes.Silver
      Gauge = (fun _ -> Cell 12.0 :> _)
      BodyFont = Cell FontFamily.Default
      Dress = ignore }

[<Fact>]
let ``标签换色；读屏 Invoke 打开切换器`` () =
    Headless.run (fun () ->
        let c = ModelChip(look ())
        let mutable opened = false
        c.OnOpen(fun anchor ->
            opened <- true
            Assert.Same(c.Host, anchor))
        Assert.Equal("未选择模型", c.Text)
        Assert.Same(Brushes.Gray, c.Host.Child :?> TextBlock |> fun t -> t.Foreground)
        c.SetLabel "gpt-test"
        Assert.Equal("gpt-test", c.Text)
        Assert.Same(Brushes.Black, (c.Host.Child :?> TextBlock).Foreground)
        let w = Window(Content = c.Host)
        w.Show()
        let peer = ControlAutomationPeer.CreatePeerForElement c.Host
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType())
        ((peer :> obj) :?> IInvokeProvider).Invoke()
        Dispatcher.UIThread.RunJobs()
        Assert.True opened
        w.Close())
