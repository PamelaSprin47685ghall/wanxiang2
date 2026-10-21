# PendingDeletion

删除即撤销的待删批次（迁自旧 `Model/PendingDeletion.fs`，`visible` 在 compose/ConversationList）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **宁少删不误删。** [根 AXIOM: 5] 撤销后的批次到期不交出任何 id；同一 id 不重复登记。

2. **到期只交出一次。** `scheduleAt` 记下到期时刻（now + 8 秒宽限期），`tick now` 按登记顺序交出已到期批次的 token 并清掉其到期时刻；撤销同时清掉到期时刻。计时器仍归宿主，宿主可用 `nextDue` 安排下一次计时。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。
2. `neighborAfter`、`restoredText` 迁自旧 AppShell（NeighborExcluding、撤销提示）；`tests/Tests.fs` 覆盖：`NeighborExcluding returns next visible neighbor when active conversation is removed`、`到期跟踪：宽限期到才交出 token，只交一次；撤销的批次不再到期`。
3. [LEMMA: 2] `到期跟踪` 用例通过（对应旧 AppShell.ScheduleDelete 的每批 DispatcherTimer 与 CommitDelete）。

# 迁入或覆盖的旧测试
- LibraryUndoTests：`§214 待删批次：隐去、撤销恢复、撤销后不再提交`、`§214 到期提交只交出一次；重复登记不重复删`
- AppShellTests：`NeighborExcluding returns next visible neighbor when active conversation is removed`（侧栏可见顺序直接给出）
