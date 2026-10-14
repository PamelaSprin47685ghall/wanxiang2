# SearchHit

全文搜索命中：解析 `search.results`、片段切分与去 Markdown、截断说明（迁自旧 `Model/ConversationSummary.fs`）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **越界高亮收敛到片段内，绝不抛异常。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- LibraryTests：`search hit parsing clamps bad highlight ranges and drops rows without ids`
- SearchHighlightTests：`消息命中片段去掉 Markdown 记号，命中词保持原样`
