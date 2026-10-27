# ForkLineage

分叉谱系的类型与解析（迁自旧 `Model/ConversationSummary.fs`；面包屑和版本切换要对照列表行，在 compose/ConversationList）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **字段缺省退回只显示分叉标记。** [根 AXIOM: 5] 旧主机不发谱系字段时不出错。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 的 `only rows with a parent enter the table and missing fields stay None`、`negative or non-numeric message ids are ignored`  通过。

# 迁入或覆盖的旧测试
- 本库只有新测试；旧 LibraryTests 的谱系用例迁在 compose/ConversationList。
