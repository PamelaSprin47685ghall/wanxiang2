# AgentRuntime（组合）

旧 `AgentRuntime` 与 `AgentCallResult`。协作者：ProviderTransports、ProviderFailure、SharedHttpClients、AgentHistory、AppConfig、Wire、Microsoft.Agents.AI 与 OpenAI SDK。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **发给 Provider 的请求体、流式增量、用量、错误、限流信号、历史回调与旧实现一致。** [根 AXIOM: 5]
2. **连接池抽成 SharedHttpClients。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `golden/runtime.txt` 由旧 AgentRuntime.fs、HistoryProvider.fs 对着本机假 OpenAI 兼容服务跑同一 Scenario 生成（流式成功含工具调用、429、500、补全、上下文过长、取消，以及异常归类矩阵），通过。
2. 观察（未改，保持旧行为）：对话 `extraJson` 进入 ChatOptions.AdditionalProperties，但 OpenAI SDK 不把它写进请求体（金样里看不到 `reasoning_effort`）。是否修复留给用户决定。

# 迁入或覆盖的旧测试（旧仓库 d9651ed，可在旧仓库删除）
- AgentCoverageTests.fs（ProviderFailure、WanxiangHistoryProvider、AgentRuntime 各节）：
  - `ProviderFailure retryAfterOfResponse parses delta, date, and string headers`
  - `ProviderFailure classify HttpRequestException status codes and network errors`
  - `ProviderFailure classify SocketException and generic exn patterns`
  - `WanxiangHistoryProvider ConversationIdOf extracts valid GUID and handles invalid`
  - `ProviderFailure classify classifies ClientResultException across statuses`
  - `ProviderFailure classify recognizes body signals in ClientResultException`
  - `ProviderFailure classify classifies timeouts and cancellations`
  - `ProviderFailure classify classifies HttpRequestException with retryAfter and statuses`
  - `ProviderFailure classify classifies SocketException and network keywords`
  - `ProviderFailure classify recognizes message body signals`
  - `AgentRuntime properties and session creation initializes conversation id`
  - `AgentRuntime CompleteOnce handles cancellation as ProviderTimeout`
  - `AgentRuntime RunStreaming handles cancellation`
  - `AgentRuntime Run handles cancellation`
- 未迁（属于别处）：AgentCoverageTests.fs 的两条 GeminiRequest 测试归 Gemini/ProviderTransports；ProductizationTests.fs「生成失败分类」三条用到 GenerationError 提示与线格式，ProviderFailure 部分（`timeouts classify as retryable provider timeouts`、`context-length signals are detected from the message body`）由本金样的 timeout 与 http400ctx 行覆盖，另两条属 Wire。
