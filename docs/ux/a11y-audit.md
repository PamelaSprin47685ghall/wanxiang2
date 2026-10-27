# 无障碍与 UX 一致性审计

> 基准变更（2026-10-18）：本文写于旧仓库 wanxiang（2026-10-17 退役，活文档迁入本仓库），正文里的 `src/Wanxiang.UI`、`src/Wanxiang.Pwa/wwwroot`、`tests/Wanxiang.Tests/…` 与旧测试名（`DesignConvergenceTests`、`UiStabilityTests`、`FocusRingTests`、`DismissFocusTests`）都是旧基准，未随迁。今天 UI 拆在 `compose/*` 与 `libs/*`，PWA 壳在 `app/Pwa/wwwroot`，机械回归是 `compose/Theme/tests/A11yRegression.fs`（非 UI，随 `./x compose` 跑；CI 见 `.github/workflows/ci.yml` 的 `compose` job，`run-tests.sh` 与 pr-gate 不存在）。

本文件是旧仓库 `ux/redesign-4-a11y-tests` 的交付：对全应用的无障碍、键盘导航、焦点顺序、对比度与文案一致性做一次**静态审计**，并把能机械判定的规则固化为测试（当时 `tests/Wanxiang.Tests/A11yRegressionTests.fs`，今天 `compose/Theme/tests/A11yRegression.fs`）与 CI 步骤。
本轮不改任何具体屏幕的布局或交互，只动共享令牌；屏幕级问题记在 §7，留给各屏幕分支或后续处理。

## 1. 方法与边界

- 依据：读旧仓库的 `src/Wanxiang.UI` 与 `src/Wanxiang.Pwa/wwwroot` 源码（对应今天的 `libs/*` + `compose/*` 与 `app/Pwa/wwwroot`），按调色板数值计算 WCAG 2.x 对比度。
- **真机实测（2026-10-26 完成 macOS 本机实测，见 §10）**：已在 macOS 本机（`darwin 25.6.0`）对真实桌面窗口（`app/Desktop`）完成原生 `NSAccessibility` / `AXUIElement` 全屏幕树巡检、VoiceOver 中文语音合成（`zh-CN Tingting`）分词播报、主界面与设置页 6 大分区真实 `Tab` / 方向键焦点流转轨迹、三大弹层（命令面板 `⌘+K` / 快捷键帮助 `⌘+/` / 设置页 `⌘+,`）`Esc` 焦点归位，以及快捷键与 VoiceOver 修饰键冲突实测；PWA 壳 Lighthouse 无障碍跑分（100%）已于 §9 完成。仍未覆盖的平台：Windows NVDA、Linux Orca（宿主无 `orca`/`at-spi`）、Android TalkBack（无物理设备）。
- **已自动化（2026-10-22，决策 G5）**：整棵无障碍树遍历断言 `compose/AppShell/tests/A11yTraversalTests.fs`（随 `compose/AppShell` 的测试跑）。它在无头 Avalonia 里搭出全部主屏幕，把每棵控件树走一遍，断言**每个可聚焦控件都有可访问名称与角色**。判定口径见 §4 与 §8。
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
- `border`（浅 1.33、深 1.92）、`line`（浅 1.6、深 2.1~2.5）低于 1.4.11 对“控件边界”的 3:1 要求。它们主要用作装饰性分割，**不再用作输入类控件的边界**：2026-10-18 起，`libs/ThemePalette` 新增 `ControlStroke` 令牌（浅 `#8D8A7E`、深 `#76767F`），对四种基础表面（Surface / SurfaceRaised / SurfaceContainer / Canvas）两套主题都 ≥ 3:1；`FieldSurface` 与 `Surface` 同值（没有填充差），所以边界只靠这条描边——「以描边作为唯一边界」实测成立，输入壳、开关轨道与底部输入区（`ComposerBox` 的 `BoxInk.ControlStroke`）都改用它。聚焦态仍是 accent 描边加外扩环（见 `各输入控件外壳` 的 textField 外壳；旧 `Controls/Interaction.fs`）。机械断言：`compose/Theme/tests/A11yRegression.fs` 的 `控件边界描边在四种底上都过 1.4.11`。`border` / `line` 自身数值不变。

## 3. 键盘导航与焦点

