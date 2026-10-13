# Anthropic

Anthropic Messages API 协议：请求正文（`AnthropicRequest.build`）、端点和请求头（`AnthropicApi`）、流解码器（`AnthropicStream`）。依赖 Microsoft.Extensions.AI.Abstractions 的消息模型，不碰网络，不依赖兄弟库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **请求字节与旧实现相同。** [根 AXIOM: 1, 5]
    - 推理：请求格式由 Anthropic 规定，旧实现已经在用户的真实账户上跑过。提示词缓存默认关（写缓存更贵，是计费决定），max_tokens 会抬到思维链预算之上（否则请求直接 400），会话里配的额外参数原样透传到顶层。
2. **解码器是一个只吃事件的对象。** [根 AXIOM: 3, 4]
    - 推理：喂（事件名, data），得到增量、停止或失败；最后取收尾更新（用量、结束原因）。不碰网络，所以可以用录下来的事件回放测试。分帧（Sse）和 HTTP（HttpRetry）由组合者接上。
3. **思维链同时作为推理内容和带 "thinking" 标记的文本发出；工具入参拼不成 JSON 对象时丢弃这次调用。** [根 AXIOM: 5]
    - 推理：前者是旧行为，下游界面和已经落盘的消息依赖它。后者是因为用残缺参数执行工具，比不执行更糟。

# SPEC
1. [LEMMA: 1] 请求正文、端点、请求头与旧实现逐字节相同：组合者 ProviderTransports 的 `GoldenTests`，通过。
2. [LEMMA: 2, 3] 工具入参分片拼接、坏入参丢弃、按事件名兜底、error 和 stop、收尾用量与结束原因映射：`tests/Tests.fs`，通过。

# 变更记录
- 流事件结果联合类型加 `[<RequireQualifiedAccess>]`：它们与 AgentRuntime 的 `AgentCallResult`（Completed/Failed/Cancelled）同在命名空间 Wanxiang.Agent，作为包被引用时 `Failed` 会被解析错。调用方原本都已限定写法，无需改动。
