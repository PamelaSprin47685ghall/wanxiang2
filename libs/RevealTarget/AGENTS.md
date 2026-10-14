# RevealTarget

打开对话后要滚动到的消息（迁自旧 `Model/ConversationSummary.fs`）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **目标不在已加载窗口时返回 None，由调用方决定是否加载更多。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- LibraryTests：`reveal targets resolve against the loaded window`
