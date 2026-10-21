namespace Wanxiang.UI

open System

/// 附件的生命周期阶段（互斥 DU，消灭 ready&&failed 等非法组合）
type AttachmentStage =
    | Uploading
    | Ready
    | Failed
    | Restored

/// 附件内容种类
type AttachmentKind =
    | Image
    | OtherFile

/// 输入区里的一条附件（只取发送门控需要的两个互斥事实）
type ComposerAttachment =
    {
        stage: AttachmentStage
        kind: AttachmentKind
    }

/// 主机/连接可用性
type HostAvailability =
    | HostAvailable
    | HostDisabled of reason: string

/// 停止能力
type StopCapability =
    | CanStop
    | WaitingHost

/// 生成活动状态
type GenerationStatus =
    | Idle
    | Generating of StopCapability

/// 输入区文本存在性
type TextPresence =
    | HasText
    | EmptyText

/// 模型看图能力
type VisionCapability =
    | VisionAllowed
    | VisionForbidden of blockedMessage: string

/// 发送按键配置
type SendKeyPreference =
    | EnterSends
    | CtrlEnterSends

/// 发送门控的全部输入（零裸布尔，全由事实 DU 构成）
type ComposerSendInput =
    {
        host: HostAvailability
        generation: GenerationStatus
        text: TextPresence
        attachments: ComposerAttachment list
        vision: VisionCapability
        sendKey: SendKeyPreference
    }

/// 阻挡发送/排队的原因
type BlockReason =
    | HostOffline of reason: string
    | AttachmentUploading
    | AttachmentFailed
    | ModelCannotSeeImage of message: string
    | EmptyContent

/// 发送门控的核心决策
type SendGate =
    | Send
    | Queue
    | Stop
    | Blocked of BlockReason

/// 主按钮（发送 / 停止）动作状态
type PrimaryAction =
    | SendReady of tip: string
    | SendBlocked of reason: BlockReason * tip: string
    | StopReady of tip: string
    | StopWaiting of tip: string

/// 排队按钮动作状态
type QueueAction =
    | QueueHidden
    | QueueReady of tip: string
    | QueueBlocked of reason: BlockReason * tip: string

/// 看图提示条状态
type VisionNotice =
    | VisionClear
    | VisionNotice of message: string

/// 发送键、排队键与看图提示的决定（零裸布尔，由互斥行为 DU 组成）
type ComposerSendView =
    {
        gate: SendGate
        primary: PrimaryAction
        queue: QueueAction
        vision: VisionNotice
    }

    member this.VisionBlocked: bool =
        match this.vision with
        | VisionNotice _ -> true
        | VisionClear -> false

    member this.VisionMessage: string =
        match this.vision with
        | VisionNotice msg -> msg
        | VisionClear -> ""

    member this.SendEnabled: bool =
        match this.primary with
        | SendReady _
        | StopReady _ -> true
        | SendBlocked _
        | StopWaiting _ -> false

    member this.SendLabel: string =
        match this.primary with
        | SendReady _
        | SendBlocked _ -> "发送"
        | StopReady _
        | StopWaiting _ -> "停止生成"

    member this.SendTip: string =
        match this.primary with
        | SendReady tip
        | SendBlocked(_, tip)
        | StopReady tip
        | StopWaiting tip -> tip

    member this.QueueVisible: bool =
        match this.queue with
        | QueueHidden -> false
        | QueueReady _
        | QueueBlocked _ -> true

    member this.QueueEnabled: bool =
        match this.queue with
        | QueueReady _ -> true
        | QueueHidden
        | QueueBlocked _ -> false

    member this.QueueTip: string option =
        match this.queue with
        | QueueHidden -> None
        | QueueReady tip
        | QueueBlocked(_, tip) -> Some tip

