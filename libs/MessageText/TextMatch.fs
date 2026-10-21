namespace Wanxiang.Server

open System

/// 多词全匹配与命中片段。不懂消息、对话和协议。
module TextMatch =

    /// 按空格、制表符、换行分词并去重。
    let terms (query: string) : string list =
        if String.IsNullOrWhiteSpace query then
            []
        else
            query.Trim().Split([| ' '; '\t'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.distinct
            |> List.ofArray

    /// 所有词（大小写不敏感）都出现时，返回最靠前那个词的 (下标, 长度)；否则 None。
    let firstMatch (words: string list) (text: string) : (int * int) option =
        let mutable best = (Int32.MaxValue, 0)
        let mutable all = true

        for w in words do
            if all then
                let i = text.IndexOf(w, StringComparison.OrdinalIgnoreCase)

                if i < 0 then
                    all <- false
                elif i < fst best then
                    best <- (i, w.Length)

        if all then Some best else None

    /// 以命中为中心截取约 total 个字符：命中前留 30 字符，换行压成空格（一对一，下标不变），
    /// 不切开代理对，两端被截时加省略号。返回 (片段, 命中在片段中的起点)。
    let snippetAround (total: int) (text: string) (matchStart: int) (matchLength: int) : string * int =
        let lead = 30
        let mutable start = max 0 (matchStart - lead)

        if start > 0 && Char.IsLowSurrogate text[start] then
            start <- start - 1

        let mutable stop = min text.Length (start + max total (matchLength + lead))

        if stop < text.Length && stop > 0 && Char.IsHighSurrogate text[stop - 1] then
            stop <- stop - 1

        let body = text.Substring(start, stop - start).Replace('\n', ' ').Replace('\r', ' ')
        let prefix = if start > 0 then "…" else ""
        let suffix = if stop < text.Length then "…" else ""
        prefix + body + suffix, prefix.Length + (matchStart - start)
