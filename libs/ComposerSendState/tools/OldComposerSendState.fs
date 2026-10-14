namespace Wanxiang.UI

// 照抄旧 Views/Composer.fs 的 refreshSendState、Submit 门控与 RenderAttachments 的状态字分支（控件写入换成记录字段）
open System

type ComposerAttachment = { ready: bool; failed: bool; isImage: bool }

type ComposerSendInput =
    { enabled: bool; generating: bool; canStop: bool; disabledReason: string; hasText: bool
      attachments: ComposerAttachment list; visionNo: bool; visionBlockedMessage: string; enterSends: bool }

type ComposerSendView =
    { visionBlocked: bool; canSend: bool; sendEnabled: bool; queueEnabled: bool; queueVisible: bool
      sendTip: string; sendLabel: string; queueTip: string option }

module ComposerSendState =
    let decide (i: ComposerSendInput) : ComposerSendView =
        let attachments = i.attachments
        let enabled, generating, canStop, disabledReason, enterSends = i.enabled, i.generating, i.canStop, i.disabledReason, i.enterSends
        let hasText = i.hasText
        let hasAttachment = attachments |> List.exists (fun a -> a.ready)
        let uploading = attachments |> List.exists (fun a -> not a.ready && not a.failed)
        let hasFailed = attachments |> List.exists _.failed
        let hasImage = attachments |> List.exists (fun a -> a.isImage)
        let visionBlocked = hasImage && i.visionNo
        let canSend = enabled && not uploading && not hasFailed && not visionBlocked && (hasText || hasAttachment)
        let sendTip =
            if generating then
                if canStop then "停止生成 (Escape)" else "等待主机确认…"
            elif not enabled then
                if not (String.IsNullOrWhiteSpace disabledReason) then disabledReason else "连接已断开"
            elif uploading then
                "附件上传中，请稍候…"
            elif hasFailed then
                "有附件上传失败，移除或重新添加后发送"
            elif visionBlocked then
                i.visionBlockedMessage
            elif not hasText && not hasAttachment then
                "请输入消息或添加附件"
            else
                if enterSends then "发送（Enter）" else "发送（Ctrl / ⌘ + Enter）"
        let sendLabel = if generating then "停止生成" else "发送"
        let queueTip =
          if generating then
            Some(
                if not enabled then
                    if not (String.IsNullOrWhiteSpace disabledReason) then disabledReason else "连接已断开"
                elif uploading then "附件上传中，请稍候…"
                elif hasFailed then "有附件上传失败，移除或重新添加后发送"
                elif not hasText && not hasAttachment then "请输入消息或添加附件"
                else
                    if enterSends then "排队发送（Enter）" else "排队发送（Ctrl / ⌘ + Enter）")
          else None
        { visionBlocked = visionBlocked; canSend = canSend
          sendEnabled = (if generating then canStop else canSend); queueEnabled = canSend
          queueVisible = generating; sendTip = sendTip; sendLabel = sendLabel; queueTip = queueTip }

    let canSubmit (enabled: bool) (text: string) (attachments: ComposerAttachment list) =
        let uploading = attachments |> List.exists (fun a -> not a.ready)
        if enabled && not uploading then
            not (String.IsNullOrWhiteSpace text) || attachments |> List.exists (fun a -> a.ready)
        else false

    let chipState (restored: bool) (attachment: ComposerAttachment) (sizeText: string) =
        if restored then "需重选"
        elif attachment.ready || attachment.failed then sizeText
        else "上传中"

    let chipTip (restored: bool) (attachment: ComposerAttachment) (sizeText: string) =
        let formattedSize =
            if attachment.ready || attachment.failed then sizeText else "上传中"
        if restored then "附件需重新选择：重新打开后无法确认文件还在。可移除，或重新添加这个文件。"
        elif attachment.failed then sprintf "上传失败：%s，可移除或重新添加" formattedSize
        else sprintf "文件大小：%s" formattedSize