module ComposerSendState =

    let private offlineText (reason: string) =
        if not (String.IsNullOrWhiteSpace reason) then
            reason
        else
            "连接已断开"

    /// 判断阻止发送的原因（按旧分支优先级）
    let blockReason
        (host: HostAvailability)
        (text: TextPresence)
        (attachments: ComposerAttachment list)
        (vision: VisionCapability)
        : BlockReason option =
        let hasReadyAttachment =
            attachments |> List.exists (fun a -> a.stage = AttachmentStage.Ready)

        let isUploading =
            attachments |> List.exists (fun a -> a.stage = AttachmentStage.Uploading)

        let isFailed =
            attachments
            |> List.exists (fun a -> a.stage = AttachmentStage.Failed || a.stage = AttachmentStage.Restored)

        let hasImage = attachments |> List.exists (fun a -> a.kind = AttachmentKind.Image)
        let hasContent = (text = TextPresence.HasText) || hasReadyAttachment

        match host with
        | HostDisabled reason -> Some(BlockReason.HostOffline(offlineText reason))
        | HostAvailable ->
            if isUploading then
                Some BlockReason.AttachmentUploading
            elif isFailed then
                Some BlockReason.AttachmentFailed
            else
                match vision with
                | VisionForbidden msg when hasImage -> Some(BlockReason.ModelCannotSeeImage msg)
                | _ ->
                    if not hasContent then
                        Some BlockReason.EmptyContent
                    else
                        None

    /// 门控判定：返回 Send / Queue / Stop / Blocked
    let gate (i: ComposerSendInput) : SendGate =
        match i.generation with
        | Generating _ ->
            // 生成中主按键动作恒为 Stop
            SendGate.Stop
        | Idle ->
            match blockReason i.host i.text i.attachments i.vision with
            | Some r -> SendGate.Blocked r
            | None -> SendGate.Send

    /// 旧 Composer.refreshSendState 的分支决策
    let decide (i: ComposerSendInput) : ComposerSendView =
        let g = gate i
        let hasImage = i.attachments |> List.exists (fun a -> a.kind = AttachmentKind.Image)

        let visionNotice =
            match i.vision with
            | VisionForbidden msg when hasImage -> VisionNotice msg
            | _ -> VisionClear

        let sendShortcutTip =
            match i.sendKey with
            | EnterSends -> "发送（Enter）"
            | CtrlEnterSends -> "发送（Ctrl / ⌘ + Enter）"

        let queueShortcutTip =
            match i.sendKey with
            | EnterSends -> "排队发送（Enter）"
            | CtrlEnterSends -> "排队发送（Ctrl / ⌘ + Enter）"

        match i.generation with
        | Idle ->
            let primary =
                match blockReason i.host i.text i.attachments i.vision with
                | Some r ->
                    let tip =
                        match r with
                        | HostOffline reason -> reason
                        | AttachmentUploading -> "附件上传中，请稍候…"
                        | AttachmentFailed -> "有附件上传失败，移除或重新添加后发送"
                        | ModelCannotSeeImage msg -> msg
                        | EmptyContent -> "请输入消息或添加附件"

                    SendBlocked(r, tip)
                | None -> SendReady sendShortcutTip

            {
                gate = g
                primary = primary
                queue = QueueHidden
                vision = visionNotice
            }
        | Generating stopCap ->
            let primary =
                match stopCap with
                | CanStop -> StopReady "停止生成 (Escape)"
                | WaitingHost -> StopWaiting "等待主机确认…"

            // 排队分支：已知旧行为，排队提示不提看图受阻
            let queue =
                let hasReadyAttachment =
                    i.attachments |> List.exists (fun a -> a.stage = AttachmentStage.Ready)

                let isUploading =
                    i.attachments |> List.exists (fun a -> a.stage = AttachmentStage.Uploading)

                let isFailed =
                    i.attachments
                    |> List.exists (fun a -> a.stage = AttachmentStage.Failed || a.stage = AttachmentStage.Restored)

                let hasContent = (i.text = TextPresence.HasText) || hasReadyAttachment

                match i.host with
                | HostDisabled reason ->
                    let t = offlineText reason
                    QueueBlocked(HostOffline t, t)
                | HostAvailable ->
                    if isUploading then
                        QueueBlocked(AttachmentUploading, "附件上传中，请稍候…")
                    elif isFailed then
                        QueueBlocked(AttachmentFailed, "有附件上传失败，移除或重新添加后发送")
                    elif not hasContent then
                        QueueBlocked(EmptyContent, "请输入消息或添加附件")
                    else
                        // 内容充足且无上传/失败：视看图受阻决定是否能排队
                        match visionNotice with
                        | VisionNotice _ ->
                            // 旧行为：排队提示不提看图受阻（仍显示快捷键），但 queueEnabled 为 false (canSend 为 false)
                            QueueBlocked(ModelCannotSeeImage "", queueShortcutTip)
                        | VisionClear -> QueueReady queueShortcutTip

            {
                gate = g
                primary = primary
                queue = queue
                vision = visionNotice
            }

    /// 旧 Composer.Submit 的门控：从同一 blockReason / SendGate 派生
    let canSubmit (host: HostAvailability) (text: TextPresence) (attachments: ComposerAttachment list) : bool =
        // 旧 canSubmit 行为：不考虑 vision，仅看 host、附件就绪性、文本/就绪附件
        match blockReason host text attachments VisionAllowed with
        | None -> true
        | Some _ -> false

    /// 附件 chip 右侧的状态字；sizeText 为格式化后的大小（从 AttachmentStage 派生）
    let chipState (stage: AttachmentStage) (sizeText: string) : string =
        match stage with
        | Restored -> "需重选"
        | Ready
        | Failed -> sizeText
        | Uploading -> "上传中"

    /// 附件 chip 状态字的提示（从 AttachmentStage 派生）
    let chipTip (stage: AttachmentStage) (sizeText: string) : string =
        let formatted =
            match stage with
            | Ready
            | Failed -> sizeText
            | Restored
            | Uploading -> "上传中"

        match stage with
        | Restored -> "附件需重新选择：重新打开后无法确认文件还在。可移除，或重新添加这个文件。"
        | Failed -> sprintf "上传失败：%s，可移除或重新添加" formatted
        | Ready
        | Uploading -> sprintf "文件大小：%s" formatted

    /// Enter 发送按键矩阵：not shift && (enterSends <> ctrl)
    let shouldSend (sendKey: SendKeyPreference) (shift: bool) (ctrlOrMeta: bool) : bool =
        let enterMode = (sendKey = EnterSends)
        not shift && (enterMode <> ctrlOrMeta)
