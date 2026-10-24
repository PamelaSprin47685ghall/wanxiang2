# HttpFailureFacts

从 HTTP 调用失败里读出事实：Retry-After（头值、响应、异常 Data）、是否网络不可达、文字里的 5xx、精简细节。只依赖 BCL，从旧 ProviderFailure 拆出。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：这些行为直接决定用户看到的错误、发给 Provider 的请求与落盘消息。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过；远期 `Retry-After` 日期或超大整数秒收敛到 `Int32.MaxValue` 而不因浮点转 `int` 溢出成负数，带小数点的纯数值串不会被 `DateTimeOffset.TryParse` 误当成「月.日」日期；端到端字节由 AgentRuntime 金样钉住。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/AgentRuntime/tests（逐条列在 AgentRuntime AGENTS.md）。
