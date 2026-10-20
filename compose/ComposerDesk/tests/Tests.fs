module Wanxiang2.ComposerDesk.Tests.Tests

// 真实组合：主题外观 + ComposerBox / AttachmentTray / ModelChip + ComposerSendState / ModelVision。
// 全部经单入口 Render(ComposerVS) 驱动：门控、附件、看图、生成态都由 VS 投影决定，测试不再手播 ApplySend。

open System
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.LogicalTree
open Avalonia.Media.Imaging
open Avalonia.Threading
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ComposerDesk
open Wanxiang2.ComposerDeskTests
open Wanxiang2.WorkspaceState

let private rig () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let styles = ThemeStyles(theme)
    styles.Install Avalonia.Application.Current.Styles
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
    let desk = ComposerDesk(actions)
    let w = Window(Width = 800.0, Height = 300.0, Content = desk.Host)
    // 主题资源字典装进窗口：输入区留白走动态资源键（Margin.Composer / Margin.ComposerCompact）
    w.Resources.MergedDictionaries.Add(theme.Resources :> Avalonia.Controls.IResourceDictionary)
    w.Show()
    Dispatcher.UIThread.RunJobs()
    desk, sent, removed, w

let private att (id: Guid) ready failed mt =
    { attachmentId = id; sha256 = "x"; size = 2048L; mediaType = mt; fileName = "a.png"; ready = ready; failed = failed }

let private attVS (item: PendingAttachment) : AttachmentVS =
    { Id = item.attachmentId
      FileName = item.fileName
      MediaType = item.mediaType
      Size = item.size
      ProgressPercent = 100
      Ready = item.ready
      Failed = item.failed }

let private emptyComposerVS () =
    (Workspace.project (Workspace.init ())).Composer

let rec private texts (c: Control) : string list =
    [ match c with
      | :? TextBlock as t -> t.Text
      | _ -> ()
      for x in c.GetLogicalChildren() do
          match x with
          | :? Control as cc -> yield! texts cc
          | _ -> () ]

let rec private names (c: Control) : string list =
    (match AutomationProperties.GetName c with
     | null -> []
     | "" -> []
     | n -> [ n ])
    @ [ for x in c.GetLogicalChildren() do
          match x with
          | :? Control as cc -> yield! names cc
          | _ -> () ]

// 迁自 ComposerTests：上传中不能发送，发送键提示说明原因；就绪后可发。
[<Fact>]
let ``上传中挡住发送，就绪后可以发送`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        let a = att (Guid.NewGuid()) false false "image/png"
        desk.Render { emptyComposerVS () with Enabled = true; Text = "你好"; Tray = [ attVS a ] }
        Assert.False desk.Box.SendButton.IsEnabled
        Assert.Equal("附件上传中，请稍候…", ToolTip.GetTip(desk.Box.SendButton) :?> string)
        desk.Render { emptyComposerVS () with Enabled = true; Text = "你好"; Tray = [ attVS { a with ready = true } ] }
        Assert.True desk.Box.SendButton.IsEnabled
        Assert.Equal(1, desk.Tray.Count)
        w.Close())

// 迁自 ComposerTests（看图受阻）：不能看图的模型（VS 事实）带图时禁用发送并显示说明；
// 同一帧只把 Vision 换回未知即放行——门控不再有本地看图副本。
[<Fact>]
let ``不能看图的模型带图时挡住发送`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        let a = att (Guid.NewGuid()) true false "image/png"
        desk.Render { emptyComposerVS () with Enabled = true; Text = "你好"; Tray = [ attVS a ]; Vision = VisionNo; VisionLabel = "X" }
        Assert.False desk.Box.SendButton.IsEnabled
        Assert.Equal(ModelVision.blockedMessage "X", ToolTip.GetTip(desk.Box.SendButton) :?> string)
        Assert.Contains(ModelVision.blockedMessage "X", texts desk.Box.Host)
        desk.Render { emptyComposerVS () with Enabled = true; Text = "你好"; Tray = [ attVS a ]; Vision = VisionUnknown; VisionLabel = "" }
        Assert.True desk.Box.SendButton.IsEnabled
        Assert.DoesNotContain(ModelVision.blockedMessage "X", texts desk.Box.Host)
        w.Close())

