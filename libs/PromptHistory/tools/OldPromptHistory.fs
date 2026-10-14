namespace Wanxiang.UI

open System

/// 旧 Composer 里发送历史的状态与按键分支，逐行照抄（input.Text / CaretIndex 换成参数与返回值）
type PromptHistory() =
    let promptHistory = ResizeArray<string>()
    let mutable historyIndex = -1
    let mutable uncommittedDraft = ""
    let maxHistoryCount = 100

    member _.Up(currentText: string, caret: int, hasSelection: bool) : string option =
        let mutable shown = None
        if not hasSelection && promptHistory.Count > 0 then
            let currentText = if isNull currentText then "" else currentText
            let isMultiline = currentText.IndexOfAny([| '\n'; '\r' |]) >= 0
            let hasNewlineBeforeCaret =
                let idx = Math.Clamp(caret, 0, currentText.Length)
                currentText.Substring(0, idx).IndexOfAny([| '\n'; '\r' |]) >= 0
            let canRecall =
                if isMultiline then
                    not hasNewlineBeforeCaret && caret = 0
                elif historyIndex <> -1 then
                    true
                else
                    currentText.Length = 0 || caret = 0
            if canRecall then
                if historyIndex = -1 then
                    uncommittedDraft <- currentText
                    historyIndex <- promptHistory.Count - 1
                elif historyIndex > 0 then
                    historyIndex <- historyIndex - 1
                shown <- Some promptHistory.[historyIndex]
        shown

    member _.Down(currentText: string, caret: int, hasSelection: bool) : string option =
        let mutable shown = None
        if not hasSelection && historyIndex <> -1 then
            let currentText = if isNull currentText then "" else currentText
            let isAtEnd = caret >= currentText.Length
            if isAtEnd then
                if historyIndex < promptHistory.Count - 1 then
                    historyIndex <- historyIndex + 1
                    shown <- Some promptHistory.[historyIndex]
                else
                    historyIndex <- -1
                    shown <- Some uncommittedDraft
        shown

    member _.Escape() : string option =
        if historyIndex <> -1 then
            historyIndex <- -1
            Some uncommittedDraft
        else None

    member _.Record(text: string) =
        if not (String.IsNullOrWhiteSpace text) then
            let isDuplicate = promptHistory.Count > 0 && promptHistory.[promptHistory.Count - 1] = text
            if not isDuplicate then
                promptHistory.Add text
                if promptHistory.Count > maxHistoryCount then
                    promptHistory.RemoveAt 0
        historyIndex <- -1
        uncommittedDraft <- ""

    member _.Reset() =
        historyIndex <- -1
        uncommittedDraft <- ""

    member _.Draft(currentText: string) =
        if historyIndex <> -1 then uncommittedDraft
        elif isNull currentText then ""
        else currentText

    member _.IsRecalling = historyIndex <> -1
