# MessageSerde

ChatMessage 与 JSON 互转（Microsoft.Extensions.AI 的 JSON 配置），宽松解析客户端手写 JSON，抽取 `{"type":"attachment"}` 附件引用。源码原样迁自旧 `Wanxiang.Agent.MessageSerde`，去掉了没用到的 Wanxiang.Core 引用。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：落盘消息要继续能读，送给 Provider 的 JSON 不能变。

# SPEC
1. [LEMMA: 1] 测试覆盖宽松解析、附件引用、往返；完整字节由 AgentContent 金样钉住。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/AgentContent/tests（逐条列在 AgentContent AGENTS.md）。
