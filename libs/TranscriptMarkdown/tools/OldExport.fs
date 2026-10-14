namespace Wanxiang.UI

open System

type AttachmentRef = {
    sha256: string
    size: int64
    mediaType: string
    fileName: string
}

module AttachmentRef =

    let isImage (r: AttachmentRef) =
        let m = if isNull r.mediaType then "" else r.mediaType.ToLowerInvariant()
        m.StartsWith("image/", StringComparison.Ordinal)

    let formatSize (n: int64) =
        if n < 1024L then sprintf "%d B" n
        elif n < 1024L * 1024L then sprintf "%.1f KiB" (float n / 1024.0)
        else sprintf "%.1f MiB" (float n / (1024.0 * 1024.0))

/// 一次工具调用及其结果。调用与结果分属两条消息，
type ToolCallView = {
    callId: string
    name: string
    argumentsJson: string
    /// 结果尚未返回时为 None（生成中为「执行中」，生成结束后为「结果未知」）
    result: string option
}

type MessageView = {
    /// user / assistant / tool / system
    role: string
    text: string
    /// 思维链单独提取，折叠展示
    reasoning: string
    toolCalls: ToolCallView list
    /// 本条消息里的工具结果（callId → 结果文本）
    toolResults: (string * string) list
    attachments: AttachmentRef list
    commitId: uint64 option
    /// 落盘时刻。流式临时消息为 None。
    committedAt: DateTimeOffset option
}

module MessageView =
    let hasVisibleBody (m: MessageView) =
        not (String.IsNullOrWhiteSpace m.text)
        || not (String.IsNullOrWhiteSpace m.reasoning)
        || not (List.isEmpty m.toolCalls)
        || not (List.isEmpty m.attachments)

/// 导出对话为 Markdown。
module Export =

    /// 标题不是路径；不同平台统一处理分隔符、控制字符和过长文件名。
    let safeFileName (title: string) =
        let source = if String.IsNullOrWhiteSpace title then "对话" else title
        let cleaned =
            source |> Seq.map (fun c -> if Char.IsControl c || "<>:\"/\\|?*".Contains c then '_' else c)
            |> Seq.toArray |> String
        let trimmed = cleaned.Trim().Trim('.')
        let trimmed = if String.IsNullOrWhiteSpace trimmed then "对话" else trimmed
        let elements = Globalization.StringInfo.GetTextElementEnumerator trimmed
        let builder = Text.StringBuilder()
        let mutable bytes = 0
        let mutable doneName = false
        while not doneName && elements.MoveNext() do
            let element = elements.GetTextElement()
            let size = Text.Encoding.UTF8.GetByteCount element
            if bytes + size > 220 then doneName <- true
            else
                builder.Append element |> ignore
                bytes <- bytes + size
        let bounded = if builder.Length = 0 then "对话" else builder.ToString()
        bounded + ".md"

    let private roleTitle (role: string) =
        match role with
        | "user" -> "用户"
        | "assistant" -> "助手"
        | "tool" -> "工具"
        | "system" -> "系统"
        | other -> other

    /// 生成可读的 Markdown 转录。工具调用与思考过程也保留，方便存档与复盘。
    let toMarkdown (title: string) (messages: MessageView list) : string =
        let builder = Text.StringBuilder()
        let appendCode (language: string) (text: string) =
            // 工具输出本身可能包含 Markdown 围栏，不能提前关闭导出的代码块。
            let mutable run, longest = 0, 0
            for c in text do
                run <- if c = '`' then run + 1 else 0
                longest <- max longest run
            let fence = String('`', max 3 (longest + 1))
            builder.AppendLine(fence + language).AppendLine(text).AppendLine(fence).AppendLine() |> ignore
        builder.AppendLine("# " + title).AppendLine() |> ignore
        // 导出按已提交消息逐条保留。不能沿用 UI 合并逻辑丢掉没有调用卡片的工具结果。
        for message in messages do
            if MessageView.hasVisibleBody message || not (List.isEmpty message.toolResults) then
                builder.AppendLine("## " + roleTitle message.role).AppendLine() |> ignore
                match message.committedAt with
                | Some at -> builder.AppendLine(at.ToString("o", Globalization.CultureInfo.InvariantCulture)).AppendLine() |> ignore
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
                    | Some result when not (String.IsNullOrWhiteSpace result) ->
                        appendCode "json" result
                    | _ -> ()
                for callId, result in message.toolResults do
                    builder.AppendLine(sprintf "**工具结果 `%s`**" callId).AppendLine() |> ignore
                    appendCode "json" result
                if not (String.IsNullOrWhiteSpace message.text) then
                    builder.AppendLine(message.text).AppendLine() |> ignore
                for attachment in message.attachments do
                    builder.AppendLine(sprintf "*附件：%s（%s）*" attachment.fileName (AttachmentRef.formatSize attachment.size)).AppendLine()
                    |> ignore
        builder.ToString()
