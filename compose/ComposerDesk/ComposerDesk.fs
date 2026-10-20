namespace Wanxiang2.ComposerDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Platform.Storage
open Wanxiang.UI
open Wanxiang2.ComposerBox
open Wanxiang2.AttachmentTray
open Wanxiang2.ModelChip

/// 输入区对外的动作（旧 ComposerActions）
type ComposerDeskActions =
    { /// true 表示内容已由本地待发送记录接管
      Submit: string -> bool
      StopGeneration: unit -> unit
      PickAttachment: unit -> unit
      RemoveAttachment: Guid -> unit
      OpenModelPicker: Control -> unit
      DropFiles: IStorageItem list -> unit
      PasteFromClipboard: unit -> bool
      OpenSessionSettings: unit -> unit }

/// 组合者：ComposerBox + AttachmentTray + ModelChip，发送门控走 ComposerSendState，看图判断走 ModelVision（旧 Views/Composer.fs）。
/// 业务事实零本地副本：一切从 Render 收到的 ComposerVS 投影（在途态清单见本库 AGENTS.md）。
type ComposerDesk(actions: ComposerDeskActions) =
    let box = ComposerBox()
    let tray = AttachmentTray()
    let chip = ModelChip()
    let pending = PendingPanel()
    let thumbs = Collections.Generic.Dictionary<Guid, IImage>()
    let notes = Collections.Generic.Dictionary<Guid, string>()
    /// 在途（登记 AGENTS.md）：最近一帧 ComposerVS。门控不止在 Render 时算——
    /// 文本变化（TextChanged / Text 属性）与非每帧命令（缩略图、缩小说明、草稿回填）都要按最新帧重算，
    /// 这里存的就是「最新帧」本身，是帧的缓存不是业务事实的第二权威（与 SidebarDesk.lastVS 同一口径）。
    let mutable last: Wanxiang2.WorkspaceState.ComposerVS =
        (Wanxiang2.WorkspaceState.Workspace.project (Wanxiang2.WorkspaceState.Workspace.init ())).Composer
    /// 输入区四周留白：主题部分走动态资源键（宽屏 Margin.Composer：顶=Space.X2l、底=Space.Xl；
    /// 窄屏 Margin.ComposerCompact：左右=Space.Xl、底=Space.Md），水平档随自身宽度取
    /// （LayoutPolicy.horizontalInset，旧 ApplyComposerPadding）。本组合者不再订阅主题活值。
    let host = box.Host :?> Border
    /// 在途（登记 AGENTS.md）：已套用的留白档（None = 还没套）；非业务事实，只是避免重复换绑。
    let mutable paddedCompact: bool option = None
    let mutable marginBinding: IDisposable option = None

    let applyPadding () =
        let compact = last.Compact
        if paddedCompact <> Some compact then
            paddedCompact <- Some compact
            marginBinding |> Option.iter (fun d -> d.Dispose())
            let key = if compact then "Margin.ComposerCompact" else "Margin.Composer"
            marginBinding <- Some (host.Bind(Layoutable.MarginProperty, Dyn.res key) :> IDisposable)
        // 宽屏水平留白按自身宽度取档；窄屏的左右留白由 Margin.ComposerCompact 给
        let inset = if compact then 0.0 else LayoutPolicy.horizontalInset host.Bounds.Width
        host.Padding <- Thickness(inset, 0.0, inset, 0.0)

    let facts (a: Wanxiang2.WorkspaceState.AttachmentVS) : ComposerAttachment =
        let stage =
            if RestoredAttachments.isRestored a.Id then AttachmentStage.Restored
            elif a.Failed then AttachmentStage.Failed
            elif a.Ready then AttachmentStage.Ready
            else AttachmentStage.Uploading
        let kind = if MediaTypes.isImage a.MediaType then AttachmentKind.Image else AttachmentKind.OtherFile
        { stage = stage; kind = kind }

    let hostOf (vs: Wanxiang2.WorkspaceState.ComposerVS) =
        if vs.Enabled then HostAvailable else HostDisabled(vs.DisabledReason |> Option.defaultValue "")

    let generationOf (vs: Wanxiang2.WorkspaceState.ComposerVS) =
        match vs.Send.State with
        | Wanxiang2.WorkspaceState.SendButtonState.GeneratingStop -> Generating CanStop
        | _ -> Idle

    let visionOf (vs: Wanxiang2.WorkspaceState.ComposerVS) =
        if vs.Vision = VisionNo then VisionForbidden (ModelVision.blockedMessage vs.VisionLabel)
        else VisionAllowed

    let refresh () =
        let v =
            ComposerSendState.decide
                { host = hostOf last
                  generation = generationOf last
                  text = if String.IsNullOrWhiteSpace box.Text then EmptyText else HasText
                  attachments = last.Tray |> List.map facts
                  vision = visionOf last
                  sendKey = if last.EnterSends then EnterSends else CtrlEnterSends }
        box.ApplySend
            { VisionBlocked = v.VisionBlocked
              VisionMessage = v.VisionMessage
              SendEnabled = v.SendEnabled
              QueueEnabled = v.QueueEnabled
              QueueVisible = v.QueueVisible
              SendTip = v.SendTip
              SendLabel = v.SendLabel
              QueueTip = v.QueueTip }

    let trayItem (a: Wanxiang2.WorkspaceState.AttachmentVS) : TrayItem =
        let restored = RestoredAttachments.isRestored a.Id
        let f = facts a
        let size = AttachmentRef.formatSize a.Size
        let note = match notes.TryGetValue a.Id with | true, n -> Some n | _ -> None
        { Id = a.Id
          FileName = a.FileName
          SizeText = size
          StateText = ComposerSendState.chipState f.stage size
          Tip = ComposerSendState.chipTip f.stage size
          Name =
            if restored then sprintf "附件需重新选择：%s，可移除或重新添加这个文件" a.FileName
            elif a.Failed then sprintf "附件上传失败：%s（%s），可移除或重新添加" a.FileName size
            else
                match note with
                | Some n -> sprintf "附件：%s（%s），%s" a.FileName size n
                | None -> sprintf "附件：%s（%s）" a.FileName size
          Failed = a.Failed
          Ready = a.Ready
          IsImage = (f.kind = AttachmentKind.Image)
          ShrinkNote = note
          Thumbnail = match thumbs.TryGetValue a.Id with | true, t -> Some t | _ -> None }

    let renderItems () = tray.Render(last.Tray |> List.map trayItem, last.Compact)

    let submit (text: string) =
        if ComposerSendState.canSubmit
                (hostOf last)
                (if String.IsNullOrWhiteSpace text then EmptyText else HasText)
                (last.Tray |> List.map facts) then
            if actions.Submit text then
                box.ClearAfterSubmit text
                refresh ()

    do
        box.SlotPending pending.Host
        applyPadding ()
        host.PropertyChanged.Add(fun e -> if e.Property = Visual.BoundsProperty && not last.Compact then applyPadding ())
        box.SlotTray tray.Host
        box.SlotModel chip.Host
        chip.OnOpen actions.OpenModelPicker
        tray.OnRemove actions.RemoveAttachment
        box.Submitted.Add submit
        box.Stopped.Add(fun () -> actions.StopGeneration())
        box.AttachAsked.Add(fun () -> actions.PickAttachment())
        box.Dropped.Add actions.DropFiles
        box.ContextOpened.Add(fun () -> actions.OpenSessionSettings())
        box.WirePaste actions.PasteFromClipboard
        box.Input.TextChanged.Add(fun _ -> refresh ())
        // TextChanged 晚于 Text 属性派发：属性一变就同步重算门控，写字后立即回车不读旧门控
        box.Input.PropertyChanged.Add(fun e -> if e.Property = TextBox.TextProperty then refresh ())
        refresh ()

    member _.Host: Control = box.Host
    member _.Box = box
    member _.Tray = tray
    member _.Chip = chip
    member _.Text = box.Text
    member _.IsGenerating = (generationOf last <> Idle)
    member _.ModelAnchor() : Control = chip.Host :> Control

    /// 单入口幂等渲染：由 ComposerVS 投影全量刷新
    member this.Render(vs: Wanxiang2.WorkspaceState.ComposerVS) =
        last <- vs
        // 输入壳的启用/文本/插入点/回车档/窄屏/发送键基础态/会话上下文摘要都在 ComposerBox.Render 里投影
        box.Render vs
        chip.Render(vs.ModelChip, vs.Compact)
        // 芯片的无障碍名与提示固定为「切换本对话使用的模型」：标签渲染会覆盖，投影后统一钉回
        Avalonia.Automation.AutomationProperties.SetName(chip.Host, "切换本对话使用的模型")
        ToolTip.SetTip(chip.Host, "切换本对话使用的模型")
        chip.Host.IsEnabled <- vs.Enabled
        chip.Host.Focusable <- vs.Enabled
        chip.Host.IsHitTestVisible <- vs.Enabled
        renderItems ()
        applyPadding ()
        refresh ()

    /// 图片缩略图（已解码；宿主负责解码与释放。非每帧命令：宿主侧解码结果，VS 不承载）
    member _.ApplyThumbnail(attachmentId: Guid, image: IImage) =
        thumbs.[attachmentId] <- image
        if last.Tray |> List.exists (fun a -> a.Id = attachmentId) then renderItems ()

    member _.HasThumbnail(attachmentId: Guid) = thumbs.ContainsKey attachmentId

    /// 图片压缩说明（非每帧命令：上传管线就地生成，VS 不承载）
    member _.ApplyShrinkNote(attachmentId: Guid, note: string) =
        notes.[attachmentId] <- note
        if last.Tray |> List.exists (fun a -> a.Id = attachmentId) then renderItems ()

    member _.ShrinkNote(attachmentId: Guid) =
        match notes.TryGetValue attachmentId with
        | true, n -> n
        | _ -> ""

    /// 未确认消息（宿主直注插槽，非每帧投影）
    member _.SlotPendingMessages(items: PendingMessage list, activeConversationId: Guid option, canRetry: bool, retry: Guid -> unit, openConversation: Guid -> unit) =
        pending.Render(items, activeConversationId, canRetry, retry, openConversation)

    member _.Pending = pending

    member _.SlotAccessory(c: Control) = box.SlotAccessory c
    /// 同步提交（旧 Composer.Submit）：门控通过才交宿主，宿主接管才清空；宿主异常照常抛出
    member _.Submit() = box.Submit()

    /// 用户草稿回填（一次性命令，非每帧投影）
    member _.ApplyDraftText(text: string) =
        box.ApplyDraftText text
        refresh ()
    member _.Focus() = box.Focus()
    member _.WireImeProbe(f) = box.WireImeProbe f
    member _.WireKeyHook(f) = box.WireKeyHook f
