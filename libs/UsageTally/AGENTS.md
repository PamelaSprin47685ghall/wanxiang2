# UsageTally

一次生成里逐轮累加的 token 用量（从旧 ChatOrchestrator 的 addUsage / usageFromDetails 拆出）。依赖 Microsoft.Extensions.AI.Abstractions。根规则见项目根目录 AGENTS.md。

公开面：`UsageTally()`、`Add(UsageDetails)`、`PromptTokens`、`CompletionTokens`、`CachedTokens`、`TotalTokens`。

# LEMMA
1. **行为与旧实现一致：某项有一轮报告过就有值，从没报告过保持 None。** [根 AXIOM: 5]
2. **对象自己拥有累加状态；耗时、何时计入、怎么拼成线上 GenerationUsage 归组合者 ChatOrchestrator。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过（空、多轮、缺项、0 与 None 的区别）。

# 迁入或覆盖的旧测试
- 经 compose/ChatOrchestrator 的金样（逐字节，含工具循环多轮 usage）与迁入的 ChatOrchestratorTests 覆盖。
