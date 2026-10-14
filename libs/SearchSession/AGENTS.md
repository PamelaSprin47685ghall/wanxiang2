# SearchSession

消息正文搜索的会话状态（迁自旧 `Views/Sidebar.fs` 的 `searchHits`、`searchHitsQuery`、`searchPending`、`searchHasMore`、`searchTruncated`、`searchError`，方法 `BeginMessageSearch`、`LoadMoreMessageHits`、`RetryMessageSearch`、`ApplySearchResults`、`ApplySearchFailure`、`MessageHits`，`SetConnection` 断线清在途，`Rebuild` 命中区与状态行文字）。命中类型是类型参数，不认识 SearchHit。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

与 SearchRequery 的边界：SearchRequery 只回答「防抖到点要不要重发」，本库拥有会话状态；宿主把 `Query` 当作 SearchRequery 的上次查询词。防抖计时器、发请求、行控件归宿主。

# LEMMA
1. **命中只属于发起它的词。** 词变了旧命中不显示；迟到的旧词结果与失败都丢弃。
2. **在途时不重复请求「显示更多」**；断线清掉在途标志。
3. **状态行文字与旧 Rebuild 逐字相同。**

# SPEC
1. [LEMMA: 1, 2, 3] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- LibraryTests：`sidebar shows hit snippets under message content and clicking opens the hit`（状态部分；片段着色、点击打开要起窗口，留旧仓库）