- 可点击表面统一走 `Ui.iconButton` / `ActionBorder` 等原语，`Focusable = true`，焦点环只在 `:focus-visible`（Tab / 方向键）或程序化焦点时出现，鼠标点击不留环（旧 `FocusRingTests`；本仓库等价用例：`libs/FocusOrigin/tests`「键盘与程序化聚焦挂类名，指针聚焦与失焦去掉」）。
- 全应用没有显式 `TabIndex`，Tab 顺序等于视觉树顺序。这是正确默认值，**规则：不要引入正的 TabIndex**；顺序问题应通过调整视觉树解决。
- 对话框 / 弹层 Esc 关闭、关闭后焦点回到触发处（旧 `DismissFocusTests`、`OverlayHost.fs`；今天迁到 `libs/FocusOrigin` 与 `compose/OverlayDesk`，用例如 `libs/FocusOrigin/tests` 的「dialog … restores focus」「escape precedence …」与 `compose/AppShell/tests` 的「打开设置记下焦点归宿，关闭时把焦点还回去」）。规则：任何新弹层必须同时满足这两条。
- 全局快捷键见 `compose/DialogsDesk`（帮助面板在 `libs/ConfirmPane`；旧 `Dialogs.fs`）的快捷键帮助。**2026-10-26 实测**：macOS VoiceOver 默认修饰键为 `Control + Option`（或 `Caps Lock`），而万象桌面端快捷键采用 `⌘ (Command)`（如 `⌘+K` 命令面板、`⌘+N` 新对话、`⌘+B` 切换侧边栏、`⌘+,` 设置、`⌘+/` 快捷键帮助、`⌘+F` 搜索），修饰键命名空间正交，VoiceOver 开启状态下 `⌘+K` / `⌘+B` / `⌘+,` / `⌘+/` / `Esc` 均无抢键冲突；PWA 在浏览器标签页内运行时，`Ctrl/⌘ + N` 与 `Ctrl/⌘ + W` 会被浏览器宿主抢占为新建窗口与关闭标签页，应用内已由 `Ctrl/⌘ + K` 命令面板与界面顶栏/侧栏按钮提供无冲突入口。
- PWA 壳：`app/Pwa/wwwroot/app.css` 保留了 `:focus-visible` 外扩环；Avalonia 画布内部的焦点由 Avalonia 绘制，壳层把 `#out *` 的 outline 关掉是有意为之。

## 4. 可访问名称与语义

- 共享原语 `iconButton`：`libs/DevicesPane/Ui.fs` 的 `Ui.iconButton`（设置页各分区共用），以及 `compose/Theme/ThemeStyles.fs` 的 `icon-button` 样式类与 `compose/MessageCardView/Looks.fs` 的装饰函数。其中只有 `DevicesPane.Ui.iconButton` 在此入口同时设置 `ToolTip` 与 `AutomationProperties.Name`；`ThemeStyles` 的样式类和 `MessageCardView/Looks.fs` 的装饰函数只负责外观，不能据此证明按钮已有可访问名称，须检查实际调用方及原生 peer；旧仓库当时有 220 余处 `AutomationProperties` 使用（本仓库不再记这个总数，机械检查见 §8）。历史上的 `UiKit` 已整库退役删除（2026-10-20），不再是任何原语的落点。
- 规则（已测）：图标按钮调用不得传空字符串或 null 作为 tip，否则按钮对辅助技术是无名控件。
- **整树遍历断言（已测，决策 G5）**：`compose/AppShell/tests/A11yTraversalTests.fs` 对每个主屏幕的控件树（视觉树 ∪ 逻辑树）逐节点判定：
  - 可聚焦 = `Focusable` 且沿祖先链可见且沿祖先链可用（与 `libs/FocusOrigin` 的 `focusables` 同口径）；
  - 可访问名称 = 自动化 peer 的 `GetName()`，为空时退回 `AutomationProperties.Name`；自动派生的类型名（`Avalonia.*` / `*Control` / `*Panel` / `*Presenter`）不算名字；
  - 角色 = 自动化 peer 的 `GetAutomationControlType()`；`None`（未暴露给辅助技术）与 `Custom`（未指定具体角色）都算缺角色。
  - 覆盖的屏幕：工作区、侧栏、对话区、输入区、审批区、起步引导、命令面板、模型切换浮层、提示条、重命名 / 确认 / 编辑消息 / 连接 / 对话设置对话框、快捷键帮助、导出面板、导入面板，以及设置页的 6 个分区（模型服务 / 工具 / 对话默认值 / 设备 / 外观与操作 / 关于）。
  - 违例一次性汇总成一份清单（屏幕 / 路径 / 控件类型 / 原因），便于一次改完。
  - 边界：弹层内容不在工作区的视觉树里，所以每个弹层按自己的树单独走；这条断言只管**名字与角色**，不管焦点顺序、朗读顺序与实际发音。
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
| ~~macOS 屏幕阅读器（VoiceOver / NSAccessibility）与真实 Tab 轨迹实测~~ | 桌面端全部主屏幕、三大弹层与设置 6 大分区 | 已于 2026-10-26 在 macOS 本机完成实测并记录证据与 3 项真机边界，见 §10 |
| 其他平台屏幕阅读器实测（Windows NVDA / Linux Orca / Android TalkBack） | 跨平台客户端 | 受限于当前宿主无对应物理设备与读屏守护进程，见 §9、§10 |
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

