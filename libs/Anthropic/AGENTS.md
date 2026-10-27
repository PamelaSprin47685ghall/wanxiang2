# Anthropic

合并包：Anthropic、Gemini。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `Anthropic`

## Anthropic

Anthropic Messages API 协议：请求正文（`AnthropicRequest.build`）、端点和请求头（`AnthropicApi`）、流解码器（`AnthropicStream`）。依赖 Microsoft.Extensions.AI.Abstractions 的消息模型，不碰网络，不依赖兄弟库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **请求字节与旧实现相同。** [根 AXIOM: 1, 5]
    - 推理：请求格式由 Anthropic 规定，旧实现已经在用户的真实账户上跑过。提示词缓存默认关（写缓存更贵，是计费决定），max_tokens 会抬到思维链预算之上（否则请求直接 400），会话里配的额外参数原样透传到顶层。
2. **解码器是一个只吃事件的对象。** [根 AXIOM: 3, 4]
    - 推理：喂（事件名, data），得到增量、停止或失败；最后取收尾更新（用量、结束原因）。不碰网络，所以可以用录下来的事件回放测试。分帧（Sse）和 HTTP（HttpRetry）由组合者接上。
3. **思维链同时作为推理内容和带 "thinking" 标记的文本发出；工具入参拼不成 JSON 对象时丢弃这次调用。** [根 AXIOM: 5]
    - 推理：前者是旧行为，下游界面和已经落盘的消息依赖它。后者是因为用残缺参数执行工具，比不执行更糟。

## SPEC
1. [LEMMA: 1] 请求正文、端点、请求头与旧实现逐字节相同：组合者 ProviderTransports 的 `GoldenTests`，通过。
2. [LEMMA: 2, 3] 工具入参分片拼接、坏入参丢弃、按事件名兜底、error 和 stop、收尾用量与结束原因映射：`tests/Anthropic.Tests.fs` 的 `endpoint accepts base urls with or without v1, headers follow key and thinking`、`tool input split across deltas becomes one call when its block stops`、`broken tool input drops the call instead of running the tool with partial arguments` ，通过。

## 变更记录
- 流事件结果联合类型加 `[<RequireQualifiedAccess>]`：它们与 AgentRuntime 的 `AgentCallResult`（Completed/Failed/Cancelled）同在命名空间 Wanxiang.Agent，作为包被引用时 `Failed` 会被解析错。调用方原本都已限定写法，无需改动。

## 成员 `Gemini`

## Gemini

Gemini generateContent 协议：请求正文（`GeminiRequest.build`）、调用 id 规则、端点和请求头（`GeminiApi`）、流解码器（`GeminiStream`）。依赖 Microsoft.Extensions.AI.Abstractions，不碰网络，不依赖兄弟库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **请求字节与旧实现相同；密钥放请求头，不放 URL。** [根 AXIOM: 1, 5]
    - 推理：URL 会进日志和错误详情，密钥不能出现在那里。
2. **Gemini 不给调用 id，用「名字@序号」造一个。** [根 AXIOM: 1]
    - 推理：回传工具结果时要从 id 取回函数名。这个格式记在已落盘的会话消息里，不能改。
3. **坏 data 抛 JsonException，流里的 error 对象报失败。** [根 AXIOM: 5]
    - 推理：坏流要报出来，不能静默吞掉；这与旧实现一致。

已修的旧缺陷（2026-10-18）：一个会话从别的提供方切到 Gemini 后，旧工具结果的 callId 不是「名字@序号」格式（如 `call1`），旧实现把 `functionResponse.name` 填成原始 callId——发出去的名字不是任何一个已声明的函数。现在先在同一份请求里找那次 `functionCall` 的名字（跨提供方历史也带着它），找不到才退回解析 id。金样 `compose/ProviderTransports/tests/golden/transports.txt` 的 Gemini 请求因此有一处偏离旧字节：`"name":"call1"` → `"name":"search"`。

## SPEC
1. [LEMMA: 1, 2] 请求正文、端点、请求头、解码后的增量与旧实现逐字节相同：组合者 ProviderTransports 的 `GoldenTests`，通过。
2. [LEMMA: 2, 3] 调用 id 往返、思考片段、调用按到达顺序编号、error 对象、坏 data、用量兜底：`tests/Gemini.Tests.fs` 的 `endpoint adds v1beta only when no version is given; key goes in a header`、`call ids are made from name and index and give the name back`、`functionResponse takes the name from the call in the same request when the id has none` ，通过。
3. `tests/Gemini.Tests.fs` 的 `functionResponse takes the name from the call in the same request when the id has none`：外来 id 取同一请求里的名字、自家 `名字@序号` 若两者一致、孤立结果退回解析 id。

## 变更记录
- 流事件结果联合类型加 `[<RequireQualifiedAccess>]`：它们与 AgentRuntime 的 `AgentCallResult`（Completed/Failed/Cancelled）同在命名空间 Wanxiang.Agent，作为包被引用时 `Failed` 会被解析错。调用方原本都已限定写法，无需改动。
