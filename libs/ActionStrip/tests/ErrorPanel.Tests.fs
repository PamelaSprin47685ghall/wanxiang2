module Wanxiang2.ErrorPanel.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.ErrorPanel
open Wanxiang2.ErrorPanel.Tests
let private revealed = ResizeArray<bool>()
let private parts =
    { Copy = fun tip _ _ -> Border(Tag = tip) :> Control
      Reveal = fun c v -> (Border(Child = c, IsVisible = v) :> Control), revealed.Add
      CountdownText = fun s -> if s = 0 then "现在可以重试了。" else sprintf "%d 秒后可重试。" s }

let private label (b: Button) = b.Content.ToString()

[<Fact>]
let ``有半截回复时继续在前、重试降为幽灵档`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts, "生成失败", "稍后再试")
        let mutable go = 0
        p.AddContinue(fun () -> go <- go + 1) |> ignore
        let retry = p.AddRetry(true, ignore)
        Assert.Equal("继续生成", label (p.Actions.Children[0] :?> Button))
        Assert.Contains("ghost", retry.Classes)
        Assert.Equal("丢弃已写出的部分，重新生成", AutomationProperties.GetHelpText retry))

[<Fact>]
let ``倒计时未结束时重试不可按并显示剩余秒数`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts, "t", "h")
        let retry = p.AddRetry(false, ignore)
        p.SetSeconds 12
        Assert.False retry.IsEnabled
        Assert.Equal("重试（12 秒）", label retry)
        Assert.Equal("重试生成，12 秒后可用", AutomationProperties.GetName retry)
        Assert.False p.Countdown.IsVisible
        Assert.Equal("重试倒计时：12 秒后可重试。", AutomationProperties.GetName p.Countdown)
        p.SetSeconds(0, true)
        Assert.True retry.IsEnabled
        Assert.Equal("重试", label retry)
        Assert.True p.Countdown.IsVisible
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting p.Countdown))

[<Fact>]
let ``没有倒计时的重试照常可按`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts, "t", "h")
        let mutable hits = 0
        let retry = p.AddRetry(false, fun () -> hits <- hits + 1)
        Assert.True retry.IsEnabled
        Assert.Contains("secondary", retry.Classes)
        Assert.Equal("重新尝试生成", AutomationProperties.GetHelpText retry))

[<Fact>]
let ``技术细节开合并改文案`` () =
    Headless.run (fun () ->
        revealed.Clear()
        let p = ErrorPanel(parts, "t", "h")
        let fold = p.AddDetail "stack"
        Assert.Equal("技术细节", AutomationProperties.GetName fold)
        fold.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, fold))
        Assert.Equal("收起技术细节", AutomationProperties.GetName fold)
        Assert.Equal("收起错误技术细节", AutomationProperties.GetHelpText fold)
        Assert.Equal<bool list>([ true ], List.ofSeq revealed))

[<Fact>]
let ``只在挂上且要求时按间隔回调`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts, "t", "h")
        p.SetTicking true
        Assert.False p.IsTicking
        let w = Window(Content = p.Host)
        w.Show()
        Assert.True p.IsTicking
        p.SetTicking false
        Assert.False p.IsTicking
        p.SetTicking true
        w.Content <- null
        Assert.False p.IsTicking
        w.Close())

let private invokeVia (c: Avalonia.Controls.Control) =
    let peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement c
    Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType())
    let p = Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(peer)
    p.Invoke()
    Avalonia.Threading.Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``错误卡各按钮有读屏 Invoke 且执行动作`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts, "t", "h")
        let log = ResizeArray<string>()
        let c = p.AddContinue(fun () -> log.Add "c")
        let r = p.AddRetry(true, fun () -> log.Add "r")
        let v = p.AddRecovery("打开设置", fun () -> log.Add "v")
        let d = p.AddDetail "x"
        for b in [ c; r; v; d ] do invokeVia b
        Assert.Equal<string list>([ "c"; "r"; "v" ], List.ofSeq log)
        Assert.Equal("收起技术细节", AutomationProperties.GetName d))
