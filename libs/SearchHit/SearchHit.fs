namespace Wanxiang.UI

open System
open System.Text.Json
open System.Text.Json.Nodes

type SearchHit = {
    conversationId: Guid
    title: string
    archived: bool
    /// 消息标识 = 消息 commitId
    messageId: uint64
    role: string
    snippet: string
    matchStart: int
    matchLength: int
}

/// 一页搜索结果的界面状态。
type SearchPageState = {
    queryId: Guid
    query: string
    hits: SearchHit list
    hasMore: bool
    /// 预算触顶，结果不完整——界面必须说出来
    truncated: bool
}

/// 数字节点的宽容读取：线上解析出的节点与进程内用 int / int64 构造的节点都能读（后者 `TryGetValue<uint64>` 会失败）。
module internal JsonNumbers =
    let tryUInt64 (n: JsonNode) : uint64 option =
        match n with
        | :? JsonValue as v ->
            match v.TryGetValue<uint64>() with
            | true, value -> Some value
            | _ ->
                match v.TryGetValue<int64>() with
                | true, value when value >= 0L -> Some(uint64 value)
                | _ ->
                    match v.TryGetValue<int>() with
                    | true, value when value >= 0 -> Some(uint64 value)
                    | _ -> None
        | _ -> None

module SearchHit =

    let private str (o: JsonObject) key =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.String then n.GetValue<string>() else ""

    let private num (o: JsonObject) key =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Number then
            JsonNumbers.tryUInt64 n |> Option.defaultValue 0UL
        else 0UL

    /// 解析命中；没有有效对话 id 的项无法打开，丢弃。
    let parse (items: JsonArray) : SearchHit list =
        [ for item in items do
              match item with
              | :? JsonObject as o ->
                  match Guid.TryParse(str o "conversationId") with
                  | true, id ->
                      let snippet = str o "snippet"
                      // 越界的高亮范围一律收敛到片段内，绝不抛异常
                      let start = int (min (num o "matchStart") (uint64 snippet.Length))
                      let length = int (min (num o "matchLength") (uint64 (snippet.Length - start)))
                      yield
                          { conversationId = id
                            title = str o "title"
                            archived = (let mutable n: JsonNode = null in o.TryGetPropertyValue("archived", &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.True)
                            messageId = num o "messageId"
                            role = str o "role"
                            snippet = snippet
                            matchStart = start
                            matchLength = length }
                  | _ -> ()
              | _ -> () ]

    /// 命中片段切成「前 / 命中 / 后」三段，供界面着色。
    let split (hit: SearchHit) : string * string * string =
        let s = hit.snippet
        s.Substring(0, hit.matchStart), s.Substring(hit.matchStart, hit.matchLength), s.Substring(hit.matchStart + hit.matchLength)

    /// 片段里的 Markdown 记号给人看是噪音（视觉走查 12 号截图：`### 关于…`、`**Markdown 渲染**`）。
    /// 只去掉标题井号、粗斜体星号/下划线对、行内代码反引号，并把换行折成空格；文字本身不动。
    let plainSnippet (text: string) : string =
        if String.IsNullOrEmpty text then text
        else
            let t = Text.RegularExpressions.Regex.Replace(text, @"(^|\n)\s{0,3}#{1,6}\s+", "$1")
            let t = Text.RegularExpressions.Regex.Replace(t, @"(\*\*|__|`+)", "")
            let t = Text.RegularExpressions.Regex.Replace(t, @"\s*\n\s*", " ")
            t

    /// 片段拆成「前 / 命中 / 后」三段，并各自去掉 Markdown 记号（命中位置不受影响）。
    let splitPlain (hit: SearchHit) : string * string * string =
        let before, matched, after = split hit
        plainSnippet before, matched, plainSnippet after

    /// 截断/不完整时给用户的说明；None = 结果完整。
    let truncationNotice (page: SearchPageState) : string option =
        if page.truncated then Some "结果已截断：只显示了最近的一部分匹配，请换更具体的词。" else None

/// 打开对话后要滚动到的消息。
