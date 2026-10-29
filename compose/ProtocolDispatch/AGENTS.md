# ProtocolDispatch（组合者）

协议事件分派，取代旧 extract-ledger `src/Wanxiang.UI/AppShell.fs` 的 `HandleEvent`。协作者：Wire、WsClient（ClientState）、ConversationRuns、Outbox、CommandFeedback、ConfigFeedback、DownloadBuffers、HostRequests（SearchInFlight、PendingProbes）、ServerErrorRoute、UploadFacts、MessageView。只依赖 BCL 与这些包，不依赖 Avalonia。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ProtocolDispatch(parts: ProtocolParts, surface: ProtocolSurface)`，`Handle(ev: WireEvent)`。宿主把 WsClient.EventReceived 接到 Handle。
- `ProtocolParts = { State; Runs; Outbox; Commands; Configs; Downloads; Search; Probes }`：实例归宿主（别处也用），本组合者按事件给它们发消息、按旧顺序串起结果。
- `ProtocolSurface`：`ActiveConversation`、`InstanceId`、`Send`、`Render`、`Toast (text, DispatchTone)`、`ObserveList`、`DispatchPending`、`Downloaded`、`MissingAttachment`、`Apply`。都是事实通知；`Apply ev` 在模型部分做完后把事件交给仍在宿主的界面面。
- 留在宿主（经 Apply）：认证成功的整段步骤（换实例、重置、能力声明、重新请求目录与观察，按先前决定）、认证被拒、配对、目录快照、配置健康与设备、诊断、审批（ApprovalDesk.Handle）、探测结果落到首次运行卡或设置页、列表快照的侧栏与草稿清理、历史页、已接受的搜索结果、导出、能力表、附件上传完成/中止、生成完成后的浏览器事务。
- 本组合者做：ConfigChanged 重新请求目录；ProviderProbeResult 先销账；ConfigApplied 回执与提示、成功才重新拉目录；列表/对话快照、提交、更新、追赶、历史页先进 ClientState 并推进游标；对话快照后按实例放行 PreparingConversation 的发件箱消息；生成开始/增量/结束/部分恢复与限流倒计时；下载拼装与超限提示；命令接受/提交/拒绝；server.error 三路落点。
- 线上字节不变：发出的只有 `CatalogRequest`，经宿主 Send（WsClient 编码）。

## 与旧实现的差异
- ConversationListSnapshot：游标推进在侧栏更新之前（旧代码在之后）；推进只发出游标事件，与侧栏无关，外发顺序不变。
- ConversationSnapshot：旧代码在当前对话时重画两次（发件箱放行前后），这里宿主 Apply 里一次、放行后一次，次数相同。
- 提示语气用 `DispatchTone`（RequireQualifiedAccess），宿主映射到 ToastTone。

## 证据与旧测试（`tests/Tests.fs`，真实参与者 + 记录界面面）
- AppShellTests：`HandleEvent CommandCommitted resolves feedback and commits outbox`、`HandleEvent CommandRejected marks outbox as rejected and informs feedback`、`HandleEvent GenerationStarted, GenerationDelta, and GenerationFinished flow`、`HandleEvent GenerationFinished with error preserves error in runs`
- 留在宿主：`HandleEvent AuthAccepted …`、`HandleEvent AuthRejected …`、`HandleEvent CatalogSnapshot …`
- 新增：`Hello 与审批能力回声不改变任何状态，也不交给宿主`、`认证成功整段交给宿主，组合者不碰模型`、`配置回执：登记过的空文案不叠提示但仍重新拉目录，失败只报错误`、`迟到的旧搜索结果丢掉`、`主机错误：附件缺失标记，记账失败进当前对话错误卡，其余提示`、`下载超限提示一次，拼好后交给宿主保存`、`对话快照到达后发出等着它的发件箱消息`
