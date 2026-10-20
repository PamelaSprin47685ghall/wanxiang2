# NavigationState

主壳导航的纯状态（窄屏抽屉、侧栏折叠），迁自旧 `Model/UiControllers.fs`。窄屏断点属设计层（LayoutPolicy.compactBreakpoint），由宿主经构造参数传入。旧 `ApplyViewport` 的 `hasConversation` 参数从未使用，删除。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **变窄时先看到输入区，只有显式导航动作才打开抽屉。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 接口
- `NavigationController(initialSidebarCollapsed, compactBreakpoint)`：成员 `State`、`ApplyViewport(width) : bool * NavigationSnapshot`、`ToggleSidebar()`、`ApplyCompactNavigation(opened) : NavigationSnapshot`（抽屉单槽投影，返回快照）、`EnsureSearchVisible()`。
- `NavigationSnapshot.drawerJustOpened`、`NavigationSnapshot.autofocusDrawer`。
- 命名规约：单槽写走 `Apply*`；无 `Set*` 成员。

# 迁入或覆盖的旧测试
- UiStabilityTests：`navigation controller owns compact and collapsed state transitions`
- NavigationSnapshot.drawerJustOpened / autofocusDrawer 迁自旧 AppShell.ApplyResponsiveLayout（测试内与旧代码原样摘录逐例比对）。
