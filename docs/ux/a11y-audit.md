# 无障碍与 UX 一致性审计

> 基准变更（2026-10-18）：本文写于旧仓库 wanxiang（2026-10-17 退役，活文档迁入本仓库），正文里的 `src/Wanxiang.UI`、`src/Wanxiang.Pwa/wwwroot`、`tests/Wanxiang.Tests/…` 与旧测试名（`DesignConvergenceTests`、`UiStabilityTests`、`FocusRingTests`、`DismissFocusTests`）都是旧基准，未随迁。今天 UI 拆在 `compose/*` 与 `libs/*`，PWA 壳在 `app/Pwa/wwwroot`，机械回归是 `compose/Theme/tests/A11yRegression.fs`（非 UI，随 `./x compose` 跑；CI 见 `.github/workflows/ci.yml` 的 `compose` job，`run-tests.sh` 与 pr-gate 不存在）。

本文件是旧仓库 `ux/redesign-4-a11y-tests` 的交付：对全应用的无障碍、键盘导航、焦点顺序、对比度与文案一致性做一次**静态审计**，并把能机械判定的规则固化为测试（当时 `tests/Wanxiang.Tests/A11yRegressionTests.fs`，今天 `compose/Theme/tests/A11yRegression.fs`）与 CI 步骤。
本轮不改任何具体屏幕的布局或交互，只动共享令牌；屏幕级问题记在 §7，留给各屏幕分支或后续处理。

## 1. 方法与边界

- 依据：读旧仓库的 `src/Wanxiang.UI` 与 `src/Wanxiang.Pwa/wwwroot` 源码（对应今天的 `libs/*` + `compose/*` 与 `app/Pwa/wwwroot`），按调色板数值计算 WCAG 2.x 对比度。
- 未做：真机屏幕阅读器（NVDA / VoiceOver / TalkBack）实测、PWA 的 Lighthouse 跑分、真实 Tab 轨迹录像。以下凡写“待实测”的条目都属此类。
- 判据：文字 ≥ 4.5:1（WCAG 1.4.3）；焦点环等非文本指示器 ≥ 3:1（1.4.11）；所有可交互控件有可访问名称（4.1.2）；键盘可达且焦点可见（2.1.1 / 2.4.7）。

## 2. 对比度

沿用旧仓库既有测试（`DesignConvergenceTests`、`UiStabilityTests`，未随迁）覆盖：text / textMuted / textFaint / accent / 状态色 在五种基础表面上，以及 textOnAccent on accent。本轮补上以下此前未验的组合：

| 组合 | 结果 |
| --- | --- |
| 代码着色与 codeMuted 在 codeBg、codeHeaderBg 上 | **两处不达标，已修**：codeComment（浅 3.84 / 深 3.53，在代码头上）、深色 codeMuted 在代码头上 4.15 |
| text / textMuted 在 selected、inlineCode、tableStripe、tableHeader、accentSoft、accentFaint 上 | 全部 ≥ 5.6 |
| danger / info / accent 在各自的 Soft 底上 | 全部 ≥ 5.2 |
| textOnAccent on accentHover；userBubbleText on userBubble | 全部 ≥ 8 |
| 焦点环（accent）在五种基础表面上 | 浅 ≥ 9，深 ≥ 7.4，远高于 3:1 |

本仓库的等价机械覆盖是 §8 的 `compose/Theme/tests/A11yRegression.fs`；旧断言里 textFaint 在五种基础表面上的组合没有对应物。

修复：`libs/ThemePalette/ThemePalette.fs`（旧 `Design/Palette.fs`）中 `codeComment` 两套主题统一为 `#96938B`，深色 `codeMuted` 改为 `#9B9890`（与浅色同值）。修复后最低 4.86:1。注释色仍明显弱于 `codeText`，视觉层级不变。

