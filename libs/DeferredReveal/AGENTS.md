# DeferredReveal

延迟显示的武装状态（迁自旧 `AppShell.fs` 的 `ShowConversationLoadingDeferred`、`DisarmSkeletonLoading`，字段 `skeletonArmedFor`）。计时器、骨架与标题仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一个键只武装一次。** [根 AXIOM: 5] 已为同一键武装时，再次武装不重启计时（D1：后台渲染不能把骨架无限顺延）；到点不解除武装，与旧实现相同。
2. **过期的计时不做事。** 解除或换键之后，旧键到点返回 false。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。延迟 300ms 与旧 `MotionLedger.conversationSkeletonDelay` 相同。

# 迁入或覆盖的旧测试
旧仓库没有直接测这段的用例（骨架相关用例测的是 Tokens 与 ChatView 控件）。本库新增 3 条行为用例。
