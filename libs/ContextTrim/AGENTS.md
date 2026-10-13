# ContextTrim

按条数与 token 粗估预算裁剪上下文，保留最近的消息，开头不留孤立工具结果（旧 GenerationContext.trim）。只依赖 Microsoft.Extensions.AI。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：模型看到的上下文与用户看到的标题是外部可见行为。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过；端到端由 ChatOrchestrator 金样与迁入的旧测试钉住。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/ChatOrchestrator/tests（逐条列在 ChatOrchestrator AGENTS.md）。
