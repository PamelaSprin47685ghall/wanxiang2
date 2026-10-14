namespace Wanxiang.UI

/// 导出读取阶段（与旧 ConversationExport 状态同形，本库不引用它；限定访问）
[<RequireQualifiedAccess>]
type ExportPhase =
    | Idle
    | Reading of received: int * total: int option
    | Ready of count: int
    | Failed of message: string
    | Cancelled

/// 状态文字的颜色档（限定访问）
[<RequireQualifiedAccess>]
type ExportTone =
    | Muted
    | Text
    | Success
    | Danger

/// 保存的子状态（旧 saving、saved、savedKind、saveError）
type ExportSave = { saving: bool; saved: bool; savedKind: string; error: string option }

/// 导出对话框的呈现
type ExportViewState =
    { statusText: string
      tone: ExportTone
      progressVisible: bool
      indeterminate: bool
      progressValue: float
      retryVisible: bool
      saveVisible: bool
      saveJsonVisible: bool
      saveEnabled: bool
      saveJsonEnabled: bool
      closeText: string }

/// 进度刷新的节流决定（限定访问）
[<RequireQualifiedAccess>]
type ExportRefresh =
    | Now
    | Schedule of delayMs: float
    | Skip

/// 旧 Views/ExportDialog.fs 的 RenderProgress 与 Refresh 节流
module ExportView =

    let idleSave = { saving = false; saved = false; savedKind = ""; error = None }

    let decide (phase: ExportPhase) (save: ExportSave) (hasOutput: bool) (hasJson: bool) (reducedMotion: bool) =
        let baseState =
            { statusText = ""; tone = ExportTone.Text; progressVisible = false; indeterminate = false; progressValue = 0.0
              retryVisible = false; saveVisible = false; saveJsonVisible = false; saveEnabled = false; saveJsonEnabled = false; closeText = "关闭" }
        match phase with
        | ExportPhase.Reading(received, total) ->
            { baseState with
                statusText = (match total with None -> "正在读取完整历史…" | Some t -> sprintf "已读取 %d / %d 条消息…" received t)
                tone = ExportTone.Muted
                progressVisible = true
                indeterminate = total.IsNone && not reducedMotion
                progressValue = (match total with Some t when t > 0 -> float received / float t * 100.0 | _ -> 0.0)
                closeText = "取消导出" }
        | ExportPhase.Ready count ->
            let empty = count = 0
            // 已知旧行为：读完但没有输出时，重试键可见
            let usable = hasOutput && not save.saving && not empty
            { baseState with
                statusText =
                    (if empty then "当前对话没有已保存的消息，无需导出"
                     elif save.saving then "正在保存…"
                     elif save.saved then sprintf "已保存完整记录（%d 条消息，%s）。可以再保存另一种格式。" count save.savedKind
                     else save.error |> Option.defaultValue (sprintf "已完整读取 %d 条消息，可以保存。" count))
                tone = (if save.saved then ExportTone.Success elif save.error.IsSome then ExportTone.Danger else ExportTone.Text)
                retryVisible = not hasOutput
                saveVisible = hasOutput
                saveJsonVisible = hasOutput && hasJson
                saveEnabled = usable
                saveJsonEnabled = usable && hasJson }
        | ExportPhase.Failed message -> { baseState with statusText = message; tone = ExportTone.Danger; retryVisible = true }
        | ExportPhase.Cancelled -> { baseState with statusText = "导出已取消。"; tone = ExportTone.Muted; retryVisible = true }
        | ExportPhase.Idle -> { baseState with statusText = "正在准备导出…" }

    /// 读取中是唯一高频态：窗口内合并，窗口末尾补一次；其他状态与急件立即呈现
    let refresh (force: bool) (phase: ExportPhase) (elapsedMs: float) (windowMs: float) (pending: bool) =
        let highFrequency = match phase with ExportPhase.Reading _ -> true | _ -> false
        if force || not highFrequency || elapsedMs >= windowMs then ExportRefresh.Now
        elif not pending then ExportRefresh.Schedule(windowMs - elapsedMs)
        else ExportRefresh.Skip