已知不达标但未改：
- `border`（浅 1.33、深 1.92）、`line`（浅 1.6、深 2.1~2.5）低于 1.4.11 对“控件边界”的 3:1 要求。它们主要用作装饰性分割，**不再用作输入类控件的边界**：2026-10-18 起，`libs/ThemePalette` 新增 `ControlStroke` 令牌（浅 `#8D8A7E`、深 `#76767F`），对四种基础表面（Surface / SurfaceRaised / SurfaceContainer / Canvas）两套主题都 ≥ 3:1；`FieldSurface` 与 `Surface` 同值（没有填充差），所以边界只靠这条描边——「以描边作为唯一边界」实测成立，输入壳、开关轨道与底部输入区（`ComposerBox` 的 `BoxInk.ControlStroke`）都改用它。聚焦态仍是 accent 描边加外扩环（见 `libs/UiKit` 的 textField 外壳；旧 `Controls/Interaction.fs`）。机械断言：`compose/Theme/tests/A11yRegression.fs` 的 `控件边界描边在四种底上都过 1.4.11`。`border` / `line` 自身数值不变。

## 3. 键盘导航与焦点

- 可点击表面统一走 `Ui.iconButton` / `ActionBorder` 等原语，`Focusable = true`，焦点环只在 `:focus-visible`（Tab / 方向键）或程序化焦点时出现，鼠标点击不留环（旧 `FocusRingTests`；本仓库等价用例：`libs/FocusOrigin/tests`「键盘与程序化聚焦挂类名，指针聚焦与失焦去掉」）。
- 全应用没有显式 `TabIndex`，Tab 顺序等于视觉树顺序。这是正确默认值，**规则：不要引入正的 TabIndex**；顺序问题应通过调整视觉树解决。
- 对话框 / 弹层 Esc 关闭、关闭后焦点回到触发处（旧 `DismissFocusTests`、`OverlayHost.fs`；今天迁到 `libs/FocusOrigin` 与 `compose/OverlayDesk`，用例如 `libs/FocusOrigin/tests` 的「dialog … restores focus」「escape precedence …」与 `compose/AppShell/tests` 的「打开设置记下焦点归宿，关闭时把焦点还回去」）。规则：任何新弹层必须同时满足这两条。
- 全局快捷键见 `compose/DialogsDesk`（帮助面板在 `libs/ConfirmPane`；旧 `Dialogs.fs`）的快捷键帮助。待实测：快捷键与屏幕阅读器自身快捷键、浏览器（PWA）保留键（Ctrl+W、Ctrl+N 在浏览器标签页内会被浏览器抢占）的冲突。
- PWA 壳：`app/Pwa/wwwroot/app.css` 保留了 `:focus-visible` 外扩环；Avalonia 画布内部的焦点由 Avalonia 绘制，壳层把 `#out *` 的 outline 关掉是有意为之。

## 4. 可访问名称与语义

- 共享原语 `iconButton`（今天在 `libs/UiKit/UiKit.fs`）在 tip 非空时同时设置 `ToolTip` 与 `AutomationProperties.Name`；旧仓库当时有 220 余处 `AutomationProperties` 使用（本仓库不再记这个总数，机械检查见 §8）。
- 规则（已测）：图标按钮调用不得传空字符串或 null 作为 tip，否则按钮对辅助技术是无名控件。
- PWA 启动页有 `role="status"`、`aria-live`、`aria-busy`，装饰条 `aria-hidden`，图片有 `alt`。入口页 `lang="zh-CN"`，viewport 不禁止缩放。

## 5. 动效

- 几何动画被禁止（旧 `DesignConvergenceTests`，未随迁；本仓库的动效机械检查是 `libs/ColorBlend` 的颜色补间金样与 `compose/Theme/tests` 的「reduced motion …」用例，**没有**等价于「禁止几何动画」的断言），状态只补间颜色与不透明度。
- PWA 样式含 `prefers-reduced-motion`（本仓库仍由 `compose/Theme/tests/A11yRegression.fs` 静态断言存在）。已查（2026-10-18）：Avalonia 12.1.2 的 `IPlatformSettings` 只有点击尺寸、热键和 `GetColorValues`。`PlatformColorValues.ContrastPreference` 是系统高对比，不是减弱动效。画布内补间因此读不到系统“减少动态”；`libs/MotionSwitch` 只由 `libs/UiPrefs.reduceMotion`（默认 false）经 `compose/AppShell` 的 `SetReducedMotion` 驱动。PWA 壳的 `prefers-reduced-motion` 只作用于壳层 CSS，不进入 Avalonia 画布。

