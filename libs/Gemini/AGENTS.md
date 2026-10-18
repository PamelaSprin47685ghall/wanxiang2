# Gemini

Gemini generateContent 协议：请求正文（`GeminiRequest.build`）、调用 id 规则、端点和请求头（`GeminiApi`）、流解码器（`GeminiStream`）。依赖 Microsoft.Extensions.AI.Abstractions，不碰网络，不依赖兄弟库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **请求字节与旧实现相同；密钥放请求头，不放 URL。** [根 AXIOM: 1, 5]
    - 推理：URL 会进日志和错误详情，密钥不能出现在那里。
2. **Gemini 不给调用 id，用「名字@序号」造一个。** [根 AXIOM: 1]
    - 推理：回传工具结果时要从 id 取回函数名。这个格式记在已落盘的会话消息里，不能改。
3. **坏 data 抛 JsonException，流里的 error 对象报失败。** [根 AXIOM: 5]
    - 推理：坏流要报出来，不能静默吞掉；这与旧实现一致。

已修的旧缺陷（2026-10-18）：一个会话从别的提供方切到 Gemini 后，旧工具结果的 callId 不是「名字@序号」格式（如 `call1`），旧实现把 `functionResponse.name` 填成原始 callId——发出去的名字不是任何一个已声明的函数。现在先在同一份请求里找那次 `functionCall` 的名字（跨提供方历史也带着它），找不到才退回解析 id。金样 `compose/ProviderTransports/tests/golden/transports.txt` 的 Gemini 请求因此有一处偏离旧字节：`"name":"call1"` → `"name":"search"`。

# SPEC
1. [LEMMA: 1, 2] 请求正文、端点、请求头、解码后的增量与旧实现逐字节相同：组合者 ProviderTransports 的 `GoldenTests`，通过。
2. [LEMMA: 2, 3] 调用 id 往返、思考片段、调用按到达顺序编号、error 对象、坏 data、用量兜底：`tests/Tests.fs`，通过。
3. `tests/Tests.fs` 的 `functionResponse takes the name from the call in the same request when the id has none`：外来 id 取同一请求里的名字、自家 `名字@序号` 若两者一致、孤立结果退回解析 id。

# 变更记录
- 流事件结果联合类型加 `[<RequireQualifiedAccess>]`：它们与 AgentRuntime 的 `AgentCallResult`（Completed/Failed/Cancelled）同在命名空间 Wanxiang.Agent，作为包被引用时 `Failed` 会被解析错。调用方原本都已限定写法，无需改动。
