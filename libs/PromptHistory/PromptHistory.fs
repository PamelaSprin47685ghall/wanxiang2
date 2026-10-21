namespace Wanxiang.UI

open System

/// 输入框的发送历史（shell 风格，全局共享，不随对话隔离）：上下键召回，Esc 退回半截草稿。
///
/// 召回期间输入框显示的是旧发送，真正的半截草稿由本对象保管（`Draft`）。
/// 方法返回 Some 文本表示输入框应换成该文本并把光标放到末尾（同时该按键算已处理）；None 表示不处理。
type PromptHistory(capacity: int) =
    let entries = ResizeArray<string>()
    // -1 表示没在召回
    let mutable index = -1
    let mutable draft = ""

    new() = PromptHistory(100)

    /// 上键：单行时光标在行首或输入为空、多行时光标在最开头才召回；召回中继续往更早翻。
    member _.Up(currentText: string, caret: int, hasSelection: bool) : string option =
        if hasSelection || entries.Count = 0 then
            None
        else
            let text = if isNull currentText then "" else currentText
            let isMultiline = text.IndexOfAny([| '\n'; '\r' |]) >= 0

            let hasNewlineBeforeCaret =
                let idx = Math.Clamp(caret, 0, text.Length)
                text.Substring(0, idx).IndexOfAny([| '\n'; '\r' |]) >= 0

            let canRecall =
                if isMultiline then not hasNewlineBeforeCaret && caret = 0
                elif index <> -1 then true
                else text.Length = 0 || caret = 0

            if not canRecall then
                None
            else
                if index = -1 then
                    draft <- text
                    index <- entries.Count - 1
                elif index > 0 then
                    index <- index - 1

                Some entries.[index]

    /// 下键：召回中且光标在末尾才处理；翻过最新一条就回到半截草稿。
    member _.Down(currentText: string, caret: int, hasSelection: bool) : string option =
        if hasSelection || index = -1 then
            None
        else
            let text = if isNull currentText then "" else currentText

            if caret < text.Length then
                None
            elif index < entries.Count - 1 then
                index <- index + 1
                Some entries.[index]
            else
                index <- -1
                Some draft

    /// Esc：召回中则退回半截草稿。
    member _.Escape() : string option =
        if index = -1 then
            None
        else
            index <- -1
            Some draft

    /// 发送已被接受：非空且与最近一条不同才记入，超过容量丢最旧的；召回状态清零。
    member _.Record(text: string) =
        if not (String.IsNullOrWhiteSpace text) then
            let isDuplicate = entries.Count > 0 && entries.[entries.Count - 1] = text

            if not isDuplicate then
                entries.Add text

                if entries.Count > capacity then
                    entries.RemoveAt 0

        index <- -1
        draft <- ""

    /// 输入框被外部改写（编辑重发、模板、清空）：退出召回。
    member _.Reset() =
        index <- -1
        draft <- ""

    /// 该保存的输入：召回中是半截草稿，否则是输入框当前文本。
    member _.Draft(currentText: string) =
        if index <> -1 then draft
        elif isNull currentText then ""
        else currentText

    member _.IsRecalling = index <> -1