## 6. 文案一致性与术语表

规范写法（已测的禁用同义词在括号内）：

| 概念 | 统一写法 | 禁止 |
| --- | --- | --- |
| 模型接入方 | 模型服务 | 服务商、提供商、供应商、提供方 |
| 一次聊天的完整记录 | 对话 | 会话 |
| 万象实例（服务端） | 主机 | 服务器、服务端 |
| 从某条消息长出的新对话 | 分叉（同一条消息下的备选叫“版本”） | 分支 |
| 凭据 | 密钥 / API Key | 秘钥、Api Key、api key、Apikey |
| 偏好页 | 设置 | 设定 |
| 省略号 | …（中文省略号） | ... |
| 取消类控件的提示 | 须带「(Esc)」 | — |

旧仓库测试基线为 0 处；本仓库的等价扫描是 `compose/Theme/tests/A11yRegression.fs` 的术语用例（本文不重复其结论）。以下是**未固化为测试**的不一致（第 1 条 W6 起已固化为测试），建议由各屏幕分支顺手统一：

1. **会话 / 对话**（已由 W6 解决）：名词统一为“对话”，“会话”不再出现在用户可见字符串里；术语表见 `redesign-proposal.md` §4.6，扫描见 `compose/Theme/tests/A11yRegression.fs`（旧名 `A11yRegressionTests`）。
2. **快捷键书写**：已统一为 `Ctrl / ⌘ + 键`（帮助面板 `compose/DialogsDesk`、侧栏按钮提示金样 `compose/ChatScreen/tests/golden/screen.txt`）。旧审计里的 `Ctrl+B / ⌘B` 与 `Ctrl+Enter / ⌘Enter` 是旧仓库写法，本仓库用户可见字符串里不再出现。
3. **“聊天”仅 1 处**（设置页关于说明“聊天客户端”），其余产品文案用“对话”，可保留。

## 7. 屏幕级待办（不在本分支处理）

| 项 | 位置 | 说明 |
| --- | --- | --- |
| ~~输入框静止态边界对比度~~ | 全部输入壳 | 已由 `ControlStroke`（2026-10-18，见 §2）解决，机械断言在 `compose/Theme/tests/A11yRegression.fs` |
| ~~术语：会话→对话~~ | 已由 W6 完成 | 见 §6 |
| ~~快捷键格式统一~~ | 已统一 | 见 §6 |
| 屏幕阅读器实测 | 全部 | 见 §1 |
| ~~仓库 `NuGet.config` 的 `fallbackPackageFolders` 写死了某开发者家目录，目录不存在时 restore 报 NU1301~~ | 旧仓库的 NuGet.config | 旧仓库问题；本仓库 `NuGet.config` 已重写为本地 feed（`artifacts/feed`，见 `./x pack`）+ nuget.org，无家目录写死 |

## 8. 回归测试清单

`compose/Theme/tests/A11yRegression.fs`（旧名 `tests/Wanxiang.Tests/A11yRegressionTests.fs`；全部非 UI、毫秒级，随 `./x compose` 运行，CI 是 `.github/workflows/ci.yml` 的 `compose` job；`run-tests.sh` 与 pr-gate 在本仓库不存在）：

- 代码块 13 种着色在 codeBg / codeHeaderBg 上 ≥ 4.5
- text / textMuted 在 6 种内嵌底色上 ≥ 4.5
- 状态色在浅底上、textOnAccent 在 accent 与 accentHover 上、用户气泡文字 ≥ 4.5
- 焦点环色在 5 种基础表面上 ≥ 3
- 控件边界描边 `ControlStroke` 在 4 种基础表面上 ≥ 3（1.4.11；2026-10-18 新增）
- 遮罩 alpha 下限
- 图标按钮无空名称；共享原语同时设置 ToolTip 与 AutomationProperties.Name
- 文案：禁用同义词、禁用 `...`、取消类提示含 Esc
- PWA：`lang`、`<title>`、可缩放、启动页 aria、`prefers-reduced-motion`、`:focus-visible`、manifest 名称与图标

新增调色板槽位或新的取消类控件时，先看是否需要补进这些测试。
