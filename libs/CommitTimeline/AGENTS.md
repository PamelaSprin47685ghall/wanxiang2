# CommitTimeline

合并包：CommitTimeline、TextSocket。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `CommitTimeline`

## CommitTimeline

客户端消息列表（JsonArray）按 commitId 的操作：查找、删除、把更早一页前置并去重（从旧 ClientState 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`CommitTimeline.commitIdOf`、`contains`、`without`、`prependOlder`。

## LEMMA
1. **commitId 是消息的唯一标识，重复推送、快照与 catch-up 交叠时都按它去重。** [根 AXIOM: 5]
2. **非对象项、没有 commitId 的项原样保留、不参与匹配。** [根 AXIOM: 5]
3. **唯一改动：列表里的 JSON null 项会原样保留，旧实现在这里抛 NullReferenceException。** [根 AXIOM: 2]

## SPEC
1. [LEMMA: 1, 2, 3] 本库测试通过（含非整数 commitId 抛出，与旧实现一致）：`tests/CommitTimeline.Tests.fs` 的 `commitIdOf 只认对象里的 commitId`、`contains 按 commitId 判断，跳过非对象项`、`without 删除匹配项，保留非对象项与顺序，不改原数组`、`prependOlder 去重、丢弃无 commitId 的项并报告最早一条` 通过。

## 迁入或覆盖的旧测试
- 经 compose/WsClient 覆盖：`ClientState HistoryPage prepends older messages and deduplicates`、`ClientState MessageCommitted appends message, updates watermark, deduplicates and raises event`、`ClientState ConversationUpdated updates title and removes deleted messages`、`ClientState HistoryPage handles non-JsonObject items and updates pagination`、`ClientState MessageCommitted handles messages collection with non-JsonObject items`、`ClientState ConversationUpdated deletedMessage preserves non-JsonObject items`、`ClientState AuthorityCatchUp decodes string commit lines and handles AgentMessageRecorded and MessageDeleted`。

## 成员 `TextSocket`

## TextSocket

按「代」管理的文本 WebSocket 客户端（从旧 `Client/WsClient.fs` 拆出传输部分）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`TextSocket()`；`ConnectAsync(uri, ct) : Task<int>`（返回代号）、`Disconnect()`、`TrySendAsync(text, expectedGeneration)`、`TextReceived : (int * string)`、`Closed : (int * exn option)`、`Generation`、`IsCurrent`、`IsConnected`。

## LEMMA
1. **对象自己拥有连接、代号和发送闸门。** [根 AXIOM: 3]
    - 推理：每次连接或断开都让代号加一；旧代的帧和关闭通知一律丢掉，免得断线重连后旧连接的事件混进新视图。
2. **只认文本，不懂内容。** [根 AXIOM: 3]
    - 推理：文本怎么解码（AG-UI、WireEvent）归组合者 compose/WsClient。
3. **行为与旧实现一致**：发送失败返回 false 不抛；二进制帧带 `binary frame received` 错误关闭；接收循环用 async（browser-wasm 单线程运行时）。[根 AXIOM: 5]

## SPEC
1. [LEMMA: 1, 3] 本机真实 WebSocket 服务上的测试通过：`tests/TextSocket.Tests.fs` 的 `未连接时发送返回 false，断开让代号加一`、`分片文本拼成一条消息，回显往返`、`代号不符的发送被拒绝`、`二进制帧带错误关闭` 通过。
2. 端到端由 compose/WsClient 迁入的旧测试（如 `WsClient live connection lifecycle, generation, and clean disconnect`）覆盖。

## 迁入或覆盖的旧测试
- 经 compose/WsClient 的 WsClient 外壳覆盖（见该组合者 AGENTS.md）：`WsClient live connection lifecycle, generation, and clean disconnect`、`WsClient connection superseded during connect raises OperationCanceledException`、`WsClient sendGate concurrency and serialization under parallel load`、`WsClient ReceiveLoop handles text frames, multi-chunk fragmentation, and ignores unknown or broken JSON`、`WsClient ReceiveLoop stale generation drops events from previous connection`、`WsClient ReceiveLoop handles binary frame and closes with error`、`WsClient ReceiveLoop handles clean close frame from server`、`WsClient ReceiveLoop handles abrupt socket abort`、`WsClient reconnect lifecycle across server restarts with incremented generation`、`WsClient binary frame receives error and closes connection cleanly`。
