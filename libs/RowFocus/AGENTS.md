# RowFocus

侧栏行焦点（迁自旧 `Views/Sidebar.fs` 的 `visibleRowIds`、`focusedRowId`、`focusedRowIndex`、`pendingFocusId`，方法 `NeighborIndexAfterDelete`、`NeighborAfterDelete`、`FocusAfterDelete`，行 GotFocus 的记录与 `Rebuild` 的焦点还原）。控件聚焦与 `GetVisibleOrder` 的字符串化归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **打开谁与焦点落谁同一个答案**（契约 C3）：下一行优先，其次上一行；只剩一行时没有邻位。
2. **删除意图只用一次**：重建后优先聚焦意图行；意图行不在了就作废，不落到别处。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- KeyConvergenceTests：`neighbor after delete matches the row focus falls to`、`no neighbor when the list becomes empty`（行序直接给出；原用例经真实侧栏排序）

# 与旧实现的差异
无。意图行不在了时，旧 Rebuild 仍会还原重建前正聚焦且仍可见的行，本库相同（第四个用例末段）。
