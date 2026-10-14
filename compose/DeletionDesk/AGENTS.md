# DeletionDesk（组合者）

删除即撤销（§214），取代旧 extract-ledger `src/Wanxiang.UI/AppShell.fs` 的 `NeighborExcluding`、`ScheduleDelete`、`ArmDeleteTimer`、`CommitDelete`。协作者：PendingDeletion（批次、到期、邻位、恢复文案）、CommandFeedback（按命令登记，经 Outbox 的扩展）、Wire。不依赖 Avalonia。根规则见项目根目录 AGENTS.md。

- 宿主入口：`DeletionDesk(commands: CommandFeedbackTracker, surface: DeletionSurface)`；`Schedule(ids)`（侧栏单删与批量删都走它）、`Commit(token)`、`Pending`、`IsPending(id)`。
- `DeletionSurface`：`Now`、`ActiveConversation`、`TitleOf`、`VisibleOrder`、`PendingChanged`（侧栏按待删集合隐去 / 放回）、`FocusAfterDelete`、`Open`（宿主的 SelectConversation + 侧栏标当前）、`Render`、`Toast (text, DeletionTone)`、`ToastUndo (label, 撤销动作)`、`After`（一次性计时，计时器归宿主）、`SendCommand`、`NewInvocation`。都是事实，没有步骤脚本。
- 顺序与旧代码一致：单条先移焦点 → 登记 → 隐去 → 当前对话落邻位 → 盯计时 → 撤销提示条 → 重画；撤销：取回 → 重盯计时 → 放回 → 单条且是当前就打开它 → 重画 → 「已恢复」提示。

## 证据与旧测试（`tests/Tests.fs`，真实 PendingDeletion + CommandFeedbackTracker，手动时钟）
- InteractionReliabilityTests：`batch delete reports what it did instead of staying silent`、`§214 撤销把批量删除的行放回列表且不提交`（旧用例里反射调 CommitDelete，这里走公开 Commit）
- 新增：`删掉当前对话：焦点落邻行、打开邻位（规则归 PendingDeletion.neighborAfter）；撤销单条再打开它`、`宽限期到才提交；只盯最早一批，提交后盯下一批`、`主机拒绝删除给警告，对话在下一次列表快照里回来`、`同一对话重复删除不重复登记`
- 批次规则本身的旧用例（LibraryUndoTests、AppShellTests NeighborExcluding）在 libs/PendingDeletion。