`compose/AppShell/tests/A11yTraversalTests.fs`（2026-10-22 新增，决策 G5；UI 测试，随 `compose/AppShell` 的测试跑，需要无头 Avalonia 会话）：

- 每个主屏幕（工作区 / 侧栏 / 对话区 / 输入区 / 审批区 / 起步引导 / 命令面板 / 模型切换浮层 / 提示条 / 五个对话框 / 快捷键帮助 / 导出面板 / 导入面板）与设置页 6 个分区：每个可聚焦控件都有可访问名称
- 同一批控件都有角色（control type），不允许 `None` / `Custom`
- 口径与边界见 §4

**这条断言替不了的，已在真机实测或列明平台边界（见 §9、§10）**：

- 真机屏幕阅读器实测：**macOS VoiceOver / 原生 NSAccessibility 树与 `zh-CN Tingting` 语音合成已于 2026-10-26 完成本机实测**（见 §10）；Windows NVDA / Linux Orca / Android TalkBack 因无对应物理设备待对应平台实测；
- PWA 壳的 Lighthouse 跑分：**已完成（100.0%，见 §9）**；
- 真实 Tab 轨迹与弹层焦点归位：**macOS 桌面窗口已于 2026-10-26 完成连续 Tab/Shift+Tab 轨迹、三大弹层 `Esc` 归位与设置 6 大分区方向键巡检实测**（见 §10）；
- 屏幕阅读器与浏览器快捷键冲突：**已于 2026-10-26 完成实测**（见 §3、§10）。

新增调色板槽位或新的取消类控件时，先看是否需要补进这些测试。

## 9. 2026-10-24 交付实跑无障碍证据与边界

- **Lighthouse 审计**：对发布后 PWA 宿主执行无障碍专项审计（`npx lighthouse http://127.0.0.1:<PORT>/ --only-categories=accessibility`），得分 **100.0%**（9 项标准基准检查全绿，原始报告见 `docs/evidence/2026-10-24-verified-release/artifacts/lighthouse/` 与 `docs/evidence/2026-10-24-verified-release/logs/final-lighthouse.log`）。
- **工具局限声明（Canvas UI）**：Avalonia Web 应用基于 Skia Canvas / WebAssembly 绘制。Lighthouse 100% 得分仅证明外部宿主 HTML 骨架（如 `<html lang="zh-CN">`、`<title>`、viewport、meta 等）无障碍达标，**不代表 Canvas 内部所有自绘控件已具备完整读屏与可访问语义**，不可替代屏幕阅读器等辅助技术。
- **实机读屏与移动端真机未测边界**：
  - 宿主环境缺少 Linux Orca 读屏守护进程与物理 iOS / Android 移动设备，这两个平台的真实读屏与触控手势未测；macOS 本机读屏与原生辅助功能树实测已于 2026-10-26 补齐（见 §10）；
  - 详见 `docs/evidence/2026-10-24-verified-release/README.md` §七。

## 10. 2026-10-26 macOS 本机真机读屏层（VoiceOver / NSAccessibility）全旅程实测

在 macOS 26.6.2 (`darwin 25.6.0`, arm64) 本机启动真实桌面窗口（`--server --client --pwa=false`）与系统 `VoiceOver`，经 `ApplicationServices` 的 `AXUIElement` API 与 `AVSpeechSynthesizer`（系统中文语音 `com.apple.voice.compact.zh-CN.Tingting`）对全部主屏幕、三大弹层、设置页 6 大分区及真实 `Tab` / `Shift+Tab` / 方向键焦点轨迹完成真机实测：

