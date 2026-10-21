namespace Wanxiang.UI

open System

/// 转录里的一次工具调用。
type TranscriptToolCall =
    {
        name: string
        argumentsJson: string
        result: string option
    }

/// 转录里的一个附件：文件名与已格式化好的大小。
type TranscriptAttachment = { fileName: string; sizeText: string }

/// 转录里的一条已提交消息。
type TranscriptMessage =
    {
        role: string
        committedAt: DateTimeOffset option
        reasoning: string
        toolCalls: TranscriptToolCall list
        /// callId → 结果文本
        toolResults: (string * string) list
        text: string
        attachments: TranscriptAttachment list
    }

/// 导出对话为 Markdown（旧 Views/Brand.fs 的 Export.toMarkdown）。
module TranscriptMarkdown =

    let private roleTitle (role: string) =
        match role with
        | "user" -> "用户"
        | "assistant" -> "助手"
        | "tool" -> "工具"
        | "system" -> "系统"
        | other -> other

    /// 有正文、思考、工具调用或附件，才算可见（旧 MessageView.hasVisibleBody）。
    let private hasVisibleBody (m: TranscriptMessage) =
        not (String.IsNullOrWhiteSpace m.text)
        || not (String.IsNullOrWhiteSpace m.reasoning)
        || not (List.isEmpty m.toolCalls)
        || not (List.isEmpty m.attachments)

    /// 生成可读的 Markdown 转录。工具调用与思考过程也保留，方便存档与复盘。
    let render (title: string) (messages: TranscriptMessage list) : string =
        let builder = Text.StringBuilder()

        let appendCode (language: string) (text: string) =
            // 工具输出本身可能包含 Markdown 围栏，不能提前关闭导出的代码块。
            let mutable run, longest = 0, 0

            for c in text do
                run <- if c = '`' then run + 1 else 0
                longest <- max longest run

            let fence = String('`', max 3 (longest + 1))

            builder.AppendLine(fence + language).AppendLine(text).AppendLine(fence).AppendLine()
            |> ignore

        builder.AppendLine("# " + title).AppendLine() |> ignore
        // 导出按已提交消息逐条保留。不能沿用 UI 合并逻辑丢掉没有调用卡片的工具结果。
        for message in messages do
            if hasVisibleBody message || not (List.isEmpty message.toolResults) then
                builder.AppendLine("## " + roleTitle message.role).AppendLine() |> ignore

                match message.committedAt with
                | Some at ->
                    builder.AppendLine(at.ToString("o", Globalization.CultureInfo.InvariantCulture)).AppendLine()
                    |> ignore
                | None -> ()

                if not (String.IsNullOrWhiteSpace message.reasoning) then
                    builder.AppendLine("> 思考过程").AppendLine(">") |> ignore

                    for line in message.reasoning.Split '\n' do
                        builder.AppendLine("> " + line.TrimEnd()) |> ignore

                    builder.AppendLine() |> ignore

                for call in message.toolCalls do
                    builder.AppendLine(sprintf "**调用工具 `%s`**" call.name).AppendLine() |> ignore

                    if not (String.IsNullOrWhiteSpace call.argumentsJson) then
                        appendCode "json" call.argumentsJson

                    match call.result with
                    | Some result when not (String.IsNullOrWhiteSpace result) -> appendCode "json" result
                    | _ -> ()

                for callId, result in message.toolResults do
                    builder.AppendLine(sprintf "**工具结果 `%s`**" callId).AppendLine() |> ignore
                    appendCode "json" result

                if not (String.IsNullOrWhiteSpace message.text) then
                    builder.AppendLine(message.text).AppendLine() |> ignore

                for attachment in message.attachments do
                    builder.AppendLine(sprintf "*附件：%s（%s）*" attachment.fileName attachment.sizeText).AppendLine()
                    |> ignore

        builder.ToString()
