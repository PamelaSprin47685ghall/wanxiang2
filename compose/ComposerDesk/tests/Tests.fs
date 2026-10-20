module Wanxiang2.ComposerDesk.Tests.Tests

// 真实组合：主题外观 + ComposerBox / AttachmentTray / ModelChip + ComposerSendState / ModelVision。

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Threading
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ComposerDesk
open Wanxiang2.ComposerDeskTests

let private rig () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let styles = ThemeStyles(theme)
    let sent = ResizeArray<string>()
    let removed = ResizeArray<Guid>()
    let actions =
        { Submit = (fun t -> sent.Add t; true)
          StopGeneration = ignore
          PickAttachment = ignore
          RemoveAttachment = removed.Add
          OpenModelPicker = ignore
          DropFiles = ignore
          PasteFromClipboard = (fun () -> false)
          OpenSessionSettings = ignore }
    let desk = ComposerDesk(ComposerMetrics.ofTheme theme styles, actions)
    let w = Window(Width = 800.0, Height = 300.0, Content = desk.Host)
    w.Show()
    Dispatcher.UIThread.RunJobs()
    desk, sent, removed, w

let private att ready failed mt =
    { attachmentId = Guid.NewGuid(); sha256 = "x"; size = 2048L; mediaType = mt; fileName = "a.png"; ready = ready; failed = failed }

let private emptyComposerVS () =
    (Wanxiang2.WorkspaceState.Workspace.project (Wanxiang2.WorkspaceState.Workspace.init ())).Composer

// 迁自 ComposerTests：上传中不能发送，发送键提示说明原因；就绪后可发。
[<Fact>]
let ``上传中挡住发送，就绪后可以发送`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        let a = att false false "image/png"
        let attVS (item: PendingAttachment) : Wanxiang2.WorkspaceState.AttachmentVS =
            { Id = item.attachmentId; FileName = item.fileName; MediaType = item.mediaType; Size = item.size; ProgressPercent = 100; Ready = item.ready; Failed = item.failed }
        desk.Render { emptyComposerVS () with Enabled = true; Text = "你好"; Tray = [ attVS a ]; Send = { State = Wanxiang2.WorkspaceState.SendButtonState.Idle; Label = "发送" } }
        desk.Box.ApplySend { VisionBlocked = false; VisionMessage = ""; SendEnabled = false; QueueEnabled = false; QueueVisible = false; SendTip = "附件上传中，请稍候…"; SendLabel = "发送"; QueueTip = None }
        Assert.False desk.Box.SendButton.IsEnabled
        Assert.Equal("附件上传中，请稍候…", ToolTip.GetTip(desk.Box.SendButton) :?> string)
        desk.Render { emptyComposerVS () with Enabled = true; Text = "你好"; Tray = [ attVS { a with ready = true } ] }
        Assert.True desk.Box.SendButton.IsEnabled
        Assert.Equal(1, desk.Tray.Count)
        w.Close())

// 迁自 ComposerTests（看图受阻）：不能看图的模型带图时禁用发送并显示说明。
[<Fact>]
let ``不能看图的模型带图时挡住发送`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        let a = att true false "image/png"
        let attVS (item: PendingAttachment) : Wanxiang2.WorkspaceState.AttachmentVS =
            { Id = item.attachmentId; FileName = item.fileName; MediaType = item.mediaType; Size = item.size; ProgressPercent = 100; Ready = item.ready; Failed = item.failed }
        desk.Render { emptyComposerVS () with Enabled = true; Tray = [ attVS a ] }
        desk.Box.ApplySend { VisionBlocked = true; VisionMessage = ModelVision.blockedMessage "X"; SendEnabled = false; QueueEnabled = false; QueueVisible = false; SendTip = ModelVision.blockedMessage "X"; SendLabel = "发送"; QueueTip = None }
        Assert.False desk.Box.SendButton.IsEnabled
        Assert.Equal(ModelVision.blockedMessage "X", ToolTip.GetTip(desk.Box.SendButton) :?> string)
        desk.Box.ApplySend { VisionBlocked = false; VisionMessage = ""; SendEnabled = true; QueueEnabled = false; QueueVisible = false; SendTip = ""; SendLabel = "发送"; QueueTip = None }
        Assert.True desk.Box.SendButton.IsEnabled
        w.Close())

// 迁自 ComposerTests：模型芯片的无障碍名与提示同旧代码，标签更新只改显示字。
[<Fact>]
let ``模型芯片名称固定为切换模型`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        desk.Render { emptyComposerVS () with ModelChip = Some "gpt-x" }
        Assert.Equal("切换本对话使用的模型", AutomationProperties.GetName desk.Chip.Host)
        Assert.Equal("gpt-x", desk.Chip.Text)
        w.Close())

// 迁自 ComposerTests：失败残留占一条 chip，挡住发送。
[<Fact>]
let ``附件状态字同旧 chipState`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        let a = att false true "text/plain"
        let attVS (item: PendingAttachment) : Wanxiang2.WorkspaceState.AttachmentVS =
            { Id = item.attachmentId; FileName = item.fileName; MediaType = item.mediaType; Size = item.size; ProgressPercent = 0; Ready = item.ready; Failed = item.failed }
        desk.Render { emptyComposerVS () with Enabled = true; Text = "hi"; Tray = [ attVS a ] }
        desk.Box.ApplySend { VisionBlocked = false; VisionMessage = ""; SendEnabled = false; QueueEnabled = false; QueueVisible = false; SendTip = "有附件上传失败，移除或重新添加后发送"; SendLabel = "发送"; QueueTip = None }
        Assert.Equal(1, desk.Tray.Count)
        Assert.False desk.Box.SendButton.IsEnabled
        Assert.Equal("有附件上传失败，移除或重新添加后发送", ToolTip.GetTip(desk.Box.SendButton) :?> string)
        w.Close())

// 迁自 ComposerTests：提交交给宿主，接管后清空输入框。
[<Fact>]
let ``提交走宿主动作并清空输入`` () =
    Headless.run (fun () ->
        let desk, sent, _, w = rig ()
        desk.Render { emptyComposerVS () with Enabled = true; Text = "你好" }
        let peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement desk.Box.SendButton
        ((peer :> obj) :?> Avalonia.Automation.Provider.IInvokeProvider).Invoke()
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<string list>([ "你好" ], List.ofSeq sent)
        Assert.Equal("", desk.Text)
        w.Close())
