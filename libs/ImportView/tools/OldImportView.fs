namespace Wanxiang.UI

// 照抄旧 Views/ImportDialog.fs 的 Refresh、Confirm/finish 守卫与文案（Guid 尝试号换成计数）
type ImportChrome = { importVisible: bool; importEnabled: bool; pickEnabled: bool; pickText: string }

module ImportView =
    let chrome (hasPreview: bool) (busy: bool) =
        let preview = if hasPreview then Some() else None
        { importVisible = preview.IsSome
          importEnabled = (preview.IsSome && not busy)
          pickEnabled = (not busy)
          pickText = (if preview.IsSome then "换一个文件…" else "选择 JSON 文件…") }
    let noFile = "还没有选择文件。"
    let importing = "正在导入…"
    let timedOut = "主机没有回应导入请求，可能是主机版本过旧。升级主机后再试。"
    let rejected = "导入没有完成，主机拒绝了这份内容或连接已断开。可以再试一次。"
    let readFailed (message: string) = "读取文件失败：" + message
    let pickerFailed (message: string) = "无法打开文件选择窗口：" + message
    let previewStatus (title: string) (count: int) = sprintf "「%s」· %d 条消息" title count
    let skippedNote (skipped: int) = if skipped > 0 then Some(sprintf "有 %d 条无法识别的记录会被跳过。" skipped) else None

type ImportAttempt() =
    let mutable busy = false
    let mutable currentAttempt = 0
    member _.Busy = busy
    member _.Begin() =
        if not busy then
            busy <- true
            let attempt = currentAttempt + 1
            currentAttempt <- attempt
            Some attempt
        else None
    member _.Finish(attempt: int) =
        if busy && currentAttempt = attempt then
            busy <- false
            true
        else false
    member _.Reset() = busy <- false