### 10.1 已验证通过的真机能力

1. **主界面与设置页 6 大分区原生 AX 节点导出**：
   - 窗口以 `AXWindow/AXStandardWindow`（标题 `"万象"`）挂载到 macOS 辅助功能树，首屏共 **217 个节点**、**28 个带名交互与语义节点**；
   - **主界面（起步引导态与已配模型对话态）**：`"新对话"` (`AXButton`)、`"清空搜索"` (`AXButton`)、`"搜索标题和正文"` (`AXTextField`)、`"设置（Ctrl / ⌘ + ,）"` (`AXButton`)、`"命令面板"` (`AXButton`)、`"重新连接主机"` (`AXButton`)、`"对话列表"` (`AXList/AXContentList`)、`"调整侧边栏宽度"` (`AXHandle`)、`"切换侧边栏（Ctrl / ⌘ + B）"` (`AXButton`)、`"添加一个模型服务"` (`AXGroup`)、6 个模型服务预设按钮（`"OpenAI"` / `"Anthropic Claude"` / `"Google Gemini"` / `"Ollama（本机）"` / `"自定义地址"` / `"更多模型服务"`）、`"API Key"` (`AXTextField`)、`"连接"` (`AXButton`)、`"高级设置"` (`AXButton`)、`"输入消息"` (`AXTextField`)、`"添加附件"` (`AXButton`)、`"切换本对话使用的模型"` (`AXButton`)、`"发送"` (`AXButton`) 全部准确导出；
   - **命令面板（`⌘+K`）**：导出 `"命令面板"` (`AXTextField`) 与命令列表行 `"新建对话"` (`AXRow`)、`"切换模型"` (`AXRow`)、`"切换主题"` (`AXRow`) 等；
   - **快捷键帮助弹窗（`⌘+/`）**：导出 `"关闭"` (`AXButton`) 与滚动区控件；
   - **设置页（`⌘+,`）全部 6 个分区**（经左栏 `AXTabGroup "设置分区导航"` 方向键 `↓` 逐一切换实测）：
     - **模型服务** (`AXRadioButton "模型服务"`)：`"返回对话"` (`AXButton`)、`"添加模型服务"` (`AXButton`)；
     - **工具** (`AXRadioButton "工具"`)：`"授权文件夹"` (`AXButton`)、`"显示调试工具"` (`AXGroup`/`AXButton`)、`"添加 MCP 服务"` (`AXButton`)；
     - **对话默认值** (`AXRadioButton "对话默认值"`)：`"Temperature"` (`AXTextField`)、`"Top P"` (`AXTextField`)、`"最大输出 token"` (`AXTextField`)、`"上下文消息上限"` (`AXTextField`, `val="200"`)、`"工具调用轮数上限"` (`AXTextField`, `val="12"`)、`"默认系统指令"` (`AXTextField`)、`"自动生成对话标题"` (`AXGroup`/`AXButton`)、`"保存默认值"` (`AXButton`)、`"放弃修改"` (`AXButton`)、`"导入对话"` (`AXButton`)；
     - **设备** (`AXRadioButton "设备"`)：`"设备列表"` (`AXGroup`)、`"刷新设备列表"` (`AXButton`)；
     - **外观与操作** (`AXRadioButton "外观与操作"`)：`"主题"` (`AXPopUpButton`)、`"更小"` (`AXButton`)、`"更大"` (`AXButton`)，以及 `"Enter 直接发送"`、`"完成后收起思考过程"`、`"代码块长行折行"`、`"在侧栏显示已归档对话"`、`"减少动态效果"`、`"保存未发送的草稿"` 6 项开关与 `"清除本地草稿"` (`AXButton`)；
     - **关于** (`AXRadioButton "关于"`)：`"复制诊断报告"` (`AXButton`)、`"检查主机"` (`AXButton`)。

2. **已配置模型服务态（日常主对话界面）连续 `Tab` 焦点闭环**：
   - 连续发送真实 `Tab` 键，原生 `kAXFocusedUIElementAttribute` 轨迹按视觉从上到下顺序依次走过：
     `#01 [AXButton] "新对话"` → `#02 [AXTextField] "搜索标题和正文"` → `#03 [AXList] "对话列表"` → `#04 [AXButton] "设置（Ctrl / ⌘ + ,）"` → `#05 [AXButton] "命令面板"` → `#06 [AXButton] "重新连接主机"` → `#07 [AXHandle] "调整侧边栏宽度"` → `#08 [AXButton] "切换侧边栏（Ctrl / ⌘ + B）"` → `#09 [AXTextField] "输入消息"` → `#10 [AXButton] "新对话"`，9 个可聚焦节点首尾闭环，无焦点丢失。

