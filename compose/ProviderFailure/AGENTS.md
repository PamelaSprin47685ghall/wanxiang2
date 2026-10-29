# ProviderFailure（组合）

旧 `ProviderFailure`：把 Provider 调用异常归类成 GenerationError，并读出限流信号。协作者：HttpFailureFacts、Wire、System.ClientModel。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **归类、文字、细节、重试判断与旧实现一致。** [根 AXIOM: 5]
2. **从异常读事实与按事实归类分开**：前者是 HttpFailureFacts 库。 [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] 本组合 `tests/Tests.fs` 的 `timeouts and refused connections are classified` 及 AgentRuntime 金样（`agent runtime behaves byte for byte like the old implementation`）覆盖 17 种异常与真实 HTTP 429/500/400，通过。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/AgentRuntime/tests（逐条列在 AgentRuntime AGENTS.md）。
