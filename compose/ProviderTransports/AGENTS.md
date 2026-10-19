# ProviderTransports

组合者：Anthropic、Gemini 两家原生传输（`AnthropicChatClient`、`GeminiChatClient`，实现 `IChatClient`），以及旧仓库的 `ProviderSse` 入口。协作者：Sse、HttpRetry、JsonPick、Anthropic、Gemini、AppConfig（`ProviderConfig`）、Wire（`ProviderRateLimit`），全部按交付包引用。命名空间沿用 `Wanxiang.Agent`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **组合者负责协作：建请求 → 首字节前重试 → 非 2xx 抛错（带上限流信息）→ 分帧 → 交给解码器 → 收尾。** [根 AXIOM: 3]
    - 推理：两家共用的只有这段流程（`StreamPump`）；协议语义各在自己的库里。限流头属于线协议，只有组合者同时认识 HttpRetry 和 Wire。
2. **对外行为与旧实现逐字节相同。** [根 AXIOM: 1, 5]
    - 推理：请求发往用户自己的提供方账户；解码出的增量会变成落盘的会话消息。金样由旧仓库源码跑同一个场景生成（`tests/Scenario.fs`、`tests/golden/transports.txt`），不是由新代码生成。
    - 已知偏离（2026-10-18，1 行）：场景里的跨提供方历史用 id `call1`，旧实现给 Gemini 的 `functionResponse.name` 填了原始 id，是个缺陷（不是任何已声明函数）；`libs/Anthropic` 修成了「取同一请求里那次调用的名字」，金样的 Gemini 请求行随之从 `"name":"call1"` 改为 `"name":"search"`。其余字节仍与旧实现相同。
3. **`ProviderConfig.headers`（附加请求头）不在这里处理。** [根 AXIOM: 3]
    - 推理：旧实现里它由 `ProviderHttp` 设在共享的 HttpClient 上，传输只拿到一个现成的 HttpClient。这一点保持不变。

# SPEC
1. [LEMMA: 2] Anthropic 和 Gemini 的请求（方法、地址、头、正文）与解码后的增量（文本、思维链、工具调用、用量、结束原因、响应 id）与旧实现逐字节相同：`tests/GoldenTests.fs`，通过。
2. [LEMMA: 1, 2] 旧仓库 `AnthropicTransportTests.fs` 原样搬来；`NativeTransportTests.fs` 去掉属于 Server、UI、MessageSerde 的 7 个测试后搬来；连同金样共 39 个，通过。
