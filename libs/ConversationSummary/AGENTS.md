# ConversationSummary

侧栏对话摘要：解析 `conversation-list.snapshot`、搜索匹配、排序分组、同名区分（迁自旧 `Model/ConversationSummary.fs` 前半）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **宽容读线上行。** [根 AXIOM: 5] 坏日期退回 UnixEpoch、无效 id 丢弃，旧主机发来的行不让侧栏崩溃。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 的 `ConversationSummary matching, sorting and grouping`、`ConversationSummary parseList handles valid, empty and invalid records`、`ConversationSummary matches searches single and multi-word keywords`  通过。

# 迁入或覆盖的旧测试
- UiPureLogicTests：`ConversationSummary matching, sorting and grouping`
- UiModelTests：`ConversationSummary parseList handles valid, empty and invalid records`、`ConversationSummary matches searches single and multi-word keywords`
- R19SimplifyTests：`361 同名对话附时间，不同名不附`
