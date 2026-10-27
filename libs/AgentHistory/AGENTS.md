# AgentHistory

`WanxiangHistoryProvider`：Agent Framework 的 ChatHistoryProvider，调用前加载已提交历史，调用后把响应或失败回调给编排层。源码原样迁自旧 HistoryProvider.fs。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：这些行为直接决定用户看到的错误、发给 Provider 的请求与落盘消息。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 的 `the conversation id key is stable`  通过；端到端字节由 AgentRuntime 金样钉住。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/AgentRuntime/tests（逐条列在 AgentRuntime AGENTS.md）。
