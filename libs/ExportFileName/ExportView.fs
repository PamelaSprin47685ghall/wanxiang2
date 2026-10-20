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

/// 导出的输出产物类别（限定访问）
[<RequireQualifiedAccess>]
type ExportOutputKind =
    | None
    | MarkdownOnly
    | JsonOnly
    | Both

/// 保存的子状态（旧 saving、saved、savedKind、saveError）
type ExportSave = { saving: bool; saved: bool; savedKind: string; error: string option }

[<RequireQualifiedAccess>]
type ExportProgress =
    | Hidden
    | Indeterminate
    | Determinate of percent: float

[<RequireQualifiedAccess>]
type SaveGate =
    | Disabled
    | Enabled

[<RequireQualifiedAccess>]
type ExportAction =
    | None
    | Retry
    | Save of gate: SaveGate * hasJson: bool

/// 导出对话框的呈现
type ExportViewState =
    { statusText: string
      tone: ExportTone
      progress: ExportProgress
      action: ExportAction
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

    let decide (phase: ExportPhase) (save: ExportSave) (outputKind: ExportOutputKind) (reducedMotion: bool) : ExportViewState =
        match phase with
        | ExportPhase.Reading(received, total) ->
            let progress =
                match total with
                | None ->
                    if reducedMotion then ExportProgress.Determinate 0.0
                    else ExportProgress.Indeterminate
                | Some t when t > 0 ->
                    ExportProgress.Determinate (float received / float t * 100.0)
                | _ ->
                    ExportProgress.Determinate 0.0
            let statusText =
                match total with
                | None -> "正在读取完整历史…"
                | Some t -> sprintf "已读取 %d / %d 条消息…" received t
            { statusText = statusText
              tone = ExportTone.Muted
              progress = progress
              action = ExportAction.None
              closeText = "取消导出" }
        | ExportPhase.Ready count ->
            let empty = count = 0
            // 已知旧行为：读完但没有输出时，重试键可见
            let hasOutput = match outputKind with ExportOutputKind.None -> false | _ -> true
            let hasJson = match outputKind with ExportOutputKind.JsonOnly | ExportOutputKind.Both -> true | _ -> false
            let usable = hasOutput && not save.saving && not empty
            let statusText =
                if empty then "当前对话没有已保存的消息，无需导出"
                elif save.saving then "正在保存…"
                elif save.saved then sprintf "已保存完整记录（%d 条消息，%s）。可以再保存另一种格式。" count save.savedKind
                else save.error |> Option.defaultValue (sprintf "已完整读取 %d 条消息，可以保存。" count)
            let tone =
                if save.saved then ExportTone.Success
                elif save.error.IsSome then ExportTone.Danger
                else ExportTone.Text
            let action =
                if not hasOutput then ExportAction.Retry
                else
                    let gate = if usable then SaveGate.Enabled else SaveGate.Disabled
                    ExportAction.Save(gate, hasJson)
            { statusText = statusText
              tone = tone
              progress = ExportProgress.Hidden
              action = action
              closeText = "关闭" }
        | ExportPhase.Failed message -> { statusText = message; tone = ExportTone.Danger; progress = ExportProgress.Hidden; action = ExportAction.Retry; closeText = "关闭" }
        | ExportPhase.Cancelled -> { statusText = "导出已取消。"; tone = ExportTone.Muted; progress = ExportProgress.Hidden; action = ExportAction.Retry; closeText = "关闭" }
        | ExportPhase.Idle -> { statusText = "正在准备导出…"; tone = ExportTone.Text; progress = ExportProgress.Hidden; action = ExportAction.None; closeText = "关闭" }

    /// 读取中是唯一高频态：窗口内合并，窗口末尾补一次；其他状态与急件立即呈现
    let refresh (force: bool) (phase: ExportPhase) (elapsedMs: float) (windowMs: float) (pending: bool) =
        let highFrequency = match phase with ExportPhase.Reading _ -> true | _ -> false
        if force || not highFrequency || elapsedMs >= windowMs then ExportRefresh.Now
        elif not pending then ExportRefresh.Schedule(windowMs - elapsedMs)
        else ExportRefresh.Skip
