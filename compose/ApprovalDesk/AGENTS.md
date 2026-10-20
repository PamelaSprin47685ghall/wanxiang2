# ApprovalDesk（组合者）

ApprovalPanel + ApprovalCardText + Wire + Theme + IconSet + OverlayDesk = 旧 `Views/ToolApprovalCard.fs` 的 ToolApprovalPanel 在 AppShell 里的接线（构造、ToolApprovalRequested / Resolved 分支、断线清空）。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ApprovalDesk(theme: ThemeManager, overlay: OverlayDesk, send: WireEvent -> unit)`；`View`（放进输入框附属位）、`ApplyConversation(Guid option)`、`Handle(WireEvent) : bool`（是审批事件就处理）、`Disconnected()`、`AwaitingCallIds`、事件 `AwaitingChanged`（宿主据此给消息里的工具卡标「等待批准」，旧 SyncAwaitingApproval）。
- 协作都在这里：主机事件 → ApprovalAsk；批准 / 拒绝 → `ToolApprovalDecision`；别的对话来审批、已处理过用 Info 提示条告知。
- 废除 `ApprovalLook`，改由主题样式系统及原生 Button 提供外观；提供单入口 `Render(ApprovalVS, currentConversation)`。

## 证据与旧测试
- W3ToolApprovalUiTests：`审批面板：只显示当前对话的卡片，已处理即移除，断线清空`（改由 WireEvent 驱动）
- 新增：`别的对话来审批给提示条，已处理过的结果也给提示条，待批准集合随之同步`（旧 AppShell 分支）、`审批卡的字色取自主题并随主题切换`。

## 与旧实现的差异
- 宿主不再自己拼 ApprovalRequestView、调 toast 与 SyncAwaitingApproval，改为 `Handle` + `AwaitingChanged`。
