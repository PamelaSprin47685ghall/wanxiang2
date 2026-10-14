# ShellDesk（组合者）

ShellLayout + BrandMark + NavigationState + Theme + DesignRhythm = 旧 `Views/MainLayout.fs`（ConversationWorkspace、MainLayoutController）在 AppShell 里的接线，以及 `Views/Brand.fs` 的品牌标。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ShellDesk(theme, shellGrid: Grid, sidebar, splitter, chat, composer, hooks: ShellHooks, initialSidebarCollapsed, sidebarWidth: unit -> float)`。`ShellHooks = { SetCompactMode; SetStartMode; FocusComposer; FocusSidebarSearch; PrefersTouch }`（侧栏、对话视图、输入区尚未迁出，经插槽驱动）。
- 方法：`ApplyViewport(width)`、`ToggleSidebar()`、`SetCompactNavigation(opened)`、`EnsureSearchVisible()`、`SetStarting(bool)`、`NoteSidebarDragged() : float option`（桌面未折叠才写回，返回值存偏好）、`State`、`Workspace`、`Columns`。都返回或更新 NavigationSnapshot，宿主据此存偏好、做 Escape 链。
- 焦点归宿（D3/D4）在这里：窄屏抽屉打开进搜索框（SetCompactNavigation 与视口自动打开在触屏下不聚焦）、收起回输入区；桌面折叠时焦点原在侧栏回输入区；遮罩按下与 Escape 同一路径收起抽屉。
- 品牌标：`ShellBrand(theme)`，`Logo(size)`；找图顺序同旧代码（内嵌资源、程序目录、pwa、WANXIANG_HOME）。
- `ShellLook.layoutOf / brandOf`：遮罩画笔与淡入（`Timing.ScrimFade` 经减弱动效门控、随开关变化）、侧栏宽度、对话区最小高取主题；分隔条宽、起步最小高、断点取 LayoutPolicy。

## 证据与旧测试
- FocusRingTests：`窄屏不自动打开抽屉，显式打开后焦点留在抽屉内`（导航与焦点请求部分；Tab 包含在侧栏）
- UiStabilityTests：`collapsing the desktop sidebar returns focus to the composer`
- AppShellTests：`ToggleSidebar toggles sidebarCollapsed and persists into prefs`（状态与列宽部分；写偏好在宿主）
- 新增：`按下抽屉遮罩收起抽屉并把焦点交回输入区`、`起步模式经插槽同步，拖分隔条只在桌面写回宽度`、`品牌标没有图时退成万字并随主题换色`

## 与旧实现的差异
- NavigationController 由本组合者拥有，宿主不再自己调 MainLayoutController.Apply。
- 宿主侧控件的 SetCompactMode / SetStartMode / Focus 改经 ShellHooks。
- 遮罩淡入时长随减弱动效开关实时变化（旧代码构造时取一次）。
