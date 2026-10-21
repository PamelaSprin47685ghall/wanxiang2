# Workspace（组合者）

ShellDesk + SidebarDesk + ChatScreen + ComposerDesk + OverlayDesk = 旧 `AppShell.Build` 的布局段（分隔条、ShellHooks、工作区插进浮层根）与 `ApplyResponsiveLayout`。根规则见项目根目录 AGENTS.md。

- 宿主入口：`Workspace(theme, overlayRoot: Grid, sidebar, chat, composer, hooks: WorkspaceHooks)`；`WorkspaceHooks = { PrefersTouch; SidebarWidth; InitialSidebarCollapsed }`。构造时把工作区插到浮层根最底层。
- 紧凑模式同步给输入区、侧栏、对话屏；起步模式同步给对话屏；焦点归宿走 ComposerDesk.Focus 与 SidebarDesk.FocusSearch。
- 响应式：默认按自身宽度投影；浏览器宿主用 `ObserveViewport(cssWidth)` 喂 CSS 视口宽（轮询计时器留宿主），此后以它为准。`ApplyResponsiveLayout()` 立即重投影。
- 方法：`ToggleSidebar()`、`EnsureSearchVisible()`、`Render(vs: ShellVS)`（单入口幂等投影，涵盖抽屉开合与起步态）、`State`、`Shell`、`Splitter`；事件 `SidebarResized(width)`（拖完分隔条，宿主写偏好）。
- 命名规约：单入口走 `Render*`，单槽走 `Apply*`；无 `Set*` 成员。

## 证据与旧测试
- 无头行为用例两条：宽窗桌面 / CSS 视口进紧凑并放大顶栏触控目标、顶栏侧栏按钮在桌面折叠与紧凑抽屉开合。
- 导航与焦点归宿的旧用例在 ShellDesk。

## 与旧实现的差异
- 认证成功后的步骤序列、浏览器视口轮询与恢复检测仍在宿主。

## 整树金样（已退役）

旧 AppShell.Build 布局段（旧 Sidebar / ChatView / Composer、审批面板进输入区附属位、ShellDesk）的整树金样复刻随纯界面复刻退役一并删除：7 个状态（宽屏 1280、折叠侧栏、起步页居中、审批卡、窄屏 600、抽屉打开、窄屏审批卡）的对照文件与生成器都不在仓库里了。现在这一段的事实由无头行为用例承担（见上面「证据与旧测试」）。

- 已知且不在本组合者修的差异：SidebarDesk.View 根是普通 Border，旧 Sidebar 是 Border 派生类型（写手二的库，已报）；新 ComposerDesk 构造后立刻落禁用外观，旧 Composer 要等第一次 SetEnabled（宿主总会调）。
- 有意偏离（2026-10-18）：输入壳的静止/禁用边界描边从旧树的 `Hairline`（浅 `#ECE9DE`）改为 `ControlStroke`（浅 `#8D8A7E`，≥3:1，见 `docs/ux/a11y-audit.md` §2）；与 `compose/ComposerDesk` 同一处改动。旧仓库已退役，这份对照无法再重新生成。
