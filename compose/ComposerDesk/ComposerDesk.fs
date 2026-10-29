namespace Wanxiang2.ComposerDesk

open System
open System.ComponentModel
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Platform.Storage
open Wanxiang.UI
open Wanxiang2.ComposerBox
open Wanxiang2.AttachmentTray
open Wanxiang2.ModelChip

/// 输入区对外的动作（旧 ComposerActions）
type ComposerDeskActions =
    {
        /// true 表示内容已由本地待发送记录接管
        Submit: string -> bool
        /// 输入框里的文字变了（每次按键都来一次）。
        ///
        /// 控件库不认识状态树，所以「文字变了」要由宿主接出去——草稿落盘、
        /// 发送门控读的都是状态树里的那一份文本。
        TextChanged: string -> unit
        /// 插入点变了（光标移动、选区变化、输入法组合中的位置更新）。
        ///
        /// 必须接出去：输入区的回写只在**文本不同**时发生（`libs/AttachmentTray/ComposerBox.fs`
        /// 的 `if input.Text <> vs.Text`），所以文本一旦相同就不会再写回——
        /// 这时候光标位置只可能靠这个回调保持同步，否则打字打到一半光标会被拉回末尾。
        CaretChanged: int -> unit
        StopGeneration: unit -> unit
        PickAttachment: unit -> unit
        RemoveAttachment: Guid -> unit
        OpenModelPicker: Control -> unit
        DropFiles: IStorageItem list -> unit
        PasteFromClipboard: unit -> bool
        OpenSessionSettings: unit -> unit
    }

