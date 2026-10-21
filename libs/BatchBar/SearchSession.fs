namespace Wanxiang.UI

/// 搜索状态行上的动作。
[<RequireQualifiedAccess>]
type SearchStatusAction =
    | Retry
    | LoadMore

module SearchStatusAction =
    let label (action: SearchStatusAction) =
        match action with
        | SearchStatusAction.Retry -> "重试"
        | SearchStatusAction.LoadMore -> "显示更多"

/// 命中区末尾的一行状态（文字为旧 Sidebar.Rebuild 原文）。
type SearchStatusLine =
    {
        text: string
        action: SearchStatusAction option
    }

/// 消息正文搜索的会话状态（迁自旧 Views/Sidebar.fs 的 searchHits、searchHitsQuery、searchPending、
/// searchHasMore、searchTruncated、searchError 及其读写方法）。
/// 命中只属于发起它的查询词，词变了就作废；迟到的旧词结果直接丢弃。
/// 防抖到点是否重发由 SearchRequery 判断（拿本对象的 Query 作上次查询词）；计时器与发请求归宿主。
type SearchSession<'hit>() =
    let mutable hits: 'hit list = []
    let mutable query = ""
    let mutable pending = false
    let mutable hasMore = false
    let mutable truncated = false
    let mutable error: string option = None

    static member Normalize(text: string) =
        (if isNull text then "" else text).Trim()

    /// 发起新查询。canSend：宿主能发（已接线且已连接）。返回应发出的查询词（跳过 0 条）；不发返回 None。
    member _.Begin(text: string, canSend: bool) : string option =
        let q = SearchSession<'hit>.Normalize text
        hits <- []
        hasMore <- false
        truncated <- false
        error <- None

        if canSend && q <> "" then
            query <- q
            pending <- true
            Some q
        else
            query <- ""
            pending <- false
            None

    /// 「显示更多」：返回 (查询词, 跳过条数)；没有查询或已在途时为 None。
    member _.LoadMore() : (string * int) option =
        if query <> "" && not pending then
            pending <- true
            Some(query, List.length hits)
        else
            None

    /// 失败后重试：返回 (查询词, 跳过条数)；没有查询时为 None。
    member _.Retry() : (string * int) option =
        if query <> "" then
            error <- None
            pending <- true
            Some(query, List.length hits)
        else
            None

    /// 一页结果到达；offset > 0 是追加页。返回是否被接受（词已变的迟到结果返回 false）。
    member _.Apply(pageQuery: string, pageHits: 'hit list, pageHasMore: bool, pageTruncated: bool, offset: int) : bool =
        if (SearchSession<'hit>.Normalize pageQuery) = query && query <> "" then
            hits <- if offset <= 0 then pageHits else hits @ pageHits
            hasMore <- pageHasMore
            truncated <- pageTruncated
            pending <- false
            error <- None
            true
        else
            false

    /// 查询失败。返回是否属于当前查询词。
    member _.Fail(failedQuery: string, message: string) : bool =
        if (SearchSession<'hit>.Normalize failedQuery) = query && query <> "" then
            pending <- false
            error <- Some message
            true
        else
            false

    /// 断开连接：在途结果不会再来，不让「正在搜索」挂着。
    member _.Disconnected() = pending <- false

    /// 上次已发出查询的词（无则为空串）。
    member _.Query = query
    member _.IsPending = pending

    /// 当前正在显示的命中。
    member _.Hits: 'hit list = if query <> "" then hits else []

    /// 搜索框当前词对应的命中区：词与命中所属的词一致，且在途、失败或有命中时才显示。
    /// 返回 (命中, 状态行)；不显示为 None。
    member _.Section(boxText: string) : ('hit list * SearchStatusLine list) option =
        let q = SearchSession<'hit>.Normalize boxText
        let current = q <> "" && query = q

        if current && (pending || error.IsSome || not (List.isEmpty hits)) then
            let lines =
                match error with
                | Some message ->
                    [
                        {
                            text = sprintf "消息搜索失败：%s" message
                            action = Some SearchStatusAction.Retry
                        }
                    ]
                | None when pending ->
                    [
                        {
                            text = (if List.isEmpty hits then "正在搜索消息…" else "正在加载更多…")
                            action = None
                        }
                    ]
                | None ->
                    [
                        if hasMore then
                            {
                                text = "还有更多匹配的消息"
                                action = Some SearchStatusAction.LoadMore
                            }
                        if truncated then
                            {
                                text = "结果已截断：只显示了最近的一部分匹配，请换更具体的词。"
                                action = None
                            }
                    ]

            Some(hits, lines)
        else
            None
