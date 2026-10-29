module Wanxiang2.ErrorPanel.Tests.Tests

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.ErrorPanel
open Wanxiang2.ErrorPanel.Tests
open Wanxiang2.WorkspaceState

let private revealed = ResizeArray<bool>()

let private parts =
    {
        Copy = fun tip _ _ -> Border(Tag = tip) :> Control
        Reveal = fun c v -> (Border(Child = c, IsVisible = v) :> Control), revealed.Add
        CountdownText = fun s -> if s = 0 then "现在可以重试了。" else sprintf "%d 秒后可重试。" s
    }

let private label (b: Button) = b.Content.ToString()

/// 默认视图状态：标题、说明、无倒计时
let private vsOf (actions: ErrorPanelAction list) =
    {
        Title = "生成失败"
        Hint = "稍后再试"
        Detail = None
        CountdownSeconds = -1
        Ticking = false
        Announce = false
        Actions = actions
        OnRetry = ignore
        OnContinue = None
        OnRecovery = None
    }

[<Fact>]
let ``有半截回复时继续在前、重试降为幽灵档`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts)

        p.Render
            { (vsOf [ ErrorPanelAction.Continue; ErrorPanelAction.Retry ]) with
                OnContinue = Some ignore
            }

        Assert.Equal("继续生成", label (p.Actions.Children[0] :?> Button))
        let retry = p.Actions.Children[1] :?> Button
        Assert.Contains("ghost", retry.Classes)
        Assert.Equal("丢弃已写出的部分，重新生成", AutomationProperties.GetHelpText retry))

[<Fact>]
let ``倒计时未结束时重试不可按并显示剩余秒数`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts)

        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 12
            }

        let retry = p.Actions.Children[0] :?> Button
        Assert.False retry.IsEnabled
        Assert.Equal("重试（12 秒）", label retry)
        Assert.Equal("重试生成，12 秒后可用", AutomationProperties.GetName retry)
        Assert.False p.Countdown.IsVisible
        Assert.Equal("重试倒计时：12 秒后可重试。", AutomationProperties.GetName p.Countdown)

        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 0
                Announce = true
            }

        Assert.True retry.IsEnabled
        Assert.Equal("重试", label retry)
        Assert.True p.Countdown.IsVisible
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting p.Countdown))

[<Fact>]
let ``没有倒计时的重试照常可按`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts)
        p.Render(vsOf [ ErrorPanelAction.Retry ])
        let retry = p.Actions.Children[0] :?> Button
        Assert.True retry.IsEnabled
        Assert.Contains("secondary", retry.Classes)
        Assert.Equal("重新尝试生成", AutomationProperties.GetHelpText retry))

[<Fact>]
let ``技术细节开合并改文案`` () =
    Headless.run (fun () ->
        revealed.Clear()
        let p = ErrorPanel(parts)
        p.Render { (vsOf []) with Detail = Some "stack" }
        let fold = p.Actions.Children[0] :?> Button
        Assert.Equal("技术细节", AutomationProperties.GetName fold)
        fold.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, fold))
        Assert.Equal("收起技术细节", AutomationProperties.GetName fold)
        Assert.Equal("收起错误技术细节", AutomationProperties.GetHelpText fold)
        Assert.Equal<bool list>([ true ], List.ofSeq revealed)
        revealed.Clear()
        p.Render { (vsOf []) with Detail = None }
        Assert.Empty p.Actions.Children)

[<Fact>]
let ``只在挂上且要求时按间隔回调`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts)
        p.Render { (vsOf []) with Ticking = true }
        Assert.False p.IsTicking
        let w = Window(Content = p.Host)
        w.Show()
        Assert.True p.IsTicking
        p.Render { (vsOf []) with Ticking = false }
        Assert.False p.IsTicking
        p.Render { (vsOf []) with Ticking = true }
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
        let p = ErrorPanel(parts)
        let log = ResizeArray<string>()

        p.Render
            { (vsOf
                  [
                      ErrorPanelAction.Continue
                      ErrorPanelAction.Retry
                      ErrorPanelAction.Recovery "打开设置"
                  ]) with
                Detail = Some "x"
                OnContinue = Some(fun () -> log.Add "c")
                OnRetry = fun () -> log.Add "r"
                OnRecovery = Some(fun _ -> log.Add "v")
            }

        let c = p.Actions.Children[0] :?> Button
        let r = p.Actions.Children[1] :?> Button
        let v = p.Actions.Children[2] :?> Button
        let d = p.Actions.Children[3] :?> Button

        for b in [ c; r; v; d ] do
            invokeVia b

        Assert.Equal<string list>([ "c"; "r"; "v" ], List.ofSeq log)
        Assert.Equal("收起技术细节", AutomationProperties.GetName d))

/// 语义钉桩（一）：卡片重建复用同一个面板实例，剩余秒数只往下走，不回跳
[<Fact>]
let ``同一面板实例先后渲染两次剩余秒数不回跳`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts)

        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 12
            }

        Assert.Equal(12, p.Left)
        // 卡片重建：同一实例再渲染一次，剩余秒数不变
        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 12
            }

        Assert.Equal(12, p.Left)
        // 时间往前走，秒数变小
        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 5
            }

        Assert.Equal(5, p.Left)
        // 再次重建（旧的快照仍是 12 秒）不回跳
        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 12
            }

        Assert.Equal(5, p.Left)
        let retry = p.Actions.Children[0] :?> Button
        Assert.Equal("重试（5 秒）", label retry))

/// 语义钉桩（二）：播报（polite 读屏）只在倒计时第一次走完时发生一次
[<Fact>]
let ``播报只在倒计时第一次走完时发生一次`` () =
    Headless.run (fun () ->
        let p = ErrorPanel(parts)
        let spoken = ResizeArray<unit>()
        p.Announced.Add(fun () -> spoken.Add())

        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 12
            }

        Assert.Empty spoken

        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 0
                Announce = true
            }

        Assert.Single spoken
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting p.Countdown)
        // 卡片重建后再播一次也不该重复（同一段倒计时只播一次）
        p.Render
            { (vsOf [ ErrorPanelAction.Retry ]) with
                CountdownSeconds = 0
                Announce = true
            }

        Assert.Single spoken)
