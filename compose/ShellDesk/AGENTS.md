# ShellDesk（组合者）

ShellLayout + BrandMark + NavigationState + Theme = 旧 `Views/MainLayout.fs`（ConversationWorkspace、MainLayoutController）在 AppShell 里的接线，以及 `Views/Brand.fs` 的品牌标。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ShellDesk(theme, shellGrid: Grid, sidebar, splitter, chat, composer, hooks: ShellHooks, initialSidebarCollapsed, sidebarWidth: unit -> float)`。`ShellHooks = { ApplyCompactMode; ApplyStartMode; FocusComposer; FocusSidebarSearch; PrefersTouch }`（侧栏、对话视图、输入区尚未迁出，经插槽驱动）。
- 方法：`ApplyViewport(width)`、`ToggleSidebar()`、`ApplyCompactNavigation(opened)`、`EnsureSearchVisible()`、`RenderStarting(bool)`、`NoteSidebarDragged() : float option`（桌面未折叠才写回，返回值存偏好）、`State`、`Workspace`、`Columns`。都返回或更新 NavigationSnapshot，宿主据此存偏好、做 Escape 链。
- 焦点归宿（D3/D4）在这里：窄屏抽屉打开进搜索框（ApplyCompactNavigation 与视口自动打开在触屏下不聚焦）、收起回输入区；桌面折叠时焦点原在侧栏回输入区；遮罩按下与 Escape 同一路径收起抽屉。
- 品牌标：`ShellBrand(theme)`，`Logo(size)`；找图顺序同旧代码（内嵌资源、程序目录、pwa、WANXIANG_HOME）。
- 废除 `ShellLook`，外观全走 Avalonia 样式类与主题资源；分隔条宽与窄屏断点取主题度量。用例 `改主题的布局度量后已显示的外壳跟着变`。

## 证据与旧测试
- FocusRingTests：`窄屏不自动打开抽屉，显式打开后焦点留在抽屉内`（导航与焦点请求部分；Tab 包含在侧栏）
- UiStabilityTests：`collapsing the desktop sidebar returns focus to the composer`
- AppShellTests：`ToggleSidebar toggles sidebarCollapsed and persists into prefs`（状态与列宽部分；写偏好在宿主）
- 新增：`按下抽屉遮罩收起抽屉并把焦点交回输入区`、`起步模式经插槽同步，拖分隔条只在桌面写回宽度`、`品牌标没有图时退成万字并随主题换色`

## 与旧实现的差异
- NavigationController 由本组合者拥有，宿主不再自己调 MainLayoutController.Apply。
- 宿主侧控件的 ApplyCompactMode / ApplyStartMode / Focus 改经 ShellHooks。
- 命名规约：投影走 `Render*`，单槽走 `Apply*`；无 `Set*` 成员。
- 遮罩淡入时长随减弱动效开关实时变化（旧代码构造时取一次）。