// 迁自 ComposerTests：模型芯片的无障碍名与提示固定为切换模型，标签更新只改显示字。
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
        let a = att (Guid.NewGuid()) false true "text/plain"
        desk.Render { emptyComposerVS () with Enabled = true; Text = "hi"; Tray = [ attVS a ] }
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

// 生成态投影自 Send.State：GeneratingStop 后 IsGenerating 为真、主键变停止；Idle 回落。
[<Fact>]
let ``生成中主键是停止`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        desk.Render { emptyComposerVS () with Enabled = false; Send = { State = SendButtonState.GeneratingStop; Label = "停止" } }
        Assert.True desk.IsGenerating
        Assert.True desk.Box.SendButton.IsEnabled
        Assert.Equal("停止生成", AutomationProperties.GetName desk.Box.SendButton)
        desk.Render { emptyComposerVS () with Enabled = true; Send = { State = SendButtonState.Idle; Label = "发送" } }
        Assert.False desk.IsGenerating
        w.Close())

// 在途命令（缩略图 / 缩小说明）不改业务事实，只补呈现：chip 的读屏名带说明，帧外可见。
[<Fact>]
let ``缩小说明并入附件 chip`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        let id = Guid.NewGuid()
        desk.Render { emptyComposerVS () with Enabled = true; Text = "hi"; Tray = [ attVS (att id true false "image/png") ] }
        desk.ApplyShrinkNote(id, "已缩小到 1024px")
        Assert.Equal("已缩小到 1024px", desk.ShrinkNote id)
        let chips = desk.Tray.Strip.Children
        Assert.Equal(1, chips.Count)
        let chip = chips.[0] :?> Border
        Assert.Equal(sprintf "附件：a.png（2.0 KiB），已缩小到 1024px", AutomationProperties.GetName chip)
        Assert.False(desk.HasThumbnail id)
        // 已解码缩略图（宿主命令）并入 chip：图片预览名出现在 chip 子树
        use img = new WriteableBitmap(Avalonia.PixelSize(64, 64), Avalonia.Vector(96.0, 96.0), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul)
        desk.ApplyThumbnail(id, img :> Avalonia.Media.IImage)
        Dispatcher.UIThread.RunJobs()
        Assert.True(desk.HasThumbnail id)
        Assert.Contains("图片预览 a.png", names desk.Tray.Host)
        w.Close())

// 单入口幂等：同一帧连渲两次，呈现与一次渲染完全一致（无累积状态）。
[<Fact>]
let ``同一帧重复渲染幂等`` () =
    Headless.run (fun () ->
        let desk, sent, _, w = rig ()
        let frame =
            { emptyComposerVS () with
                Enabled = true
                Text = "你好"
                EnterSends = false
                Tray = [ attVS (att (Guid.NewGuid()) true false "image/png") ]
                Vision = VisionNo
                VisionLabel = "X" }
        desk.Render frame
        let tip1 = ToolTip.GetTip desk.Box.SendButton :?> string
        desk.Render frame
        Assert.Equal(tip1, ToolTip.GetTip desk.Box.SendButton :?> string)
        Assert.Equal(ModelVision.blockedMessage "X", tip1)
        Assert.Equal(1, desk.Tray.Count)
        Assert.Equal<string list>([], List.ofSeq sent)
        w.Close())

// 输入区留白：主题部分走主题派生资源键（宽屏 Margin.Composer：顶=Space.X2l、底=Space.Xl；
// 窄屏 Margin.ComposerCompact：左右=Space.Xl、底=Space.Md），窄屏档切换时换绑对应键。
// 水平档随自身宽度取（LayoutPolicy.horizontalInset），落在 Padding 上。
[<Fact>]
let ``输入区留白跟随窄屏档切换`` () =
    Headless.run (fun () ->
        let desk, _, _, w = rig ()
        let host = desk.Host :?> Border
        Assert.Equal(Spacing.space2xl, host.Margin.Top)
        Assert.Equal(Spacing.spaceXl, host.Margin.Bottom)
        Assert.Equal(0.0, host.Margin.Left)
        desk.Render { emptyComposerVS () with Compact = true }
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(Spacing.spaceXl, host.Margin.Left)
        Assert.Equal(Spacing.spaceXl, host.Margin.Top)
        Assert.Equal(Spacing.spaceMd, host.Margin.Bottom)
        Assert.Equal(0.0, host.Padding.Left)
        w.Close())
