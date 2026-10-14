namespace Wanxiang.UI

open System

/// 搜索命中切分：把一行文本按查询词切成「命中 / 非命中」交替片段，供标题与预览
/// 上色高亮。分段是纯函数——测试直接断言，不需要起窗口。
///
/// 为什么不是 TextBlock.Text 高亮：Avalonia 的 TextBlock.Text 是纯字符串，
/// 要局部上色只能靠 Inlines 里的 Run。这里只负责「切成哪几段、哪段着色」，
/// 视觉决策（颜色、字号、加粗）留给调用方。
///
/// 分段口径与 ConversationSummary.matches 完全一致（空格分词、忽略大小写、
/// 每个词都必须参与匹配）：搜索能返回这行，这里就一定会标出使它返回的那些词。
/// 两者的口径一旦漂移，就会出现「行被搜出来了却看不见哪里匹配」。
module SidebarText =

    /// 一个文本片段：Text 为内容，IsMatch 表示该片段应被视为查询命中。
    type Segment = { text: string; isMatch: bool }

    /// 与 ConversationSummary.matches 同一套分词规则，返回参与匹配的非空词表。
    let terms (query: string) : string list =
        if String.IsNullOrWhiteSpace query then []
        else
            query.Trim().Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.distinct
            |> List.ofArray

    /// 把 text 按 terms 切成有序片段。
    ///
    /// 规则：
    /// - 空查询或空文本返回单段非命中（调用方就该当普通文本渲染）。
    /// - 命中片段按出现次序从左到右取；多个词命中同一区域时不重叠重复取，
    ///   全部命中片段先合并排序再去重，保证 text 被完整覆盖一次。
    /// - 大小写不敏感比较，但保留原文片段（不是查询词本身），长文本不被改写。
    let segments (query: string) (text: string) : Segment list =
        let words = terms query
        if List.isEmpty words || String.IsNullOrEmpty text then
            let safeText = if isNull text then "" else text
            [ { text = safeText; isMatch = false } ]
        else
            // 每个词单独扫一遍，收集命中的 [start, end) 区间。
            let raw =
                [ for word in words do
                    if String.IsNullOrEmpty word then ()
                    else
                        let mutable start = 0
                        let mutable keep = true
                        while keep do
                            let index = text.IndexOf(word, start, StringComparison.OrdinalIgnoreCase)
                            if index < 0 then keep <- false
                            else
                                yield (index, index + word.Length)
                                start <- index + word.Length
                                if start >= text.Length then keep <- false ]
            if List.isEmpty raw then
                [ { text = text; isMatch = false } ]
            else
                // 排序后合并重叠/相邻区间：相邻合并让「两个词挨着」也连成一段高亮，
                // 视觉上是一整块而不是两段中间被底色切碎。fold 一路保持列表有序，
                // 累加时按「最后一段」比较，新区间只接到末尾——不引用中间段。
                let sorted = raw |> List.sortBy fst
                let merged =
                    (sorted, [])
                    ||> List.fold (fun acc (s, e) ->
                        match acc with
                        | [] -> [ (s, e) ]
                        | prev ->
                            let ps, pe = List.last prev
                            if s <= pe then
                                // 与末段重叠或紧邻：把末段延长（max 保证完全包含不被砍短）。
                                (List.truncate (List.length prev - 1) prev) @ [ (ps, max pe e) ]
                            else
                                prev @ [ (s, e) ])
                // 从合并后的区间还原完整文本，空隙补非命中段。
                let spans =
                    [ let mutable cursor = 0
                      for (s, e) in merged do
                          if s > cursor then yield { text = text.Substring(cursor, s - cursor); isMatch = false }
                          yield { text = text.Substring(s, e - s); isMatch = true }
                          cursor <- e
                      if cursor < text.Length then
                          yield { text = text.Substring(cursor); isMatch = false } ]
                spans