/// 组合者：ComposerBox + AttachmentTray + ModelChip，发送门控走 ComposerSendState，看图判断走 ModelVision（旧 Views/Composer.fs）。
/// 业务事实零本地副本：一切从 Render 收到的 ComposerVS 投影（在途态清单见本库 AGENTS.md）。
type ComposerDesk(actions: ComposerDeskActions) =
    /// **渲染回写不算用户输入**：`Render` 会把投影里的文本写回输入框
    /// （`libs/AttachmentTray/ComposerBox.fs` 的 `if input.Text <> vs.Text`），
    /// 那次写入也会触发 `TextChanged`。若把它当成用户输入接出去，
    /// 就会形成自擦除的环：回写空 → 报告「文本变成空」→ 状态树变空 → 下一帧继续回写空，
    /// 用户打的字被抹掉（实测：键入后 29 ms 内 value 被写回 ""）。
    /// 所以只在**不是渲染回写**时（`applying` 为 false）才把文本接出去。
    /// 已经交给宿主的最后一份文本。用来判「这次变化是不是新的」：
    /// 同一份文本不重复报（去重），渲染回写也不算用户输入。
    let dispatched = ref ""
    let box = ComposerBox()
    let tray = AttachmentTray()
    let chip = ModelChip()
    let pending = PendingPanel()

    /// 发送历史召回（上下键召回已发送消息、Esc 退回半截草稿）。
    /// 库 `Wanxiang2.PromptHistory` 拥有召回状态机（游标 + 保管的半截草稿 + 已发送条目）；
    /// 本组合者只负责接线：把按键路由给它、把召回结果写回输入框、把「已被接受的发送」记进去。
    /// 2026-10-21（W7-CLEANBREAK）：这段接线曾在提交 `82d5628` 里随 `compose/EntryShells/Composer.fs`
    /// 一起被删，功能静默消失；此处按旧实现的行为原样恢复（见本库 AGENTS.md 的「发送历史召回」）。
    let history = PromptHistory()

    /// 召回结果写回输入框：换字并把光标放到末尾（与旧 `Composer.recall` 同一口径）。
    let recall (text: string option) : bool =
        match text with
        | Some t ->
            box.Input.Text <- t
            box.Input.CaretIndex <- (if isNull box.Input.Text then 0 else box.Input.Text.Length)
            true
        | None -> false

    /// 按键钩子：Esc 选区先清、召回中退回草稿、否则把焦点交出；Up/Down 走发送历史。
    /// 有选区时不召回（`PromptHistory.Up`/`Down` 自己也拒有选区的情况）。
    let keyHook (e: KeyEventArgs) =
        let input = box.Input
        let hasSelection = input.SelectionStart <> input.SelectionEnd

        if e.Key = Key.Escape then
            if hasSelection then
                input.ClearSelection()
                true
            elif recall (history.Escape()) then
                true
            elif input.IsFocused then
                match TopLevel.GetTopLevel input with
                | null -> ()
                | top ->
                    match top.FocusManager with
                    | null -> ()
                    | fm -> fm.Focus(null, NavigationMethod.Unspecified, KeyModifiers.None) |> ignore

                true
            else
                false
        elif e.Key = Key.Up && e.KeyModifiers = KeyModifiers.None then
            recall (history.Up(input.Text, input.CaretIndex, hasSelection))
        elif e.Key = Key.Down && e.KeyModifiers = KeyModifiers.None then
            recall (history.Down(input.Text, input.CaretIndex, hasSelection))
        else
            false

    let thumbs = Collections.Generic.Dictionary<Guid, IImage>()
    let notes = Collections.Generic.Dictionary<Guid, string>()

    /// 在途（登记 AGENTS.md）：最近一帧 ComposerVS。门控不止在 Render 时算——
    /// 文本变化（TextChanged / Text 属性）与非每帧命令（缩略图、缩小说明、草稿回填）都要按最新帧重算，
    /// 这里存的就是「最新帧」本身，是帧的缓存不是业务事实的第二权威（与 SidebarDesk.lastVS 同一口径）。
    let mutable last: Wanxiang2.WorkspaceState.ComposerVS =
        (Wanxiang2.WorkspaceState.Workspace.project (Wanxiang2.WorkspaceState.Workspace.init ())).Composer

    /// 输入区上下外边距走主题（Margin.Composer：顶=Space.Md、底=Space.X2l）。
    /// 左右留白按窗口视口分档（LayoutPolicy.horizontalInset）。视口由工作区传入；
    /// 本列单独放进窗口时，列宽就是视口，用列宽兜底。
    let host = box.Host :?> Border
    /// 在途（登记 AGENTS.md）：已套用的留白档（None = 还没套）；非业务事实，只是避免重复换绑。
    let mutable paddedCompact: bool option = None
    let mutable marginBinding: IDisposable option = None
    /// 在途：工作区最近一次给出的窗口视口宽度。0 表示还没接入，留白退回本列宽度。
    let mutable viewportWidth = 0.0

    let applyPadding () =
        // 上下外边距固定走宽屏键。左右档不看本列宽度：侧栏打开时列比窗口窄，分档会偏小。
        if paddedCompact <> Some false then
            paddedCompact <- Some false
            marginBinding |> Option.iter (fun d -> d.Dispose())
            marginBinding <- Some(host.Bind(Layoutable.MarginProperty, Dyn.res "Margin.Composer") :> IDisposable)

        let width =
            if viewportWidth > 0.0 then
                viewportWidth
            else
                host.Bounds.Width

        let inset = LayoutPolicy.horizontalInset width
        host.Padding <- Thickness(inset, 0.0, inset, 0.0)

    let facts (a: Wanxiang2.WorkspaceState.AttachmentVS) : ComposerAttachment =
        let stage =
            if RestoredAttachments.isRestored a.Id then
                AttachmentStage.Restored
            elif a.Failed then
                AttachmentStage.Failed
            elif a.Ready then
                AttachmentStage.Ready
            else
                AttachmentStage.Uploading

        let kind =
            if MediaTypes.isImage a.MediaType then
                AttachmentKind.Image
            else
                AttachmentKind.OtherFile

        { stage = stage; kind = kind }

    let hostOf (vs: Wanxiang2.WorkspaceState.ComposerVS) =
        // 生成中输入区被标成禁用，只是为了挡住「再发一条当普通发送」。
        // 排队键仍要能用：停止/排队这条路把主机当成可用，不把「正在生成」当成断线。
        match vs.Send.State with
        | Wanxiang2.WorkspaceState.SendButtonState.GeneratingStop -> HostAvailable
        | _ ->
            if vs.Enabled then
                HostAvailable
            else
                HostDisabled(vs.DisabledReason |> Option.defaultValue "")

    let generationOf (vs: Wanxiang2.WorkspaceState.ComposerVS) =
        match vs.Send.State with
        | Wanxiang2.WorkspaceState.SendButtonState.GeneratingStop -> Generating CanStop
        | _ -> Idle

    let visionOf (vs: Wanxiang2.WorkspaceState.ComposerVS) =
        if vs.Vision = VisionNo then
            VisionForbidden(ModelVision.blockedMessage vs.VisionLabel)
        else
            VisionAllowed

    let refresh () =
        let v =
            ComposerSendState.decide
                {
                    host = hostOf last
                    generation = generationOf last
                    text =
                        if String.IsNullOrWhiteSpace box.Text then
                            EmptyText
                        else
                            HasText
                    attachments = last.Tray |> List.map facts
                    vision = visionOf last
                    sendKey = if last.EnterSends then EnterSends else CtrlEnterSends
                }

        box.ApplySend
            {
                VisionBlocked = v.VisionBlocked
                VisionMessage = v.VisionMessage
                SendEnabled = v.SendEnabled
                QueueEnabled = v.QueueEnabled
                QueueVisible = v.QueueVisible
                SendTip = v.SendTip
                SendLabel = v.SendLabel
                QueueTip = v.QueueTip
            }

    let trayItem (a: Wanxiang2.WorkspaceState.AttachmentVS) : TrayItem =
        let restored = RestoredAttachments.isRestored a.Id
        let f = facts a
        let size = AttachmentRef.formatSize a.Size

        let note =
            match notes.TryGetValue a.Id with
            | true, n -> Some n
            | _ -> None

        {
            Id = a.Id
            FileName = a.FileName
            SizeText = size
            StateText = ComposerSendState.chipState f.stage size
            Tip = ComposerSendState.chipTip f.stage size
            Name =
                if restored then
                    sprintf "附件需重新选择：%s，可移除或重新添加这个文件" a.FileName
                elif a.Failed then
                    sprintf "附件上传失败：%s（%s），可移除或重新添加" a.FileName size
                else
                    match note with
                    | Some n -> sprintf "附件：%s（%s），%s" a.FileName size n
                    | None -> sprintf "附件：%s（%s）" a.FileName size
            Failed = a.Failed
            Ready = a.Ready
            IsImage = (f.kind = AttachmentKind.Image)
            ShrinkNote = note
            Thumbnail =
                match thumbs.TryGetValue a.Id with
                | true, t -> Some t
                | _ -> None
        }

    let renderItems () =
        tray.Render(last.Tray |> List.map trayItem, last.Compact)

    let submit (text: string) =
        if
            ComposerSendState.canSubmit
                (hostOf last)
                (if String.IsNullOrWhiteSpace text then
                     EmptyText
                 else
                     HasText)
                (last.Tray |> List.map facts)
        then
            if actions.Submit text then
                // 只有**已被接受**的发送才记进历史（门控没过、或宿主没接管，都不算）。
                history.Record text
                box.ClearAfterSubmit text
                refresh ()

    do
        box.SlotPending pending.Host
        applyPadding ()

        host.PropertyChanged.Add(fun e ->
            if e.Property = Visual.BoundsProperty && not last.Compact then
                applyPadding ())

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
        // 按键钩子：Esc / 上下键的发送历史召回（本组合者接 PromptHistory，控件库不认识它）。
        box.WireKeyHook keyHook
        // 文本变化要**同步**接出去。
        //
        // 为什么不能只听 `TextChanged`：Avalonia 的 `TextBox.RaiseTextChangeEvents` 是
        //   RaiseEvent(TextChanging); Dispatcher.UIThread.Post(() => RaiseEvent(TextChanged))
        // —— `TextChanged` 是**投递**出去的，不是当场触发的。等到它送达时，
        // 中间那一帧的渲染已经跑过：`ComposerBox.Render` 发现 `input.Text ("a") <> vs.Text ("")`，
        // 把投影里的空文本写回输入框，于是「送达时读到的 `input.Text`」已经是空的。
        // 实测（PWA）：键入后日志依次是 `input.Text='a'` → `input.Text=''` → `TextChanged` 报 `text=''`，
        // 打的字被抹掉，草稿永远存不进本机。
        //
        // 属性系统（`INotifyPropertyChanged`）的 `Text` 通知是**当场**发的，
        // 那一刻 `input.Text` 还是新值，所以从那里取。
        let pushText (t: string) =
            if t <> !dispatched then
                dispatched := t
                refresh ()
                // 文字变了要接出去：草稿落盘与发送门控读的都是状态树里的那一份文本。
                actions.TextChanged t

        box.Input.PropertyChanged.Add(fun e ->
            if e.Property.Name = "Text" then
                match e.NewValue with
                | :? string as s -> pushText s
                | _ -> ())
        // `TextChanged` 仍留着当兜底（属性通知没覆盖到的路径），
        // 但因为 `pushText` 按文本去重，延迟送达不会造成重复上报。
        box.Input.TextChanged.Add(fun _ -> pushText box.Input.Text)
        // 光标位置也接出去：文本相同时输入区不再回写，光标只能靠这个回调保持同步。
        //
        // Avalonia 的 `TextBox` **没有** `CaretIndexChanged` 事件（只有 `TextChanged` /
        // `TextChanging` / `TextInput` / `TextInputMethodClientRequested`），
        // 光标变化走 `INotifyPropertyChanged` 的 `CaretIndex` 属性通知。
        box.Input.PropertyChanged.Add(fun e ->
            if e.Property.Name = "CaretIndex" then
                actions.CaretChanged box.Input.CaretIndex)
        // TextChanged 晚于 Text 属性派发：属性一变就同步重算门控，写字后立即回车不读旧门控
        box.Input.PropertyChanged.Add(fun e ->
            if e.Property = TextBox.TextProperty then
                refresh ())

        refresh ()

    member _.Host: Control = box.Host

    /// 窗口视口宽度。水平留白按它分档，不按本列宽度。
    member _.ApplyViewportWidth(width: float) =
        if width > 0.0 then
            viewportWidth <- width
            applyPadding ()

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
        // 渲染回写期间不把「文本变了」当用户输入（否则会自擦除，见上面 applying 的说明）。
        // 渲染回写若与已上报的文本不同，说明状态树落后了；这时不能把回写当用户输入
        // （`pushText` 的去重已经挡掉同一份文本，这里只保证回写不会篡改已上报的值）。
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

        if last.Tray |> List.exists (fun a -> a.Id = attachmentId) then
            renderItems ()

    member _.HasThumbnail(attachmentId: Guid) = thumbs.ContainsKey attachmentId

    /// 图片压缩说明（非每帧命令：上传管线就地生成，VS 不承载）
    member _.ApplyShrinkNote(attachmentId: Guid, note: string) =
        notes.[attachmentId] <- note

        if last.Tray |> List.exists (fun a -> a.Id = attachmentId) then
            renderItems ()

    member _.ShrinkNote(attachmentId: Guid) =
        match notes.TryGetValue attachmentId with
        | true, n -> n
        | _ -> ""

    /// 未确认消息（宿主直注插槽，非每帧投影）
    member _.SlotPendingMessages
        (
            items: PendingMessage list,
            activeConversationId: Guid option,
            canRetry: bool,
            retry: Guid -> unit,
            openConversation: Guid -> unit
        ) =
        pending.Render(items, activeConversationId, canRetry, retry, openConversation)

    member _.Pending = pending

    member _.SlotAccessory(c: Control) = box.SlotAccessory c
    /// 同步提交（旧 Composer.Submit）：门控通过才交宿主，宿主接管才清空；宿主异常照常抛出
    member _.Submit() = box.Submit()

    /// 草稿回填命令（宿主一次性命令，非每帧投影）
    member _.ApplyDraftText(text: string) =
        // 外部改写输入框（编辑重发、模板、草稿恢复）：退出召回，别让召回态把这次改写吃掉。
        history.Reset()
        box.ApplyDraftText text
        refresh ()

    member _.Focus() = box.Focus()
    member _.WireImeProbe(f) = box.WireImeProbe f
    member _.WireKeyHook(f) = box.WireKeyHook f
