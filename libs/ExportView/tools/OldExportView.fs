namespace Wanxiang.UI

// 照抄旧 Views/ExportDialog.fs RenderProgress（控件写入换成记录字段）与 Refresh 的节流分支
[<RequireQualifiedAccess>]
type ExportPhase = | Idle | Reading of received: int * total: int option | Ready of count: int | Failed of message: string | Cancelled
[<RequireQualifiedAccess>]
type ExportTone = | Muted | Text | Success | Danger
type ExportSave = { saving: bool; saved: bool; savedKind: string; error: string option }
type ExportViewState =
    { statusText: string; tone: ExportTone; progressVisible: bool; indeterminate: bool; progressValue: float
      retryVisible: bool; saveVisible: bool; saveJsonVisible: bool; saveEnabled: bool; saveJsonEnabled: bool; closeText: string }
[<RequireQualifiedAccess>]
type ExportRefresh = | Now | Schedule of delayMs: float | Skip

module ExportView =
    let idleSave = { saving = false; saved = false; savedKind = ""; error = None }

    let decide (phase: ExportPhase) (s: ExportSave) (hasOutput: bool) (hasJson: bool) (reduced: bool) =
        let saving, saved, savedKind, saveError = s.saving, s.saved, s.savedKind, s.error
        let output = if hasOutput then Some() else None
        let outputJson = if hasJson then Some() else None
        let mutable text, tone, indeterminate, value = "", ExportTone.Text, false, 0.0
        let reading, ready, failed, isEmpty =
            match phase with
            | ExportPhase.Reading(received, total) ->
                tone <- ExportTone.Muted
                text <- match total with None -> "正在读取完整历史…" | Some total -> sprintf "已读取 %d / %d 条消息…" received total
                indeterminate <- total.IsNone && not reduced
                value <- total |> Option.filter ((<) 0) |> Option.map (fun total -> float received / float total * 100.0) |> Option.defaultValue 0.0
                true, false, false, false
            | ExportPhase.Ready count ->
                let empty = count = 0
                text <-
                    if empty then "当前对话没有已保存的消息，无需导出"
                    elif saving then "正在保存…"
                    elif saved then sprintf "已保存完整记录（%d 条消息，%s）。可以再保存另一种格式。" count savedKind
                    else saveError |> Option.defaultValue (sprintf "已完整读取 %d 条消息，可以保存。" count)
                tone <-
                    if saved then ExportTone.Success
                    elif saveError.IsSome then ExportTone.Danger
                    else ExportTone.Text
                false, output.IsSome, output.IsNone, empty
            | ExportPhase.Failed message -> text <- message; tone <- ExportTone.Danger; false, false, true, false
            | ExportPhase.Cancelled -> text <- "导出已取消。"; tone <- ExportTone.Muted; false, false, true, false
            | ExportPhase.Idle -> text <- "正在准备导出…"; tone <- ExportTone.Text; false, false, false, false
        let saveShown = ready
        let saveUsable = ready && not saving && not isEmpty
        { statusText = text; tone = tone; progressVisible = reading; indeterminate = indeterminate; progressValue = value
          retryVisible = failed; saveVisible = saveShown; saveJsonVisible = saveShown && outputJson.IsSome
          saveEnabled = saveUsable; saveJsonEnabled = (saveUsable && outputJson.IsSome)
          closeText = (if reading then "取消导出" else "关闭") }

    let refresh (force: bool) (phase: ExportPhase) (elapsed: float) (throttle: float) (throttlePending: bool) =
        let highFrequency =
            match phase with
            | ExportPhase.Reading _ -> true
            | _ -> false
        if force || not highFrequency || elapsed >= throttle then
            ExportRefresh.Now
        elif not throttlePending then
            ExportRefresh.Schedule(throttle - elapsed)
        else ExportRefresh.Skip
