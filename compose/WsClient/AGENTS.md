# WsClient（组合）

旧 `Wanxiang.Client.WsClient`、`ClientState`、`ConversationView`。命名空间和公开面与旧代码相同。根规则见项目根目录 AGENTS.md。

协作者（只以包引用）：TextSocket（按代文本传输）、CommitTimeline（按 commitId 的消息列表操作）、AguiWire（AG-UI 编解码）、ChatDomain（提交行与领域事件）。

# LEMMA
1. **WsClient 是协作：把 TextSocket 的 (代号, 文本) 用 WireAgui 宽容解码成 WireEvent，再按代号转发。** [根 AXIOM: 3]
    - 推理：解码后再判断是否仍是当前代，与旧实现的时机相同。
2. **ClientState 是协作：它同时要读线协议事件、提交行和领域事件，按 commitId 去重交给 CommitTimeline。** [根 AXIOM: 3]
3. **行为与旧实现一致。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1, 2, 3] 旧 ClientStateAndWsClientTests 原文迁入（`tests/ClientStateAndWsClientTests.fs` 的 `WsClient initial state is disconnected with generation zero`、`WsClient live connection lifecycle, generation, and clean disconnect` 等，去掉移到 libs/ChangeWatch 的 6 个），含本机真实 WebSocket 服务上的连接、代号、并发发送、分片、二进制帧、断线重连用例，通过。

# 迁入或覆盖的旧测试
旧 ClientStateAndWsClientTests（tests/ClientStateAndWsClientTests.fs）：
- `WsClient initial state is disconnected with generation zero`
- `WsClient TrySend when disconnected safely returns false`
- `WsClient TrySendAtGeneration with mismatched generation safely returns false`
- `WsClient TrySendCommand when disconnected safely returns false`
- `WsClient TrySendCommandAtGeneration when disconnected safely returns false`
- `WsClient Disconnect on fresh client increments generation and does not throw`
- `WsClient Connect with canceled token immediately throws or fails safely and increments generation`
- `WsClient ConnectWithGenerationAsync increments generation on each attempt`
- `WsClient SendAsync and SendCommandAsync fire-and-forget when disconnected safely complete`
- `WsClient event streams publish events as subscribers attach`
- `WsClient TrySendTextAsync respects expectedGeneration parameter`
- `WsClient detachConnection cancels and disposes previous cts and increments generation`
- `WsClient ReceiveLoop decodes text, triggers events and handles exceptions safely`
- `ClientState initial state has empty conversations and zero cursor`
- `ClientState ConversationListSnapshot updates conversationList and latestCommitId`
- `ClientState ConversationSnapshot populates conversation and raises event`
- `ClientState HistoryPage prepends older messages and deduplicates`
- `ClientState MessageCommitted appends message, updates watermark, deduplicates and raises event`
- `ClientState ConversationUpdated updates title and removes deleted messages`
- `ClientState AuthorityCatchUp replays deltas, message.committed, WireCodec and commit formats`
- `ClientState GenerationStarted and GenerationFinished update runtimeState`
- `ClientState CursorAdvanced, AdvanceCursor, and AdvanceCursorTo`
- `WsClient live connection lifecycle, generation, and clean disconnect`
- `WsClient ConnectAsync convenience method connects cleanly`
- `WsClient connection superseded during connect raises OperationCanceledException`
- `WsClient live send with generation check, command and wire event roundtrip`
- `WsClient sendGate concurrency and serialization under parallel load`
- `WsClient ReceiveLoop handles text frames, multi-chunk fragmentation, and ignores unknown or broken JSON`
- `WsClient ReceiveLoop stale generation drops events from previous connection`
- `WsClient ReceiveLoop handles binary frame and closes with error`
- `WsClient ReceiveLoop handles clean close frame from server`
- `WsClient ReceiveLoop handles abrupt socket abort`
- `WsClient TrySend when disconnected or canceled safely returns false`
- `WsClient reconnect lifecycle across server restarts with incremented generation`
- `WsClient binary frame receives error and closes connection cleanly`
- `WsClient TrySendAsync resilience against disconnected socket and cancelled token`
- `ClientState HistoryPage handles non-JsonObject items and updates pagination`
- `ClientState MessageCommitted handles messages collection with non-JsonObject items`
- `ClientState ConversationUpdated deletedMessage preserves non-JsonObject items`
- `ClientState AuthorityCatchUp processes message.committed and chat.message.committed wire formats`
- `ClientState AuthorityCatchUp decodes string commit lines and handles AgentMessageRecorded and MessageDeleted`

留在旧仓库（依赖 UI 或主机）：DeliveryPolishingTests、ConversationExportTests、InteractionReliabilityTests、R27HostCapabilitiesTests、ServerE2eTests 里用到 WsClient/ClientState 的用例。

迁入时改写的两条（旧版用反射调私有成员，只钉实现形状，不钉行为）：`WsClient TrySendTextAsync respects expectedGeneration parameter` 改为经公开的按代发送检查；`WsClient ReceiveLoop decodes text, triggers events and handles exceptions safely` 改为检查未连接时不发事件，接收循环本身由 libs/CommitTimeline 测试。