3. **三大弹层打开与 `Esc` 关闭后焦点归位与模态隔离**：
   - **命令面板（`⌘+K`）**：触发前焦点在 `[AXButton] "命令面板"` → 按 `⌘+K` 后焦点自动落入搜索框 `[AXTextField] "命令面板"` → 按 `Esc` 关闭后焦点精确归位至 `[AXButton] "命令面板"`；
   - **快捷键帮助（`⌘+/`）**：触发前焦点在 `[AXButton] "命令面板"` → 按 `⌘+/` 后焦点自动落入弹窗内 `[AXButton] "关闭"` → 按 `Esc` 关闭后焦点精确归位至 `[AXButton] "命令面板"`；
   - **设置页（`⌘+,`）**：按 `⌘+,` 打开后底层工作区禁用（`Workspace.Host.IsEnabled = false`），焦点自动落入左栏当前分区 `[AXRadioButton] "模型服务"`，`Tab` / `Shift+Tab` 由 `OverlayLayer.TrapTab` 严格圈定在设置页内部，支持 `↑`/`↓` 方向键在 6 个分区间循环移动并实时切换右侧内容区；按 `Esc` 关闭后恢复底层工作区可用，焦点由工作区状态机统一归位至主界面 `[AXTextField] "输入消息"`。

4. **VoiceOver 中文与中英混排语音合成（`zh-CN Tingting`）**：
   - 13 条核心词条（含中英混排与符号词条 `"设置（Ctrl / ⌘ + ,），按钮"`、`"切换侧边栏（Ctrl / ⌘ + B），按钮"`、`"OpenAI，按钮"`、`"Anthropic Claude，按钮"`、`"Ollama（本机），按钮"`、`"API Key，文本字段"`）13/13 完成合成播报，分词边界清晰。

### 10.2 真机实测发现的边界与修复闭环（Headless 静态测试无法检出）

