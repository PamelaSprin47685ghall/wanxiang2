# Workspace（组合者）

ShellDesk + SidebarDesk + ChatScreen + ComposerDesk + OverlayDesk = 旧 `AppShell.Build` 的布局段（分隔条、ShellHooks、工作区插进浮层根）与 `ApplyResponsiveLayout`。根规则见项目根目录 AGENTS.md。

- 宿主入口：`Workspace(theme, overlayRoot: Grid, sidebar, chat, composer, hooks: WorkspaceHooks)`；`WorkspaceHooks = { PrefersTouch; SidebarWidth; InitialSidebarCollapsed }`。构造时把工作区插到浮层根最底层。
- 紧凑模式同步给输入区、侧栏、对话屏；起步模式同步给对话屏；焦点归宿走 ComposerDesk.Focus 与 SidebarDesk.FocusSearch。
- 响应式：默认按自身宽度投影；浏览器宿主用 `ObserveViewport(cssWidth)` 喂 CSS 视口宽（轮询计时器留宿主），此后以它为准。`ApplyResponsiveLayout()` 立即重投影。
- 方法：`ToggleSidebar()`、`SetCompactNavigation(opened)`、`EnsureSearchVisible()`、`SetStarting(bool)`、`State`、`Shell`、`Splitter`；事件 `SidebarResized(width)`（拖完分隔条，宿主写偏好）。

## 证据与旧测试
- 无头行为用例两条：宽窗桌面 / CSS 视口进紧凑并放大顶栏触控目标、顶栏侧栏按钮在桌面折叠与紧凑抽屉开合。
- 导航与焦点归宿的旧用例在 ShellDesk。

## 与旧实现的差异
- 认证成功后的步骤序列、浏览器视口轮询与恢复检测仍在宿主。
