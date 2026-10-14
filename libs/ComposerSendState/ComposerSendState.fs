namespace Wanxiang.UI

open System

/// 输入区里的一条附件（只取发送门控需要的三个事实）
type ComposerAttachment = { ready: bool; failed: bool; isImage: bool }

/// 发送门控的全部输入
type ComposerSendInput =
    { enabled: bool
      generating: bool
      canStop: bool
      disabledReason: string
      hasText: bool
      attachments: ComposerAttachment list
      /// 当前模型确定不能看图
      visionNo: bool
      /// 看图受阻时的说明（旧 ModelVision.blockedMessage 的结果，由调用方给）
      visionBlockedMessage: string
      enterSends: bool }

/// 发送键、排队键与看图提示的决定
type ComposerSendView =
    { visionBlocked: bool
      canSend: bool
      sendEnabled: bool
      queueEnabled: bool
      queueVisible: bool
      sendTip: string
      sendLabel: string
      /// 只在生成中给出
      queueTip: string option }

module ComposerSendState =

    let private offline (reason: string) =
        if not (String.IsNullOrWhiteSpace reason) then reason else "连接已断开"

    /// 旧 Composer.refreshSendState 的分支
    let decide (i: ComposerSendInput) : ComposerSendView =
        let hasAttachment = i.attachments |> List.exists _.ready
        let uploading = i.attachments |> List.exists (fun a -> not a.ready && not a.failed)
        let hasFailed = i.attachments |> List.exists _.failed
        let visionBlocked = i.visionNo && (i.attachments |> List.exists _.isImage)
        // 失败残留同样挡住发送
        let canSend = i.enabled && not uploading && not hasFailed && not visionBlocked && (i.hasText || hasAttachment)
        let sendTip =
            if i.generating then (if i.canStop then "停止生成 (Escape)" else "等待主机确认…")
            elif not i.enabled then offline i.disabledReason
            elif uploading then "附件上传中，请稍候…"
            elif hasFailed then "有附件上传失败，移除或重新添加后发送"
            elif visionBlocked then i.visionBlockedMessage
            elif not i.hasText && not hasAttachment then "请输入消息或添加附件"
            elif i.enterSends then "发送（Enter）"
            else "发送（Ctrl / ⌘ + Enter）"
        // 已知旧行为：排队提示不提看图受阻
        let queueTip =
            if not i.generating then None
            elif not i.enabled then Some(offline i.disabledReason)
            elif uploading then Some "附件上传中，请稍候…"
            elif hasFailed then Some "有附件上传失败，移除或重新添加后发送"
            elif not i.hasText && not hasAttachment then Some "请输入消息或添加附件"
            elif i.enterSends then Some "排队发送（Enter）"
            else Some "排队发送（Ctrl / ⌘ + Enter）"
        { visionBlocked = visionBlocked
          canSend = canSend
          sendEnabled = (if i.generating then i.canStop else canSend)
          queueEnabled = canSend
          queueVisible = i.generating
          sendTip = sendTip
          sendLabel = (if i.generating then "停止生成" else "发送")
          queueTip = queueTip }

    /// 旧 Composer.Submit 的门控：注意这里失败残留也算「未就绪」
    let canSubmit (enabled: bool) (text: string) (attachments: ComposerAttachment list) =
        enabled
        && not (attachments |> List.exists (fun a -> not a.ready))
        && (not (String.IsNullOrWhiteSpace text) || attachments |> List.exists _.ready)

    /// 附件 chip 右侧的状态字；sizeText 为格式化后的大小
    let chipState (restored: bool) (a: ComposerAttachment) (sizeText: string) =
        if restored then "需重选"
        elif a.ready || a.failed then sizeText
        else "上传中"

    /// 附件 chip 状态字的提示
    let chipTip (restored: bool) (a: ComposerAttachment) (sizeText: string) =
        let formatted = if a.ready || a.failed then sizeText else "上传中"
        if restored then "附件需重新选择：重新打开后无法确认文件还在。可移除，或重新添加这个文件。"
        elif a.failed then sprintf "上传失败：%s，可移除或重新添加" formatted
        else sprintf "文件大小：%s" formatted