1. **Avalonia 12.1.2 `Avalonia.Native` (macOS) 底座桥接边界（上游框架层，已提报 [AvaloniaUI/Avalonia#22614](https://github.com/AvaloniaUI/Avalonia/issues/22614)）**：
   - **子角色（`AXSubrole`）未映射**：217 个节点中有 210 个节点的 `AXSubrole` 为 `AXUnknown`（主角色 `AXRole` 正常为 `AXButton` / `AXTextField` / `AXRadioButton` 等），根因为 `native/Avalonia.Native/src/OSX/automation.mm` 的 `accessibilitySubrole` 默认返回 `NSAccessibilityUnknownSubrole` 而非 `nil`；
   - **应用内控件的 `AXUIElementCopyActionNames` 与 `AXPress` 实测复核（2026-10-27 实证收口）**：
     - **真实应用行为对照**：在运行中的 `wanxiang` 桌面窗口上，系统标题栏的 3 个原生窗口按钮（`AXCloseButton` / `AXFullScreenButton` / `AXMinimizeButton`）调用 `AXUIElementCopyActionNames` 返回 `AXError = 0 (kAXErrorSuccess)` 与 `["AXPress", ...]`；应用内全部 17 个 `AvnAccessibilityElement` 按钮（如 `"新对话"`、`"设置（Ctrl / ⌘ + ,）"`、`"OpenAI"`、`"发送"` 等）调用 `AXUIElementCopyActionNames` 均返回 `AXError = -25200 (kAXErrorFailure)`、列表为空 `[]`。然而，直接对应用内按钮调用 `AXUIElementPerformAction(btn, kAXPressAction)` **均返回 `AXError = 0 (kAXErrorSuccess)` 并真实触发动作**（实测对 `"设置（Ctrl / ⌘ + ,）"` 执行 `AXPress` 立即打开设置页并将焦点移至 `"模型服务"` 单选按钮；再对设置页内 `"返回对话"` 执行 `AXPress` 立即关闭设置页返回主界面）；
     - **机器码级根因（`libAvaloniaNative.dylib` 反汇编验证）**：`AvnAccessibilityElement` 继承自 macOS 新版方法型基类 `NSAccessibilityElement`。在 `-[AvnAccessibilityElement accessibilityActionNames]`（偏移 `0xdeac`）中，代码首先调用 `[[super] accessibilityActionNames]` 再追加 `@"AXScrollToVisible"`；但父类 `NSAccessibilityElement` 并未实现旧版非正式协议的 `-accessibilityActionNames` 选择子，导致 `[super accessibilityActionNames]` 抛出 `NSInvalidArgumentException (unrecognized selector)`，被 HIServices 捕获并转为 `kAXErrorFailure (-25200)`。而执行动作时，`-[AvnAccessibilityElement accessibilityPerformAction:]`（偏移 `0xdf68`）对非 `@"AXScrollToVisible"` 动作调用 `[[super] accessibilityPerformAction:]`，由 `NSObject(NSAccessibilityInternal)` 正常桥接至新版协议方法 `-[AvnAccessibilityElement isAccessibilitySelectorAllowed:@selector(accessibilityPerformPress)]`（偏移 `0xe0d4`，检查 `IsInvokeProvider` / `IsExpandCollapseProvider` / `IsToggleProvider`）与 `-[AvnAccessibilityElement accessibilityPerformPress]`（偏移 `0xdc24`），因此 `AXPress` 实际执行完全正常，仅动作名枚举查询因 `super` 调用崩溃返回 `-25200`。
2. **首次引导卡（`OnboardingPane`）内的 `Tab` / `Shift+Tab` 焦点跳出（已修复）**：
   - **根因**：① Avalonia 12.1.2 的 `WrapPanel` 实现 `INavigableContainer.GetControl` 时仅处理四向方向键，对 `NavigationDirection.Next` / `Previous`（`Tab` / `Shift+Tab`）直接返回 `null`；② `OnboardingPane.Render` 每次调用都会 `chipRow.Children.Clear()` 重建按钮实例，导致刚获得焦点的芯片在重绘时从视觉树摘除、焦点丢失为 `null`。
   - **修复与验证**：`libs/DevicesPane/OnboardingPane.fs` 改为按芯片键集合复用按钮实例（仅在键列表变化时重建），并在 `chipRow` 上拦截 `Key.Tab` 沿内部可聚焦芯片顺序流转、首尾越界时交还外层容器；由 `libs/DevicesPane/tests/OnboardingPane.Tests.fs`「`引导卡内连续按 Tab 依次走过服务商键与更多并进入 API Key 输入框，且重复渲染保留焦点`」回归守卫。
3. **侧栏内部 `DockPanel` 视觉顺序与 `Tab` 顺序局部倒置（已修复）**：
   - **根因**：侧栏原用 `DockPanel` 布局，必须先挂载 `Dock.Bottom` 的底栏按钮、最后填充中部 `"对话列表"`，导致视觉树顺序与从上到下的视觉顺序不一致。
   - **修复与验证**：`compose/SidebarDesk/SidebarDesk.fs` 将 `DockPanel` 替换为三行 `Grid(RowDefinitions = "Auto,*,Auto")`，依次挂载顶栏（Row 0）、中部对话列表（Row 1）、底栏按钮（Row 2）；由 `compose/SidebarDesk/tests/Tests.fs`「`侧栏视觉树顺序满足从上到下自然 Tab 顺序：搜索框在对话列表之前，对话列表在底栏按钮之前`」回归守卫。
4. **设置页全屏覆盖层（`SettingsScreen`）未隔离背景工作区与 `Tab` 链（已修复）**：
   - **根因**：设置页以 `ZIndex = 2` 叠在根 `Grid` 上，打开期间底层 `Workspace.Host` 仍处于 `IsEnabled = true` 且 `SettingsScreen` 未拦截 `Tab` 循环。
   - **修复与验证**：`compose/SettingsScreen/SettingsScreen.fs` 在根层挂载 `Key.Tab` 拦截并转发 `overlay.TrapTab(surface, e)`，`compose/AppShell/AppShell.fs` 在 `vs.Settings.IsSome` 期间将 `v.Workspace.Host.IsEnabled` 设为 `false`、关闭后恢复 `true`；由 `compose/AppShell/tests/ComposerTests.fs`「`设置页打开期间禁用底层工作区并将 Tab 焦点圈定在设置页内，关闭后恢复底层工作区可用`」回归守卫。
