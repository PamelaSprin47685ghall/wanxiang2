# AguiMessageMap

`MessageMap`：MAF ChatMessage 与 AG-UI 消息对象互转。源码原样迁自旧 MessageMap.fs（去掉没用到的 Wanxiang.Core 引用）。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：这些行为直接决定用户看到的错误、发给 Provider 的请求与落盘消息。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过；端到端字节由 AgentRuntime 金样钉住。

# 迁入或覆盖的旧测试
- 无旧测试直接针对 MessageMap；旧仓库不需删除测试。
