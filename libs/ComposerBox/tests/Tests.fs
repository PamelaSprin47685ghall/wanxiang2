module Wanxiang2.ComposerBox.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ComposerBox
open Wanxiang2.ComposerBox.Tests

let private look () =
    { Ink = (fun _ -> SolidColorBrush(Colors.Gray) :> IBrush)
      Gauge = (fun _ -> Cell 8.0 :> _)
      BodyFont = Cell FontFamily.Default
      Glyph = (fun _ _ -> Border(Width = 10.0, Height = 10.0) :> Control)
      DressSend = ignore
      DressQueue = ignore
      DressAttach = ignore
      DressGhost = ignore }

let private ready =
    { VisionBlocked = false; VisionMessage = ""; SendEnabled = true; QueueEnabled = true; QueueVisible = false
      SendTip = "发送（Enter）"; SendLabel = "发送"; QueueTip = None }

let private invoke (b: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement b
    Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType())
    ((peer :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

let private key (c: Control) k m =
    c.RaiseEvent(KeyEventArgs(Key = k, KeyModifiers = m, RoutedEvent = InputElement.KeyDownEvent))

[<Fact>]
let ``发送、附件、停止都可经读屏 Invoke`` () =
    Headless.run (fun () ->
        let b = ComposerBox(look ())
        let log = ResizeArray<string>()
        b.Submitted.Add(fun t -> log.Add("send:" + t))
        b.AttachAsked.Add(fun () -> log.Add "attach")
        b.Stopped.Add(fun () -> log.Add "stop")
        b.SetEnabled(true, "")
        b.Input.Text <- "你好"
        b.ApplySend ready
        let w = Window(Width = 600.0, Height = 400.0, Content = b.Host)
        w.Show()
        invoke b.SendButton
        invoke b.AttachButton
        b.SetGenerating true
        b.ApplySend { ready with SendLabel = "停止生成" }
        Assert.Equal("停止生成", AutomationProperties.GetName b.SendButton)
        invoke b.SendButton
        Assert.Equal<string list>([ "send:你好"; "attach"; "stop" ], List.ofSeq log)
        w.Close())

[<Fact>]
let ``Enter 发送、Shift+Enter 换行；Ctrl+Enter 模式反过来；输入法组合中不提交`` () =
    Headless.run (fun () ->
        let b = ComposerBox(look ())
        let mutable n = 0
        b.Submitted.Add(fun _ -> n <- n + 1)
        b.SetEnabled(true, "")
        b.Input.Text <- "x"
        b.ApplySend ready
        let w = Window(Content = b.Host)
        w.Show()
        key b.Input Key.Enter KeyModifiers.None
        key b.Input Key.Enter KeyModifiers.Shift
        Assert.Equal(1, n)
        b.SetEnterSends false
        key b.Input Key.Enter KeyModifiers.None
        key b.Input Key.Enter KeyModifiers.Control
        Assert.Equal(2, n)
        b.SetImeProbe(fun _ -> true)
        key b.Input Key.Enter KeyModifiers.Control
        Assert.Equal(2, n)
        w.Close())

[<Fact>]
let ``禁用时说明条可见、附件键不可用；看图受阻挂提示`` () =
    Headless.run (fun () ->
        let b = ComposerBox(look ())
        b.SetEnabled(false, "连接已断开")
        Assert.False b.AttachButton.IsEnabled
        Assert.Equal("", b.Input.PlaceholderText)
        b.ApplySend { ready with VisionBlocked = true; VisionMessage = "当前模型不能看图"; SendEnabled = false }
        Assert.False b.SendButton.IsEnabled
        b.SetEnabled(true, "")
        Assert.True b.AttachButton.IsEnabled
        Assert.Equal("输入消息…", b.Input.PlaceholderText))

[<Fact>]
let ``Enter 发送档 Ctrl+Enter 不发送；钩子先于发送判定；排队键直达提交；异常照常抛出`` () =
    Headless.run (fun () ->
        let b = ComposerBox(look ())
        let log = ResizeArray<string>()
        b.Submitted.Add(fun t -> log.Add t)
        b.SetEnabled(true, "")
        let w = Window(Content = b.Host)
        w.Show()
        b.Input.Text <- "x"
        key b.Input Key.Enter KeyModifiers.Control
        Assert.Empty log
        b.Input.Text <- "x"
        // 门控尚未写入也照样交给宿主（宿主同步判定）
        key b.Input Key.Enter KeyModifiers.None
        Assert.Equal<string list>([ "x" ], List.ofSeq log)
        b.SetKeyHook(fun e -> e.Key = Key.Enter)
        key b.Input Key.Enter KeyModifiers.None
        Assert.Equal(1, log.Count)
        b.SetKeyHook(fun _ -> false)
        b.SetGenerating true
        b.ApplySend { ready with QueueVisible = true }
        invoke b.QueueButton
        Assert.Equal(2, log.Count)
        b.Submitted.Add(fun _ -> failwith "boom")
        Assert.ThrowsAny<exn>(fun () -> b.Submit()) |> ignore
        w.Close())

[<Fact>]
let ``窄屏收起对话上下文按钮，回到宽屏恢复`` () =
    Headless.run (fun () ->
        let b = ComposerBox(look ())
        b.ShowContext("系统指令", "详情", true)
        b.SetCompact true
        Assert.False b.ContextButton.IsVisible
        b.SetCompact false
        Assert.True b.ContextButton.IsVisible
        Assert.True(DragDrop.GetAllowDrop b.Host))
