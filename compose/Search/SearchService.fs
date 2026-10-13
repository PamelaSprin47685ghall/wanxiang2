namespace Wanxiang.Server

open System
open System.Text.Json
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol

/// 全文搜索（W4，`wanxiang-protocol` 决策 201）。
///
/// 只读：输入是不可变 `Projection` 快照，输出是一页命中。不建数据库、不建持久索引、
/// 不写日志、不创建文件——内存投影是唯一事实来源，搜索只是它的一次查询。
///
/// 语义：
/// - 查询按空白分词，**全部**词（大小写不敏感）都出现在同一条消息正文里才算命中，
///   与侧栏标题过滤、高亮使用同一套分词（`SidebarText.terms`）。
/// - 只匹配消息自己记录的正文（`text` 内容项）；思维链、工具参数与工具结果不参与。
/// - 只搜每个对话**自己记录**的消息，不搜继承自父对话的前缀：同一条历史消息不会因
///   被多个分叉共享而重复命中。
/// - 已删除对话、已 tombstone 的消息永不出现；归档对话由 `includeArchived` 决定。
/// - 顺序：最近活动的对话优先，同一对话内新消息优先（"最近优先"，预算耗尽时丢的是最老的）。
/// - 预算见 `SearchLimits`：命中数、扫描消息数、单条检查字符数都有上限，
///   触顶时 `truncated = true`，绝不静默丢结果。
module SearchService =

    /// 与 `SidebarText.terms` 同一规则：空格、制表符分词并去重（库 TextMatch）。
    let terms (query: string) : string list = TextMatch.terms query

    /// 提取消息的可搜索正文：只取 `text` 内容项，跳过思维链；单条上限 `maxMessageChars`（库 MessageText）。
    let textOf (payload: JsonNode) : string = MessageText.visible SearchLimits.maxMessageChars payload

    let private firstMatch = TextMatch.firstMatch

    /// 以命中为中心截取片段（库 TextMatch，长度取 `SearchLimits.snippetChars`）。
    let snippetAround (text: string) (matchStart: int) (matchLength: int) : string * int =
        TextMatch.snippetAround SearchLimits.snippetChars text matchStart matchLength

    /// 对内存投影做一次查询。
    let search (proj: Projection<'C>) (q: SearchQueryData) : Result<SearchResultsData, string> =
        if q.query.Length > SearchLimits.maxQueryChars then
            Error(sprintf "搜索词太长（上限 %d 个字符），请缩短后重试。" SearchLimits.maxQueryChars)
        else
            let words = terms q.query
            let offset = max 0 q.offset
            let limit = q.limit |> max 1 |> min SearchLimits.maxPageSize
            let items = JsonArray()
            let mutable hits = 0
            let mutable scanned = 0
            let mutable truncated = false
            let mutable hasMore = false
            let mutable stop = List.isEmpty words
            let conversations =
                proj.conversations.Values
                |> Seq.filter (fun c -> not c.deleted && (q.includeArchived || not c.archived))
                |> Seq.sortByDescending (fun c -> c.lastCommitId |> Option.defaultValue 0UL)
            use e = conversations.GetEnumerator()
            while not stop && e.MoveNext() do
                let c = e.Current
                let mutable rest = List.rev c.messages
                while not stop && not (List.isEmpty rest) do
                    let m = rest.Head
                    rest <- rest.Tail
                    if m.deletedAtCommitId.IsNone then
                        if scanned >= SearchLimits.maxScannedMessages then
                            truncated <- true
                            stop <- true
                        else
                            scanned <- scanned + 1
                            let text = textOf m.payloadJson
                            match firstMatch words text with
                            | None -> ()
                            | Some(index, length) ->
                                if hits >= SearchLimits.maxHits then
                                    truncated <- true
                                    stop <- true
                                elif hits >= offset + limit then
                                    hasMore <- true
                                    stop <- true
                                else
                                    if hits >= offset then
                                        let snippet, start = snippetAround text index length
                                        let o = JsonObject()
                                        o["conversationId"] <- c.conversationId.ToString("D")
                                        o["title"] <- c.title
                                        o["archived"] <- c.archived
                                        o["messageId"] <- m.commitId
                                        o["commitId"] <- m.commitId
                                        o["role"] <-
                                            (let mutable r: JsonNode = null
                                             if (match m.payloadJson with
                                                 | :? JsonObject as po -> po.TryGetPropertyValue("role", &r) && not (isNull r) && r.GetValueKind() = JsonValueKind.String
                                                 | _ -> false)
                                             then r.GetValue<string>() else "")
                                        o["snippet"] <- snippet
                                        o["matchStart"] <- start
                                        o["matchLength"] <- length
                                        items.Add o
                                    hits <- hits + 1
            Ok { queryId = q.queryId
                 query = q.query
                 offset = offset
                 items = items
                 hasMore = hasMore
                 truncated = truncated
                 atCommitId = proj.latestCommitId }
