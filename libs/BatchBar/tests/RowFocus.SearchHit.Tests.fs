module RowFocusSearchHitTests

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.UI

// 迁自旧 LibraryTests（该用例在旧文件里只用纯逻辑）。
[<Fact>]
let ``search hit parsing clamps bad highlight ranges and drops rows without ids`` () =
    let items = JsonArray()
    let bad = JsonObject()
    bad["conversationId"] <- Guid.NewGuid().ToString("D")
    bad["snippet"] <- "abc"
    bad["matchStart"] <- 99
    bad["matchLength"] <- 99
    bad["messageId"] <- 5UL
    items.Add bad
    let noId = JsonObject()
    noId["snippet"] <- "x"
    items.Add noId
    let hits = SearchHit.parse items
    Assert.Equal(1, hits.Length)
    Assert.Equal(5UL, hits.Head.messageId)
    let before, matched, after = SearchHit.split hits.Head
    Assert.Equal(("abc", "", ""), (before, matched, after))

// 迁自旧 SearchHighlightTests。
[<Fact>]
let ``消息命中片段去掉 Markdown 记号，命中词保持原样`` () =
    let hit: SearchHit =
        {
            conversationId = Guid.NewGuid()
            title = "排版验证"
            archived = false
            messageId = 1UL
            role = "assistant"
            snippet = "### 关于排版\n这是验证 **Markdown 渲染**、`行内代码`"
            matchStart = 16
            matchLength = 8
        }

    let before, matched, after = SearchHit.splitPlain hit
    Assert.Equal("Markdown", matched)
    Assert.Equal("关于排版 这是验证 ", before)
    Assert.Equal(" 渲染、行内代码", after)

[<Fact>]
let ``truncated page says so`` () =
    let page =
        {
            queryId = Guid.Empty
            query = "q"
            hits = []
            hasMore = false
            truncated = true
        }

    Assert.Equal(Some "结果已截断：只显示了最近的一部分匹配，请换更具体的词。", SearchHit.truncationNotice page)
    Assert.Equal(None, SearchHit.truncationNotice { page with truncated = false })
