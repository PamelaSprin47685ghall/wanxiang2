namespace Wanxiang.Server

open System

/// 对话标题的文字规则：限长、取首行、去引号与句末标点、取首句、认出系统占位标题。只依赖 BCL。
/// 从旧 TitleGenerator 拆出；Markdown 清洗归调用方（PlainText）。行为不变。
module TitleText =

    /// 标题最长字符数：侧栏一行能放下的量级。
    let maxLength = 24

    /// 超长截断并加省略号。
    let clip (s: string) : string =
        if s.Length <= maxLength then
            s
        else
            s.Substring(0, maxLength) + "…"

    /// 第一行非空行；没有就原样。
    let firstLine (raw: string) : string =
        raw.Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.tryHead
        |> Option.defaultValue raw

    /// 去掉两端引号、句号、冒号与空白。
    let stripQuotes (s: string) : string =
        s.Trim('"', '\'', '「', '」', '“', '”', '.', '。', ':', '：').Trim()

    /// 首句：遇到句末标点且前面至少 4 个字符时截断。
    let firstSentence (s: string) : string =
        let stops = [| '。'; '！'; '？'; '\n'; '.'; '!'; '?'; ';'; '；' |]

        match s.IndexOfAny stops with
        | -1 -> s
        | index when index >= 4 -> s.Substring(0, index)
        | _ -> s

    /// 系统生成的占位标题（只有占位标题才自动改名，用户手动命名的绝不覆盖）。
    let isPlaceholder (title: string) : bool =
        if String.IsNullOrWhiteSpace title then
            true
        else
            let t = title.Trim()

            t = "新对话"
            || t = "新会话"
            || t.StartsWith("对话 ", StringComparison.Ordinal)
            || t.StartsWith("会话 ", StringComparison.Ordinal)
