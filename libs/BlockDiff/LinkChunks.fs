namespace Wanxiang.UI

open System
open System.Globalization
open System.Text

/// 超长链接的分段：按字素簇切片，再按行首行尾禁则挪动边界标点。
module LinkChunks =

    /// 按字素簇切片，不在代理对或 emoji 序列中间拆分（旧 MarkdownRenderer.SafeChunk）。
    let safeChunk (chunkSize: int) (str: string) : string list =
        if String.IsNullOrEmpty str || chunkSize <= 0 then
            []
        else
            let enumerator = StringInfo.GetTextElementEnumerator(str)
            let chunks = ResizeArray<string>()
            let current = StringBuilder()
            let mutable count = 0

            while enumerator.MoveNext() do
                current.Append(enumerator.GetTextElement()) |> ignore
                count <- count + 1

                if count = chunkSize then
                    chunks.Add(current.ToString())
                    current.Clear() |> ignore
                    count <- 0

            if current.Length > 0 then
                chunks.Add(current.ToString())

            chunks |> Seq.toList

    /// 段首的收标点并回上一段尾，段尾的开标点顺到下一段首（旧 MarkdownRenderer.RebalanceLinkChunks）。
    let rebalance (chunks: string list) : string list =
        let closers =
            set
                [
                    '。'
                    '，'
                    '、'
                    '；'
                    '：'
                    '！'
                    '？'
                    '”'
                    '’'
                    '）'
                    '］'
                    '｝'
                    '〉'
                    '》'
                    '…'
                    '—'
                    '.'
                    ','
                    ';'
                    ':'
                    '!'
                    '?'
                    ')'
                    ']'
                    '}'
                ]

        let openers = set [ '「'; '『'; '（'; '［'; '｛'; '〈'; '《'; '“'; '‘'; '('; '['; '{' ]
        let arr = chunks |> List.toArray

        for i in 1 .. arr.Length - 1 do
            if arr[i].Length > 1 && closers.Contains arr[i].[0] then
                arr[i - 1] <- arr[i - 1] + string arr[i].[0]
                arr[i] <- arr[i].Substring(1)

        for i in 0 .. arr.Length - 2 do
            if arr[i].Length > 1 && openers.Contains arr[i].[arr[i].Length - 1] then
                let last = arr[i].[arr[i].Length - 1]
                arr[i] <- arr[i].Substring(0, arr[i].Length - 1)
                arr[i + 1] <- string last + arr[i + 1]

        arr |> Array.toList |> List.filter (not << String.IsNullOrEmpty)
