# TextSocket

按「代」管理的文本 WebSocket 客户端（从旧 `Client/WsClient.fs` 拆出传输部分）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`TextSocket()`；`ConnectAsync(uri, ct) : Task<int>`（返回代号）、`Disconnect()`、`TrySendAsync(text, expectedGeneration)`、`TextReceived : (int * string)`、`Closed : (int * exn option)`、`Generation`、`IsCurrent`、`IsConnected`。

# LEMMA
1. **对象自己拥有连接、代号和发送闸门。** [根 AXIOM: 3]
    - 推理：每次连接或断开都让代号加一；旧代的帧和关闭通知一律丢掉，免得断线重连后旧连接的事件混进新视图。
2. **只认文本，不懂内容。** [根 AXIOM: 3]
    - 推理：文本怎么解码（AG-UI、WireEvent）归组合者 compose/WsClient。
3. **行为与旧实现一致**：发送失败返回 false 不抛；二进制帧带 `binary frame received` 错误关闭；接收循环用 async（browser-wasm 单线程运行时）。[根 AXIOM: 5]

# SPEC
1. [LEMMA: 1, 3] 本机真实 WebSocket 服务上的测试通过：分片拼接与往返、代号不符拒发、二进制帧、对端干净关闭只通知一次、主动断开后旧代静默、连不上时抛原异常。
2. 端到端由 compose/WsClient 迁入的旧测试覆盖。

# 迁入或覆盖的旧测试
- 经 compose/WsClient 的 WsClient 外壳覆盖（见该组合者 AGENTS.md）：`WsClient live connection lifecycle, generation, and clean disconnect`、`WsClient connection superseded during connect raises OperationCanceledException`、`WsClient sendGate concurrency and serialization under parallel load`、`WsClient ReceiveLoop handles text frames, multi-chunk fragmentation, and ignores unknown or broken JSON`、`WsClient ReceiveLoop stale generation drops events from previous connection`、`WsClient ReceiveLoop handles binary frame and closes with error`、`WsClient ReceiveLoop handles clean close frame from server`、`WsClient ReceiveLoop handles abrupt socket abort`、`WsClient reconnect lifecycle across server restarts with incremented generation`、`WsClient binary frame receives error and closes connection cleanly`。
