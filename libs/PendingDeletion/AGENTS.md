# PendingDeletion

删除即撤销的待删批次（迁自旧 `Model/PendingDeletion.fs`，`visible` 在 compose/ConversationList）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **宁少删不误删。** [根 AXIOM: 5] 撤销后的批次到期不交出任何 id；同一 id 不重复登记。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。
2. `neighborAfter`、`restoredText` 迁自旧 AppShell（NeighborExcluding、撤销提示）；`tests/golden/expected.txt` 由 `tools/OldPendingDeletion.fs`（原样摘录）经 `tools/gen-golden.fsx` 跑 `tests/Render.fs` 生成，重跑逐字节不变。

# 迁入或覆盖的旧测试
- LibraryUndoTests：`§214 待删批次：隐去、撤销恢复、撤销后不再提交`、`§214 到期提交只交出一次；重复登记不重复删`
- AppShellTests：`NeighborExcluding returns next visible neighbor when active conversation is removed`（侧栏可见顺序直接给出）
