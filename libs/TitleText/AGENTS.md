# TitleText

对话标题的文字规则：限长、首行、去引号、首句、占位标题判定（从旧 TitleGenerator 拆出）。只依赖 BCL；Markdown 清洗归调用方。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：模型看到的上下文与用户看到的标题是外部可见行为。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过；端到端由 ChatOrchestrator 金样与迁入的旧测试钉住。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/ChatOrchestrator/tests（逐条列在 ChatOrchestrator AGENTS.md）。
