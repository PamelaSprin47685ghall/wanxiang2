namespace Wanxiang2.ComposerDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Platform.Storage
open Wanxiang.UI
open Wanxiang2.ComposerBox
open Wanxiang2.AttachmentTray
open Wanxiang2.ModelChip
open Wanxiang2.Theme

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
type ComposerDesk(looks: ComposerMetrics, actions: ComposerDeskActions) =
    let box = ComposerBox()
    let tray = AttachmentTray()
    let chip = ModelChip()
    let pending = PendingPanel(looks.Pending)
    let thumbs = Collections.Generic.Dictionary<Guid, IImage>()
    let notes = Collections.Generic.Dictionary<Guid, string>()
    let mutable attachments: PendingAttachment list = []
    let mutable enabled = false
    let mutable generating = false
    let mutable canStop = true
    let mutable reason = ""
    let mutable enterSends = true
    let mutable vision = VisionUnknown
    let mutable visionLabel = ""
    let mutable compactMode = false
    let mutable top = 0.0
    let mutable bottom = 0.0
    let mutable md = 0.0
    /// 旧 SetCompactMode / ApplyComposerPadding：窄屏固定收紧，桌面按自身宽度取水平档
    let applyPadding () =
        (box.Host :?> Border).Padding <-
            if compactMode then Thickness(bottom, bottom, bottom, md)
            else
                let inset = LayoutPolicy.horizontalInset box.Host.Bounds.Width
                Thickness(inset, top, inset, bottom)

    let facts (a: PendingAttachment) : ComposerAttachment =
        { ready = a.ready; failed = a.failed; isImage = MediaTypes.isImage a.mediaType }

    let refresh () =
        let v =
            ComposerSendState.decide
                { enabled = enabled
                  generating = generating
                  canStop = canStop
                  disabledReason = reason
                  hasText = not (String.IsNullOrWhiteSpace box.Text)
                  attachments = attachments |> List.map facts
                  visionNo = (vision = VisionNo)
                  visionBlockedMessage = ModelVision.blockedMessage visionLabel
                  enterSends = enterSends }
        box.ApplySend
            { VisionBlocked = v.visionBlocked
              VisionMessage = (if v.visionBlocked then ModelVision.blockedMessage visionLabel else "")
              SendEnabled = v.sendEnabled
              QueueEnabled = v.queueEnabled
              QueueVisible = v.queueVisible
              SendTip = v.sendTip
              SendLabel = v.sendLabel
              QueueTip = v.queueTip }

    let trayItem (a: PendingAttachment) : TrayItem =
        let restored = RestoredAttachments.isRestored a.attachmentId
        let f = facts a
        let size = AttachmentRef.formatSize a.size
        let note = match notes.TryGetValue a.attachmentId with | true, n -> Some n | _ -> None
        { Id = a.attachmentId
          FileName = a.fileName
          SizeText = size
          StateText = ComposerSendState.chipState restored f size
          Tip = ComposerSendState.chipTip restored f size
          Name =
            if restored then sprintf "附件需重新选择：%s，可移除或重新添加这个文件" a.fileName
            elif a.failed then sprintf "附件上传失败：%s（%s），可移除或重新添加" a.fileName size
            else
                match note with
                | Some n -> sprintf "附件：%s（%s），%s" a.fileName size n
                | None -> sprintf "附件：%s（%s）" a.fileName size
          Failed = a.failed
          Ready = a.ready
          IsImage = f.isImage
          ShrinkNote = note
          Thumbnail = match thumbs.TryGetValue a.attachmentId with | true, t -> Some t | _ -> None }

    let render () = tray.SetItems(attachments |> List.map trayItem)

    let submit (text: string) =
        if ComposerSendState.canSubmit enabled text (attachments |> List.map facts) then
            if actions.Submit text then
                box.ClearAfterSubmit text
                refresh ()

    do
        box.SetPending pending.Host
        looks.Space Space.X2l |> Observable.add (fun v -> top <- v; applyPadding ())
        looks.Space Space.Xl |> Observable.add (fun v -> bottom <- v; applyPadding ())
        looks.Space Space.Md |> Observable.add (fun v -> md <- v; applyPadding ())
        box.Host.PropertyChanged.Add(fun e -> if e.Property = Visual.BoundsProperty && not compactMode then applyPadding ())
        box.SetTray tray.Host
        box.SetModel chip.Host
        chip.OnOpen actions.OpenModelPicker
        Avalonia.Automation.AutomationProperties.SetName(chip.Host, "切换本对话使用的模型")
        ToolTip.SetTip(chip.Host, "切换本对话使用的模型")
        tray.OnRemove actions.RemoveAttachment
        box.Submitted.Add submit
        box.Stopped.Add(fun () -> actions.StopGeneration())
        box.AttachAsked.Add(fun () -> actions.PickAttachment())
        box.Dropped.Add actions.DropFiles
        box.ContextOpened.Add(fun () -> actions.OpenSessionSettings())
        box.SetPaste actions.PasteFromClipboard
        box.Input.TextChanged.Add(fun _ -> refresh ())
        // TextChanged 晚于 Text 属性派发：属性一变就同步重算门控，写字后立即回车不读旧门控
        box.Input.PropertyChanged.Add(fun e -> if e.Property = TextBox.TextProperty then refresh ())
        refresh ()

    member _.Host: Control = box.Host
    member _.Box = box
    member _.Tray = tray
    member _.Chip = chip
    member _.Text = box.Text
    member _.IsGenerating = generating
    member _.ModelAnchor() : Control = chip.Host :> Control

    /// 单入口幂等渲染：由 ComposerVS 投影全量刷新
    member this.Render(vs: Wanxiang2.WorkspaceState.ComposerVS) =
        attachments <-
            vs.Tray
            |> List.map (fun a ->
                { attachmentId = a.Id
                  sha256 = ""
                  size = a.Size
                  mediaType = a.MediaType
                  fileName = a.FileName
                  ready = a.Ready
                  failed = a.Failed })
        if box.Text <> vs.Text then
            box.SetText vs.Text
        enabled <- vs.Enabled
        reason <- vs.DisabledReason |> Option.defaultValue ""
        box.SetEnabled(vs.Enabled, reason)
        chip.Host.IsEnabled <- vs.Enabled
        chip.Host.Focusable <- vs.Enabled
        chip.Host.IsHitTestVisible <- vs.Enabled
        enterSends <- vs.EnterSends
        box.SetEnterSends vs.EnterSends
        compactMode <- vs.Compact
        applyPadding ()
        box.SetCompact vs.Compact
        tray.SetCompact vs.Compact
        chip.SetCompact vs.Compact
        match vs.Send.State with
        | Wanxiang2.WorkspaceState.SendButtonState.GeneratingStop ->
            generating <- true
            canStop <- true
            box.SetGenerating true
        | _ ->
            generating <- false
            box.SetGenerating false
        match vs.ModelChip with
        | Some label ->
            chip.SetLabel label
            Avalonia.Automation.AutomationProperties.SetName(chip.Host, "切换本对话使用的模型")
            ToolTip.SetTip(chip.Host, "切换本对话使用的模型")
        | None -> ()
        match vs.Context with
        | Some ctx -> box.ShowContext(ctx.Model, ctx.ContextSummary, true)
        | None -> box.HideContext()
        tray.Render(vs.Tray, vs.Compact)
        refresh ()

    /// 图片缩略图（已解码；宿主负责解码与释放）
    member _.ApplyThumbnail(attachmentId: Guid, image: IImage) =
        thumbs.[attachmentId] <- image
        if attachments |> List.exists (fun a -> a.attachmentId = attachmentId) then render ()

    member _.HasThumbnail(attachmentId: Guid) = thumbs.ContainsKey attachmentId

    member _.ApplyShrinkNote(attachmentId: Guid, note: string) =
        notes.[attachmentId] <- note
        if attachments |> List.exists (fun a -> a.attachmentId = attachmentId) then render ()

    member _.ShrinkNote(attachmentId: Guid) =
        match notes.TryGetValue attachmentId with
        | true, n -> n
        | _ -> ""

    /// 未确认消息（宿主直注插槽，非每帧投影）
    member _.SlotPendingMessages(items: PendingMessage list, activeConversationId: Guid option, canRetry: bool, retry: Guid -> unit, openConversation: Guid -> unit) =
        pending.Set(items, activeConversationId, canRetry, retry, openConversation)

    member _.Pending = pending

    member _.SlotAccessory(c: Control) = box.SetAccessory c
    /// 同步提交（旧 Composer.Submit）：门控通过才交宿主，宿主接管才清空；宿主异常照常抛出
    member _.Submit() = box.Submit()

    /// 用户草稿回填（一次性命令，非每帧投影）
    member _.ApplyDraftText(text: string) =
        box.SetText text
        refresh ()
    member _.Focus() = box.Focus()
    member _.SlotImeProbe(f) = box.SetImeProbe f
    member _.SlotKeyHook(f) = box.SetKeyHook f
