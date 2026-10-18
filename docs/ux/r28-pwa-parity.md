# 第 28 轮：桌面 / PWA 生命周期一致性审计

> 基准变更（2026-10-18）：下表是旧仓库 wanxiang（2026-10-17 退役，活文档迁入本仓库）的运行时实测记录，打点靠旧 `ParityProbe`（决策 450）。探针链已于 2026-10-18 退役删除（无接线、无消费者），本表按历史记录读；今天的行为证据是 `compose/AppShell/tests`、`tools/smoke_desktop.sh`、`tools/smoke_published.sh`。
> 今天的证据基础：`compose/AppShell/tests`（18 个 headless 用例，假端口 rig）、`tools/smoke_desktop.sh`（真实启动断言）、`tools/smoke_published.sh` + `tools/smoke_published_pwa.js`（发布产物 + headless Chrome 壳冒烟；先 `npm ci` 装 `puppeteer-core`，CI 的 `published-smoke` 任务与发布流程的 linux-x64 都跑它；2026-10-18 本机通过：画布渲染、DPR 2、文本上屏桥、**认证态下草稿落进 IndexedDB 且重开页面恢复**、无页面异常——最后一项由脚本预置客户端凭据把浏览器接进已认证状态后真跑）。表中浏览器运行时行为里，**落草稿一条已有运行时实测**；剪贴板、文件选择/保存、右键菜单、拖放、焦点在 body 时快捷键、localStorage 持久化仍只有编译与 JS 绑定契约级证据。

方法（旧仓库，2026-10 执行）：`ParityProbe`（决策 450，默认关闭）在每条依赖生命周期的路径上打点。PWA 用发布产物 + headless Chrome（查询参数开关，`tools/parity_pwa.js`）实测；桌面用发布产物 + Xvfb（`WANXIANG_PARITY_PROBE=1`）实测。

| 行为 | 桌面 | PWA（修复前） | PWA（修复后） | 说明 / 守护 |
|---|---|---|---|---|
| 视图挂载 → 启动计时器（投递确认、请求超时、草稿自动保存） | ✅ | ✅（第 27 轮已修，决策 449） | ✅ | 旧打点（parity_pwa）：`timer.delivery` `timer.draft` |
| 浏览器视口轮询（断点、恢复信号、页面链接） | 不适用 | ✅ | ✅ | `timer.viewport` |
| ChatView 挂载、动效订阅 | ✅ | ✅ | ✅ | `lifecycle.chatAttached` |
| 系统主题变化订阅 | ✅ | ✅ | ✅ | `theme.systemSubscribed` |
| 自动连接 | ✅ | ✅ | ✅ | `connect.auto` |
| 读本机草稿 | ✅（文件） | ✅（IndexedDB） | ✅ | `drafts.loaded` |
| 关闭 / 离开时落草稿 | ✅ Window.Closing | ❌ 无对应事件，只靠 800 ms 定时器（后台页被节流） | ✅ visibilitychange/pagehide | 决策 451，`drafts.flushOnHide` |
| 界面偏好持久化（主题、字号、侧栏、回车发送、草稿开关） | ✅ ui.json | ❌ 写进内存文件系统，刷新即丢 | ✅ localStorage | 决策 452；旧核对方式：parity_pwa 刷新后核对 |
| 命令面板「最近使用」 | ✅ | ❌ 同上 | ✅ | 决策 452 |
| 键盘快捷键（首屏、点空白处后） | ✅ | ❌ 焦点在 body，按键进不了应用 | ✅ | 决策 454，`shortcut.resolved` |
| 键盘快捷键（输入框有焦点） | ✅ | ✅ | ✅ | — |
| 网络恢复 / 回到前台 → 立即重连 | 不适用（桌面走退避） | ✅ | ✅ | `reconnect.resume` |
| 深链 | ✅ `wanxiang://`（决策 251） | ✅ `#open?…` | ✅ | `deeplink.open` |
| 输入框聚焦 | ✅ | ✅ | ✅ | `focus.composer`（脚本外实测） |
| 剪贴板复制 / 粘贴 | ✅ | ✅（第 22 轮实测） | ✅ | 已打点 `clipboard.*`，CI 未断言（需会话） |
| 文件选择 / 保存（附件、导入、导出） | ✅ | ✅（第 14/15 轮实测） | ✅ | 已打点 `file.*`，CI 未断言 |
| 拖放附件 | ✅ | 未实测（headless 无法合成系统拖放） | — | 已打点 `dragdrop.drop` |
| 消息右键菜单 | ✅ | ✅（第 8 轮修复） | ✅ | 已打点 `contextmenu.message`，CI 未断言 |
| 限流倒计时 / 搜索防抖 | ✅ | 控件构造时挂事件，挂载顺序无风险 | ✅ | 已打点 |
| 系统通知 | 无此功能 | 无此功能 | — | — |

今天代码落点（供对照，不由本表断言）：决策 449 的计时器在 `compose/AppShell/AppShell.fs` 装配段（`ports.Every`：投递确认、草稿 800 ms、视口轮询、更早一页看门狗）；决策 451 的页面隐藏钩子在 `app/Pwa/wwwroot/main.js`（`visibilitychange`/`pagehide` → `libs/BrowserPage.NotifyHidden`）；决策 452 的 PWA 持久化在入口本地模块 `LocalText`（`app/Pwa/Program.fs`，键 `wanxiang.local:<文件名>`，经 `libs/BrowserLocalStore`），桌面落 `ui.json`（`compose/UiPrefsFile`）；决策 454 的顶层按键钩子在 `app/Pwa/Program.fs`（`top.KeyDown`）与 `app/Desktop/Program.fs`（根视图 `AddHandler`），换算与分派在 `compose/AppShell/KeyShortcut.fs`。决策 290 的 `firstFrame` 桌面打在窗口 `Opened` 后第一帧，PWA 打在根视图挂上后的渲染优先级；`exit` 模式两边都由入口安装 `StartupTrace.requestExit`（`Environment.Exit 0`），时机仍在组合者的 `listReady` / `chatReady`。

结论（旧仓库，2026-10）：第 27 轮那类「挂载早于订阅」的问题只出现在 MainView（顶层），子控件都是先挂事件再加入可视树，已逐个核对。另外发现 3 个与挂载无关、但同样「桌面正常、PWA 静默失效」的缺口（离开时落草稿、偏好持久化、首屏键盘），均已修复；当时「进 CI 冒烟」指 ParityProbe 打点 + `tools/parity_pwa.js` 链路，该链路未随迁，在本仓库不成立——今天能自动跑的只有 `compose/AppShell/tests`（18 个 headless 用例）与 `tools/smoke_desktop.sh` 的启动断言。
