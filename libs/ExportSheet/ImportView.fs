namespace Wanxiang.UI

[<RequireQualifiedAccess>]
type ImportGate =
    | Hidden
    | Disabled
    | Enabled

/// 导入对话框按键的呈现
type ImportChrome = { gate: ImportGate; pickEnabled: bool; pickText: string }

/// 旧 Views/ImportDialog.fs 的按键门控、提交尝试与文案。预览解析、文件读取仍归 ImportDialog
module ImportView =

    let chrome (hasPreview: bool) (busy: bool) =
        let gate =
            if not hasPreview then ImportGate.Hidden
            elif busy then ImportGate.Disabled
            else ImportGate.Enabled
        { gate = gate
          pickEnabled = not busy
          pickText = (if hasPreview then "换一个文件…" else "选择 JSON 文件…") }

    let noFile = "还没有选择文件。"
    let importing = "正在导入…"
    let timedOut = "主机没有回应导入请求，可能是主机版本过旧。升级主机后再试。"
    let rejected = "导入没有完成，主机拒绝了这份内容或连接已断开。可以再试一次。"
    let readFailed (message: string) = "读取文件失败：" + message
    let pickerFailed (message: string) = "无法打开文件选择窗口：" + message
    let previewStatus (title: string) (count: int) = sprintf "「%s」· %d 条消息" title count
    let skippedNote (skipped: int) = if skipped > 0 then Some(sprintf "有 %d 条无法识别的记录会被跳过。" skipped) else None

/// 一次导入提交只认最新的那次回执（旧 busy + currentAttempt）
type ImportAttempt() =
    let mutable busy = false
    let mutable current = 0

    member _.Busy = busy

    /// 开始一次提交；已在忙返回 None
    member _.Begin() =
        if busy then None
        else
            busy <- true
            current <- current + 1
            Some current

    /// 回执；只有忙且是当前这次才算数，返回 true 表示要处理
    member _.Finish(attempt: int) =
        if busy && current = attempt then
            busy <- false
            true
        else false

    /// 重新打开对话框
    member _.Reset() = busy <- false
