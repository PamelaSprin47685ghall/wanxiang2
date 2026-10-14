# ContinuableReply

半截回复是否可续写：助手角色且有非空白正文（从旧 ChatOrchestrator.PartialText 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ContinuableReply.hasText`。

# LEMMA
1. **行为与旧实现一致；形状不对一律为否，不抛。** [根 AXIOM: 5]
2. **这是独立的判定规则；保存与否、何时保存归组合者 ChatOrchestrator 与 PartialReplies。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
- compose/ChatOrchestrator/tests/ProductizationOrchestrationTests.fs 中断言 `ChatOrchestrator.PartialText` 的用例（该静态成员转发到本库）；本库 `助手且有正文才算可续写` 是同一组断言。
