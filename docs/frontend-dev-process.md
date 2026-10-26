# 前端开发流程与交付清单

> **快照声明**：本文件是 2026-10-21 的盘点快照，正文中的「未建 / 没有」断言均以当日为准。此后 G3（画廊像素基线门禁，41/41 条目 × 深浅双主题共 82 张 PNG，`tools/gallery-baseline.sh`）与浏览器侧真实交互 e2e 冒烟（`tools/smoke_published.sh`，28/28 项断言）均已建成并接入门禁面，现行口径以根 `AGENTS.md` SPEC 7 与 `compose/Gallery/AGENTS.md` 为准。
>
> 审计日期：2026-10-21。审计范围：仓库 `/home/kunweiz/Desktop/vibe/wanxiang2` 的界面验证手段（测试、冒烟、性能、a11y、CI、工具链）。
> 本文件只做盘点与提案，不改任何源码。读者：提出「目前没有遵循前端项目的标准开发流程，导致界面缺陷」的人，以及要动手补流程的人。
> 结论一句话：界面验证从「结构断言」起步，现在**多**了三样东西——断言卫生门禁、界面文案门禁、组件画廊；但**仍然没有**像素级视觉回归、浏览器侧真实交互 e2e，也没有无障碍树的整树遍历断言。§5 是一份**端到端的交付清单**（DoD），由改界面的工程师自己走完并留证。

---

## 1. 现状盘点

### 1.1 全局：CI 实际跑什么

`.github/workflows/ci.yml` 的作业：`check`（`./x check`，依赖与文档引用检查）、`pack`（`./x pack` 产本地 feed 工件）、`test`（每个库一个矩阵任务跑 `./x test <lib>`）、`compose`（`./x compose`，跑 67 个组合者测试工程）、`consume`（仓库外消费）、`app`（构建 `app/Desktop` 与 `app/Pwa`）、`published-smoke`（`publish.sh` + `tools/smoke_published.sh`）、`widget-template`（`./x new-widget CiSmoke` 生成与自检）。

**证明的东西**：代码能编译、库与组合者能只经交付包构建、发布产物能在 headless Chrome 里跑起来。
**不能证明的东西**：任何界面外观、任何交互结果、任何无障碍树形态。CI 里没有一个 job 会因为「按钮画歪了」「焦点丢了」「对比度错了」「文案错字」而失败——除非该缺陷恰好被某条结构断言覆盖。

### 1.2 逐项：现有界面验证手段与其覆盖边界

| 手段 | 文件 | 断言了什么 | 覆盖边界（诚实的说法） |
| --- | --- | --- | --- |
| 逐控件 headless 行为测试 | `libs/*/tests/*.Headless.fs`（如 `libs/ActionStrip/tests/ActionStrip.Headless.fs`）、`compose/*/tests/Headless.fs` | 用 `HeadlessUnitTestSession`（不是 `Avalonia.Headless.XUnit`，见根 AGENTS.md「控件库交付约定」）在 UI 线程上构造控件，断言控件树形状、可见性、`AutomationProperties` 名称、点击后的状态转移 | **只断言控件树与行为，不看像素。** 没有任何一处比较渲染结果；颜色、字号、间距、内边距错位、遮挡、裁切、文字溢出全部不可见。`AutomationProperties.GetName` 的断言是逐点手写的，不是「每个可交互控件都有名字」的系统性遍历（见 §2.5）。 |
| 组合者 headless 旅程测试 | `compose/AppShell/tests/ComposerTests.fs`（21 个 `[<Fact>]`）+ `compose/AppShell/tests/Tests.fs`（3 个）+ `compose/AppShell/tests/JourneyAttachmentTests.fs`（1 个，**真实控件树**）+ `compose/AppShell/tests/ExpiryDeletionTests.fs`（1 个，**到期真删**） | 假 `ShellPorts`（`compose/AppShell/tests/Rig.fs`）记录 `Effect`，断言 MVU 状态机投影；`JourneyAttachmentTests.fs` 额外驱动真实控件（「添加附件」按键、发送按键、托盘 chip）并断言控件的可用性/提示/状态字；`ExpiryDeletionTests.fs` 用可推进的假时钟推进宽限期，断言真的发出 `conversation.delete`，并把**同一条**命令落进服务端事件存储，断言投影里该对话被标记删除、列表里不再出现 | 前两处断言的是**模型与 Effect 序列**，不是控件树。SPEC 7 要求的「原生交互证明」（焦点流转、无障碍角色）在这一层仍没有系统性发生；旅程 2 已改为强断言（见 §4.5），旅程 3 已有**真实控件树**用例（见 §4.5），旅程 6 已有**到期真删**用例（见 §4.5），但旅程 1/5 仍只验状态投影与 Effect 序列（见 §4.5）。 |
| 主题 a11y 机械回归 | `compose/Theme/tests/A11yRegression.fs` | 纯数值对比度计算（`ThemePalette` 的色值算 WCAG 比值）+ PWA 壳的**交付物**检查（`index.html` / `app.css` / `manifest.webmanifest` 的真实属性） | **不构造任何 Avalonia 控件**，不读真实无障碍树：对比度是按调色板数值算的，控件若没绑定该资源，对比度全绿而界面仍是错的。**产品文案规则（术语表、取消类提示须写明 Esc、中文省略号）已从本文件迁到 `tools/lint-copy.sh`**（见 §4.5），因为文案规则没有运行时可观察，不该伪装成 xunit 用例。 |
| 桌面启动冒烟 | `tools/smoke_desktop.sh` | 进程退出码 0、stderr 有 `startup.firstFrame` 与 `startup.listReady` 打点、无未处理异常/异常栈行、数据目录有 `ledger/log.jsonseq`/`.lock`/`.lock.pid`、`attachments/` 存在、锁已释放、临时目录被清理 | 证明「窗口真的画出来了并且应用能干净退出」。**firstFrame 是打点，不是画面**：一个全黑、错位、只有骨架的窗口同样会打出 firstFrame。它还依赖 `DISPLAY`（默认 `:0`），本机无 X 时只能靠 CI 的 ubuntu runner。**已知环境边界（2026-10-22）**：macOS 上若图形会话起不了渲染定时器，进程会以 `Avalonia.Native was not able to start the RenderTimer. Native error code is: -6661` 退出，四条启动断言全红。这不是产品缺陷——用未含当轮改动的 HEAD 版脚本复跑同样复现；要验这条只能在能起渲染定时器的真实桌面会话（或 CI 的 Linux runner）上跑。 |
| 发布产物 PWA 冒烟 | `tools/smoke_published.sh` + `tools/smoke_published_pwa.js` | headless Chrome（`puppeteer-core`，根 `package.json` 的 devDependency）打开发布产物：画布出现、DPR=2（像素= CSS×2）、画布非纯色（64×64 采样**至少 3 种颜色**）、CDP `Input.insertText` 走文本上屏桥、认证态下草稿 800 ms 自动落 IndexedDB、重开页面草稿恢复、**三类弹出层（tooltip / 上下文菜单 / 输入框右键菜单）出现且文字不是豆腐块**、无页面异常 | 「画布非纯色」= **画布不是一块纯色**。一个只有背景色加一条错误提示的界面照样通过。交互只覆盖「把字塞进输入框」，不覆盖发送、审批、删除、搜索、主题切换。弹出层判据是**像素**（`avalonia.js` 没有可用的 DOM 无障碍树，弹出层也没有 DOM 节点）：开/关两帧求差得外接矩形，再在矩形里统计笔画。识别豆腐块靠的是**形状多样性**（`distinctShapes / 有笔画行数`）而不是笔画密度——`.notdef` 空心方框每行同样有好几段笔画，密度与真字形接近（实测豆腐块 `meanRuns=11.33` vs 正文 `10.82`），只有形状多样性会掉（豆腐块 `0.06`，真字形 `0.63~1.00`，阈值 `0.3`）。`meanRuns` 比值仍保留作「笔画密度不异常」的第二道检查。它量的是「字形有没有渲染出来」，不保证排版正确、不保证菜单项文案齐全。 |
| 性能采样 | `tools/perf_desktop.sh`（N 次启动读 `StartupTrace` 打点）、`tools/perf_pwa.js` | 冷启动 `firstFrame` / `listReady` 的毫秒数 | 脚本注释写「跑 N 次取中位数」，**实现只是逐次 echo，没有取中位数、没有阈值、没有进 CI**。性能是观测工具，不是门禁。 |
| 组件画廊（人工工具） | `compose/Gallery` + `tools/gallery.sh` | `--list` 列出 **40** 条状态条目；`--window` 开真实桌面窗口；`--render <条目名> <输出.png>` 出一个 **720×900** 的 PNG（实测 `MessageCardView.toolcall`）。**它不是门禁**：没有基线、没有 diff，渲染变了 CI 不会红。 |
| 金样 | `compose/*/tests/golden/*.txt`、`libs/ThemeFonts/tests/golden/fonts.sha256` | 协议字节、错误文案、公式 HTML 与**公式控件的布局量**（`compose/MathFigure/tests/Probe.fs` 用 `RenderTargetBitmap` 量 Bounds / 基线 / 留白 / 读屏名；**整块像素哈希已从金样移除**——Skia 在 macOS 的 CoreText 与 Linux 的 FreeType 上光栅化结果不同，跨平台不稳定，故只做同一进程内的前后像素对比） | 公式渲染一处做的是**布局级**锁定，**不再**是跨平台像素级锁定。根 AGENTS.md 已明令「不再新增与旧仓库死代码比对的金样测试」，所以这里**没有**可复用的全界面视觉回归基线。 |

### 1.3 唯一可复用的技术前提

`compose/MathFigure/tests/Probe.fs` 已经示范了本仓库能做的最强视觉断言：headless 窗口 → `RenderTargetBitmap(PixelSize(w,h), Vector(96,96))` → `CopyPixels` → 像素哈希（**只用于同一进程内的前后对比**；整块像素哈希**不进金样**，因为 Skia 在 macOS 的 CoreText 与 Linux 的 FreeType 上光栅化结果不同，跨平台不稳定——见 `compose/MathFigure/AGENTS.md` LEMMA 1）。**像素回归所需的渲染与取样能力已经在仓库里，只是只用在了一个组件上。**

---

## 2. 缺口清单

每条给出：缺什么 → 它本该抓住什么界面缺陷 → 今天为什么抓不住。

### (a) 没有覆盖真实控件树的视觉回归 / 截图 diff

- **现状**：没有任何一条测试比较整棵控件树的渲染结果。`RenderTargetBitmap` 全仓只出现在 `compose/MathFigure/tests/Probe.fs`。
- **会漏掉的缺陷**：控件重叠、遮挡、留白塌陷、边框错位、圆角/阴影丢失、文本被裁成「…」、深浅主题下某块颜色没跟上、深色模式整块反色、缩放（HiDPI）下布局崩掉。
- **今天为什么漏**：headless 断言的是 `Bounds`/`IsEffectivelyVisible`/控件树结构，这些数值**在控件画歪时依然正确**。`smoke_published_pwa.js` 的「非纯色」判定更弱：3 种颜色就通过。

### (b) 组件画廊（component gallery / storybook 等价物）—— **已建成**

- **现状（2026-10-21）**：**已经有了**。`compose/Gallery` 是一个组合者，把各控件库的**状态矩阵**显式列成条目，每条由 `Workspace.init ()` + `Workspace.update` 的消息序列跑出来、再经 `Workspace.project` 投影（不手搓视图状态记录）。
- **命令**（`tools/gallery.sh` 是命令行外壳，渲染在 Avalonia headless / Skia 下进行，不连主机、不读本机文件、不写仓库里的任何东西）：
  ```
  bash tools/gallery.sh --list                              # 列出全部条目（名字 / 控件库 / 说明）
  bash tools/gallery.sh --window                             # 打开真实桌面窗口看这些状态
  bash tools/gallery.sh --render MessageCardView.toolcall /tmp/gallery.png   # 渲染单个状态成 PNG
  ```
  `--list` 现有 **40** 条；`--render MessageCardView.toolcall /tmp/gallery.png` 产出 **720×900** 的 PNG（`file` 报 `PNG image data, 720 x 900`）。
- **画廊解决了什么**：控件的某个状态不再只能通过「整条用户旅程」触达——它现在可以被单独列出来、单独渲染、单独看。
- **画廊没有解决什么（不要误当门禁）**：
  - **它不是像素回归门禁**。`--render` 出一张 PNG 供人看，但**没有基线、没有 diff、没有 CI 判定**。谁改了渲染结果，CI 不会红。
  - **它不是无障碍证明**。条目渲染出来不等于无障碍树正确（`AutomationProperties` 另说，见 §2.(e)）。
  - **它的夹具仍在修正中，但附件那条已经收口**：状态树此前给不出附件（`ComposerUserMsg.Attach` / `.RemoveAttachment` 与 `InternalMsg.UploadProgress` / `.UploadFinished` / `.UploadFailed` 都没有处理分支，而且 `Send` **只把 `text` 放进消息、把附件静默丢掉**——那是真 bug）。现在 `libs/WorkspaceState/Update.fs` 补齐了这些处理分支，并按 `compose/Outbox` 的 `MessageOutbox.BuildMessage` 同一份形状构造带附件的消息；画廊测试**现在 18 条全过**（实测 `total: 18, failed: 0`）。
  - **已收口**：侧栏多选——多选模式与选中集合是 `libs/WorkspaceState` 的 `Chrome.BatchMode` / `Chrome.SelectedRowIds`，经 `SidebarVS.BatchMode` / `SidebarVS.SelectedRowIds` 投影给侧栏；`SidebarDesk` 改从投影读，只发意图（`SidebarDeskActions` 的 `enterSelection` / `toggleSelected` / `exitSelection` / `toggleSelectAllVisible`），`libs/BatchBar/SidebarSelection.fs` 不再记账。
  - **错误卡也已收成单一归属**：生成错误是 `ChatVS.Error` 字段，由 `Workspace.project` 从 `RunState.Idle (Some err)` 投影；宿主改从投影取，不再自己读 `RunState`；`ChatPhase.Error` 那个无生产者的死分支已删除。详见 `compose/Gallery/AGENTS.md`《已知缺口与各自的归属》。
  - **已查清不改的一条**：对话框的「打开」状态没有投影生产者，但**这是「投影是壳、真实控件在桌内」的分工**，不是两份互相打架的事实——`compose/DialogsDesk/DialogsDesk.fs` 直接 `overlay.ShowDialog(...)` 造真实控件，`compose/OverlayDesk/OverlayDesk.fs` 的 `OverlayVS.Dialog` 分支只设 `currentOverlayPhase`、不造控件。
- **已知缺口**（记在 `compose/Gallery/AGENTS.md`《已知缺口与各自的归属》里）：对话框的「打开」状态不在状态树里——这是「投影是壳、真实控件在桌内」的分工，不改。
- **会漏掉的缺陷**：某个控件的**边缘状态**从未被任何测试构造过——比如 `ComposerBox` 在「有附件 + 生成中 + 断线」三态同时成立时的样子，或者 `ModelSwitchPanel` 的空目录态。这类缺陷因为「没人构造过这个状态」而根本不进入测试面。
- **今天为什么漏**：旅程测试是**打包**的：一个旅程失败时，你无法单独把那个控件的某个状态拎出来看；控件的多种状态散落在**几十个测试文件**里，组合爆炸时没人能系统枚举。
- **额外损失**：想肉眼审一个控件的十个状态，只能启动整 app、连主机、走真实交互——这是 §2.(c) 的成本，也是「界面缺陷靠人肉发现」的直接原因。
- **现状（2026-10-21）**：画廊（§2.(b)）已经补上了「单独看一个状态」这一环；`./x gallery` 式的快速回路**仍未建**（`./x` 里没有 `dev` / `watch` / `serve` 子命令），改一个控件库后仍要走 `pack → 构建入口 → 起 app`。

### (c) 没有记录在案的「重建 + 运行」快速回路

- **现状**：`./x` 只有 `test / check / hygiene / lint / pack / compose / consume / app / sln / new-widget / all` 这些子命令，**没有 `dev` / `watch` / `serve`**。
- **实测成本（来自仓库自身的记录）**：
  - `run-desktop.sh` 注释：全量 `./x pack` **实测 15 分钟**，入口构建 **实测 2 分半**（`run-desktop.sh` 头部）；`app/Desktop` + `app/Pwa` 两入口 `dotnet build` **共 12.7 秒**（Desktop 4.4 / Pwa 7.6，见 `.github/workflows/ci.yml` 的 `app` 作业注释）。
  - `./x app`（`x` 脚本的 `app()`）每次都会 `rm -rf artifacts/packages/wanxiang2.* app/*/obj` 后重新构建两个入口——**干净消费**，代价 12.7 秒起步。
  - `./x all` 是 check → hygiene → lint → 逐库 test → pack → compose → consume 的全链，**不适合迭代单个控件**（hygiene 与 lint 是秒级的源码门禁，但整条链仍以 pack 为重）。
- **于是「迭代一个控件」的实际路径**：改某个控件库自己的源文件 → 造包（`./x pack`，受依赖顺序影响，分钟级）→ 构建入口 → 起 app → 人工走到那个状态 → 看一眼。**没有文档写清这条路径，每一步都靠人摸索。**
- **会漏掉的缺陷**：迭代慢 → 开发者跳过验证 → 缺陷带进主干。形式化一点：**缺陷逃逸率的直接成因是「看一眼」的门槛高于「跑一遍」的门槛**。

### (d) PWA 没有浏览器侧的真实交互 e2e

- **现状**：`tools/smoke_published_pwa.js` 的全部交互是「点输入框 → `Input.insertText` 塞两段文本 → 读 `.avalonia-input-element` 的 value → 截图」。**没有发送、没有流式回复、没有工具审批、没有删除撤销、没有搜索、没有主题切换。**
- **会漏掉的缺陷**：发送按钮不响应、Enter 发送判定错、流式增量不落屏、审批卡不出现、删除后撤销失效、焦点在 body 时快捷键不响应（这正是 `docs/ux/r28-pwa-parity.md` 里旧仓库修过的第 27 轮那类「桌面正常、PWA 静默失效」问题——**今天仍然没有任何自动化能抓住它**）。
- **今天为什么漏**：canvas 里的 Avalonia 界面无法用 DOM 选择器定位；现有脚本只验证「桥通了」，不验证「桥通了之后界面做对了事」。

### (e) 没有自动化的无障碍树断言

- **现状**：`AutomationProperties` 相关断言是**逐点手写**的零散断言（`compose/MarkdownView/tests/Tests.fs` 约 15 处、`compose/ComposerDesk/tests/Tests.fs` 约 5 处、`libs/ApprovalPanel`、`compose/MessageCardView` 等），**没有一条「遍历整棵控件树 → 每个可交互控件都有非空 Name 与正确 ControlType」的通用断言**。
- **两条文案扫描原本恒真/失明，已迁到 `tools/lint-copy.sh` 并把范围补全**（§4.5）：`所有图标按钮调用都带非空名称` 的正则匹配 `.IconButton(`，而本仓库的 API 是小写 `iconButton`——在**全仓生产 `.fs` 文件**上命中 0 次（扫遍 `libs` 与 `compose` 的每一个生产文件），这条测试永远不会失败；`取消按钮的提示文案都写明 Esc` 的扫描范围只覆盖 `compose` + `app`，而全部取消类控件住在 `libs`——同样永远通过。**这两条本质是产品文案规则，不是源码接线规则**，所以修法不是删掉，而是**移出测试套件、变成 lint 规则**（`./x lint`），并把覆盖范围扩到 `libs` + `compose` + `app`。
- **会漏掉的缺陷**：新增控件忘记设 `AutomationProperties.Name`（屏幕阅读器读作「按钮」）、把 `TextBlock` 当按钮用（角色错）、`AccessibilityView` 未设 Raw（内容被朗读两遍）、Tab 序与视觉序错位。
- **今天为什么漏**：`docs/ux/a11y-audit.md` §4 写明「旧仓库当时有 220 余处 `AutomationProperties` 使用」——**这个总数本身说明覆盖面远超现有零散断言；今天的机械检查只剩逐点手写断言，全树遍历断言（§3 G5）仍待建。**

### (f) 格式门禁已建；lint 门禁只覆盖文案

- **实测仓库现状（2026-10-21 更新）**：
  - **`.editorconfig` 已存在**（仓库根），写明 F# 格式规则：缩进 4、行宽 120、LF、文件末换行，以及 Fantomas 8 的多行括号风格（`aligned`）。
  - **Fantomas 已钉版**：`.config/dotnet-tools.json` 钉住本地工具 `fantomas 8.0.10`（`rollForward: false`），`dotnet tool restore` 即可复现同一版本。
  - **`.fantomasignore` 排除生成物**（`bin/`、`obj/`、`artifacts/`、`node_modules/`），格式门禁不会误报构建产物。
  - **门禁已接进 `./x check`**：`check()` 末尾跑格式门禁，未格式化的文件逐个打印 `FAIL format` 行并让 `./x check` 以非零退出。
  - **格式检查只跑目标文件（2026-10-22 所有者规则）**：`dotnet fantomas --check .` 与 `dotnet fantomas .`（全仓）**一律禁止**。目标集合 = 显式文件列表 `./x format-check <文件…>`，或没给列表时的**改动集合**：相对基准提交改动的 `.fs`（基准默认取 `origin/main` 的合并基点；CI 用 `WX_FORMAT_BASE` 传 PR base / push 的上一个提交；detached HEAD 或缺 `origin/main` 时退 `HEAD~1`）∪ 暂存区/工作区/未跟踪的 `.fs`，再按 `.fantomasignore` 过滤。集合为空时打印「无文件可查」并通过（退出码 0）。
  - **一次性重排用 `./x format <文件…>`**（不带参数时重排上述改动集合，同样不是全仓）。
  - CI 侧：`./x check` 是 CI 的 `check` 作业，格式门禁随它一起跑；CI 侧用 `fetch-depth: 0` + 合并基点/上一个提交算改动集合，**不跑全仓**。
  - 仍**没有**的：F# 语义层面的 lint（未使用的绑定、模式匹配写错、可变状态误用这类编译器抓不到的错误）——`./x lint` 管的是**界面文案**（禁用同义词 / 取消类提示须写明 Esc / 中文省略号 / PWA 壳术语），不是代码格式。
  - 编译级约束仍在：`Directory.Build.props` 的 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` 与 `<WarnOn>3390;1182</WarnOn>`。
- **还剩的缺陷**：格式漂移已经拦住，但「F# 语义 lint」这一类仍然只靠人眼。
- **状态**：格式部分**已解除**（见 §3 G6）；语义 lint 未建。

### (g) `docs/ux/a11y-audit.md` 自身记录的「待实测」项

该文档 §1 明写：「未做：真机屏幕阅读器（NVDA / VoiceOver / TalkBack）实测、PWA 的 Lighthouse 跑分、真实 Tab 轨迹录像。以下凡写『待实测』的条目都属此类。」

逐条列出仍未闭合的项：

| # | 待实测项 | 出处 | 今天的状态 |
| --- | --- | --- | --- |
| 1 | 真机屏幕阅读器（NVDA / VoiceOver / TalkBack）实测 | `docs/ux/a11y-audit.md` §1、§7 | 未做。仓库内没有任何读屏器实测记录；自动化只能覆盖 `AutomationProperties`，覆盖不了读屏器实际播报。 |
| 2 | PWA 的 Lighthouse 跑分 | §1 | 未做。CI 无 Lighthouse 步骤。 |
| 3 | 真实 Tab 轨迹录像 | §1 | 未做。焦点流转只由 `libs/FocusOrigin/tests`、`compose/OverlayDesk`、`compose/DialogsDesk` 的局部断言覆盖，没有一条「从 Tab 到 Tab 走完整棵树」的轨迹断言。 |
| 4 | 全局快捷键与**屏幕阅读器自身快捷键**的冲突 | §3 | 未做。 |
| 5 | 全局快捷键与**浏览器（PWA）保留键**的冲突（Ctrl+W、Ctrl+N 在浏览器标签页内被浏览器抢占） | §3 | 未做。`docs/ux/r28-pwa-parity.md` 把「焦点在 body 时按键进不了应用」记为旧仓库已修（决策 454），但**没有回归断言**，删掉这段接线不会有任何红灯。 |
| 6 | 快捷键书写格式一致性（「Ctrl / ⌘ + 键」）的机器固化 | §6 | §6 明确写「**未固化为测试**」；`A11yRegression.fs` 的术语用例只覆盖禁用同义词，不覆盖快捷键格式。 |
| 7 | 「禁止几何动画」等价断言 | §5 | §5 明确写「**没有**等价于『禁止几何动画』的断言」。 |
| 8 | 遮罩/浮层的 `prefers-reduced-motion` 是否进入 Avalonia 画布 | §5 | 已查明读不到（`IPlatformSettings` 无 `ContrastPreference` 语义），当前只由 `libs/UiPrefs.reduceMotion`（默认 false）驱动——**默认开启动效**，且无断言守住这个默认值。 |
| 9 | 屏幕级待办表里的「屏幕阅读器实测 | 全部」一行 | §7 | 未做。 |

### (h) 没有「界面改动完成定义（Definition of Done）」清单

- **现状（2026-10-21）**：仓库里原本**没有**这份清单（`grep -rniE 'definition of done|完成定义|交付定义|DoD'` 在全仓 `.md`（排除 `docs/archive`）命中 **0**），现在由本文 **§5** 补上：一份由**改界面的工程师自己**走完并留证的端到端清单。根 `AGENTS.md` 的 SPEC 7 证明栏也已从 `[TODO]` 改写为**部分证据**并点名仍未走到的转换。
- **会漏掉的缺陷**：提交一个「视觉上明显不对」的界面改动，因为「测试全绿」而被接受——**因为绿灯与界面无关**（§1.2）。没有 DoD，就没有人需要回答「这个改动的界面正确性由哪条证据担保」。
- **今天为什么漏**：DoD 不是一个 bug，是一条**流程缺失**。`compose/EntryShells/AGENTS.md` SPEC 2 里那句「**真实界面验收要人看，仓库内未自动化**」是这句缺失最直白的自白。

---

## 3. 门禁与工具（已建的标「已建成」，未建的仍是提案）

约束：只用现有工具链能建成的东西——`Avalonia.Headless` 的 `HeadlessUnitTestSession`、Avalonia 的 `RenderTargetBitmap`、根 `package.json` 里已有的 `puppeteer-core`、`./x` 子命令、CI 的矩阵作业。

按**价值 / 成本**排序。成本一栏是本机实测量级与一次实现的工作量。

### G1 — 断言卫生门禁（先建这个）

- **跑什么**：一个 CI 步骤（不需要新工具，`grep` + 一个 F# 或 shell 脚本即可）：
  1. 禁止 `Assert.True(true)` / `Assert.False(false)` / `|| true` / `Assert.True(<常量>)` 出现在任何 `compose/*/tests`、`libs/*/tests`、`app/*/tests` 里；
  2. 每个 `[<Fact>]` 的函数体必须至少出现一次 `Assert.` 或 `failwith`（禁止「跑完什么都不说」的事实）；
  3. 每个 `[<Fact>]` 必须至少有一条**正向**断言（不能只靠 `Assert.Fail` 分支）。
- **成本**：已建成。门禁本身只需源码、秒级完成（实测见 §4.5）；一次性把 §4 列出的那批清理掉的工作量不写数字——没有可核对的测量就不写。
- **拦什么**：所有「绿灯但什么都没验」的提交。
- **怎么响**：CI 里 `::error::` 注解逐行列出 `file:line`，非零退出。

### G2 — 组件画廊（component gallery）—— **已建成**

> **状态：已建成**（2026-10-21）。命令见下；条目与已知缺口见 `compose/Gallery/AGENTS.md`。
> **它是「能单独看一个状态」的工具，不是门禁**：`--render` 出的 PNG 供人看，没有基线、没有 diff、没有 CI 判定。把它当视觉回归门禁是误用。

- **跑什么**：`compose/Gallery` 组合者 + `tools/gallery.sh` 外壳：
  ```
  bash tools/gallery.sh --list                              # 40 条条目
  bash tools/gallery.sh --window                             # 真实桌面窗口
  bash tools/gallery.sh --render <条目名> <输出.png>          # 单个状态 → PNG（720×900）
  ```
- **状态来源**：每条的视图状态都由 `Workspace.init ()` + `Workspace.update` 的消息序列跑出来、再经 `Workspace.project` 投影；**没有一条手搓视图状态记录**（手搓是画廊与真实界面漂移的唯一途径）。**两条例外走真实控件入口**（不是手搓投影，是那条控件真实的入口）：`MessageCardView.error` 走卡片组的 `ErrorCard`，`SidebarDesk.batch` 走侧栏自己的 `EnterSelection`。需要宿主的线（网络、剪贴板、文件系统、计时器）由显式的无副作用假端口提供，**假端口只影响「点了之后会发生什么」，不影响「界面长什么样」**。
- **成本**：已建成（实现工作量不写在这里——没有可核对的测量就不写数字）。
- **拦什么**：§2.(b) 枚举的「某个状态从没被构造过」这一整类缺陷——画廊把状态显式列出来，缺哪条一眼看得见。
- **怎么响**：画廊自己的测试在 headless 下逐条渲染并断言（渲染不抛、控件树非空、状态符合当前事实）。**但它不是 CI 门禁**：条目缺失不会让 CI 红，渲染变化也不会让 CI 红。

### G3 — 视觉回归（像素 diff）

- **跑什么**：以 G2 的**状态矩阵**为输入（40 条，`tools/gallery.sh --list`），用**已有的** `RenderTargetBitmap` 手法（照抄 `compose/MathFigure/tests/Probe.fs`）对每个状态渲染，基线存在画廊组合者的 tests 目录下的 golden 子目录里。**用 PNG 做逐像素 diff，不建议用像素哈希**：`compose/MathFigure/AGENTS.md` LEMMA 1 已记下，Skia 在 macOS（CoreText）与 Linux（FreeType）上光栅化结果不同，整块像素哈希**跨平台不稳定**（金样里那部分已被移除，见 §1.2）。深浅两套主题都做。
- **与画廊的关系**：画廊**已经能出 PNG**（`--render` 出 720×900），但**没有基线也没有 diff**——所以 G3 不是「让画廊出图」，而是「给这些图加基线与判定」。G3 未建之前，画廊出的 PNG 只供人看。
- **成本**：未建，不给数字（没有可核对的测量就不写）。要预先说清的是：基线的**审核**成本随控件数线性增长，所以必须配「基线变更需附截图」的评审规则，否则基线会变成无人看的锁。
- **拦什么**：§2.(a) 的全部布局/配色/遮挡/裁切缺陷，以及主题切换后样式没跟上的问题。
- **怎么响**：基线 PNG 与本次渲染逐像素不一致 → 打印 `状态名: 基线 <路径> 实际 <路径> 差异 <区域>`，CI 红；基线文件缺失 → 明确报错「这是新状态，请生成基线并人工核对截图」，不允许静默通过。

### G4 — 浏览器侧 e2e（PWA 真实交互）

- **跑什么**：扩展 `tools/smoke_published_pwa.js`（`puppeteer-core` 已在 devDependency）：在现有「画布非纯色 + 文本上屏桥 + 草稿落盘」之上，加真实交互序列——发送一条消息 → 等流式增量 → 触发一次工具审批并响应 → 批量删除并撤销 → 搜索 → 切主题；每步截图存 `artifacts/smoke/shots/`。
- **成本**：未建，不给数字。已知参照：`tools/smoke_published.sh` 现有那套本机实测 **33 秒**（`docs/ux/r28-pwa-parity.md` 记录），加真实交互后会更久，具体数字等实现出来实测再写。
- **拦什么**：§2.(d) 的全部「桌面正常、PWA 静默失效」缺陷，以及 `docs/ux/r28-pwa-parity.md` 表里那批「已打点但 CI 未断言」的行为（剪贴板、文件选择/保存、右键菜单、拖放）。
- **怎么响**：现有脚本已是 `check(name, ok, detail)` + `PASS`/`FAIL` 计数 + 非零退出；沿用即可，CI 里把 `FAIL` 行写成 `::error::` 注解（现有 `published-smoke` 作业已经这么做了）。

### G5 — 自动化无障碍树断言

- **跑什么**：两部分。
  1. **通用遍历断言**（唯一要做的一件事）：在 headless 里渲染一个控件 → 递归遍历 `GetVisualDescendants()` → 对每个 `Focusable` 或可点击控件断言 `AutomationProperties.GetName` 非空、且（若是自绘/非原生控件）`ControlTypeOverride` 已设。**不要**用「读 .fs 源码做正则扫描」的方式实现——那种扫描器会随代码形状漂移而恒真（§4.5 的两条旧扫描就是这么坏的），而且它验的是「源码长什么样」，不是「控件对外暴露了什么」。
  2. **控件自己的提示文案由控件自己的测试断言**（例如 `libs/ConfirmPane/tests` 断言取消键的 tooltip 含 `Esc`），不靠跨库的措辞扫描器。
- **成本**：未建，不给数字。门禁只需源码、纯 headless，不依赖构建产物。
- **拦什么**：§2.(e) 的无名称控件、角色错、重复朗读。
- **怎么响**：遍历断言失败时打印「控件类型 + 所在路径 + 缺什么」，CI 红。

### G6 — 格式 / lint 门禁 —— **格式部分已建**

- **跑什么（已建）**：仓库根 `.editorconfig` 写明 F# 格式规则（Fantomas 8 的显式默认值：缩进 4、行宽 120、LF、文件末换行、`fsharp_multiline_bracket_style = aligned`）；`.config/dotnet-tools.json` 把本地工具 **Fantomas 8.0.10** 钉死（`rollForward: false`）；`.fantomasignore` 挡掉 `bin/` `obj/` `artifacts/` `node_modules/`；`./x check` 的 `check()` 末尾跑**目标文件**的格式门禁。
- **一次性重排**：`./x format <文件…>`（不给参数时重排改动集合）。
- **只跑目标文件（2026-10-22 所有者规则，硬约束）**：`dotnet fantomas --check .` 与 `dotnet fantomas .`（全仓）**禁止**。目标集合 = 显式文件列表 `./x format-check <文件…>`，或没给列表时的**改动集合**：相对基准提交改动的 `.fs`（基准默认 `origin/main` 的合并基点；CI 用 `WX_FORMAT_BASE` 传 PR base / push 的上一个提交；detached HEAD 或缺 `origin/main` 时退 `HEAD~1`）∪ 暂存区/工作区/未跟踪的 `.fs`，再按 `.fantomasignore` 过滤；集合为空即打印「无文件可查」并通过。
- **成本（实测）**：全仓 895 个 `.fs` 一次性重排曾动了 **816 个文件、+54644 / −19854** 行（提交 `a4727a3`，纯排版、行为不变）。门禁本身只需源码、不依赖构建产物；**改成只查改动文件后，全仓 `--check` 那约 60–83 秒的开销不再每次支付**（干净分支上 `./x check` 的格式部分直接跳过）。
- **拦什么**：格式漂移导致的 diff 噪声（review 疲劳 → 漏审）。
- **怎么响（实测）**：`fantomas --check` 退出码 99 → `./x check` 打印 `FAIL format: F# files are not formatted (run: ./x format <文件…>)` 并逐个列出 `! <路径> needs formatting.`，`./x check` 退出码 **1**。
- **仍未建**：**F# 语义层面的 lint**（未使用的绑定、模式匹配写错、可变状态误用这类编译器抓不到的错误）。`./x lint` 管的是界面文案，不是代码格式。

### G7 — 界面改动完成定义（DoD）清单

- **跑什么**：在 `docs/` 下写一份可勾选的清单（本次交付即包含本文档，可直接抽出 §3 做清单），并要求每个 UI 改动的 PR 描述里贴出「哪几条门禁的哪一次运行」作为证据。
- **成本**：清单本身是文档，运行成本 0；它真正的成本是**评审习惯**（每次界面改动都留下证据）。
- **拦什么**：§2.(h)——「测试全绿所以这个界面改动可以合」。
- **怎么响**：清单本身不自动失败，但它把「没有证据」的 PR 变成**评审时必然被打回**的状态；这是唯一能长期维持其余门禁的执行层。

### 已建与未建

| | 内容 | 状态 |
| --- | --- | --- |
| G1 | 断言卫生门禁（`./x hygiene`） | **已建** |
| G2 | 组件画廊（`compose/Gallery` + `tools/gallery.sh`） | **已建**（40 条条目） |
| G5 的一半 | 产品文案规则（`./x lint`） | **已建**（从测试套件迁出，范围补到 libs+compose+app） |
| G7 | 界面改动交付清单（本文 §5） | **已建** |
| G6 的格式部分 | F# 格式门禁（`./x check` 里的**目标文件** `dotnet fantomas --check`） | **已建**（Fantomas 8.0.10 钉版 + `.editorconfig`；全仓已重排；**全仓运行被禁止**，只查改动文件） |
| G4 | 浏览器侧真实交互 e2e | 未建 |
| G3 | 像素基线门禁 | 未建（画廊能出 PNG，但无基线无 diff） |
| G5 剩下 | 无障碍树整树遍历断言 | 未建 |
| G6 剩下 | F# **语义** lint（未使用绑定、模式匹配写错、可变状态误用） | 未建（`./x lint` 管的是文案，不是代码格式） |

**为什么 G1 排在最前**（当时只有它可建，且顺序不能反）：
  1. 成本最低（只需源码、秒级完成），立刻让「绿灯」重新有意义；
  2. **G2/G3/G4/G5 全部建在现有测试之上**——如果底层的断言是恒真的，那么画廊、像素基线、e2e、无障碍遍历都会继承这份不可信。**先修断言，再修覆盖面，顺序不能反。**
  3. 它直接处理本次审计最硬的发现：§4 列出的恒真与空断言。

**G2（组件画廊）已建成**（40 条条目，见 §2.(b)）：它是「快回路」的一部分，也是 G3（像素基线）的输入来源；但 G3 仍未建——**画廊出图不等于像素门禁**。

---

## 4. 缺陷复现线索：空断言 / 恒真断言清单

分三层：**A. 完全空洞**（无论行为如何都通过）、**B. 弱断言**（只验负向或只在一条路径上验，关键正向结果无人断言）、**C. 事实级空转**（函数体没有任何断言）。

### 4.1 A 层：完全空洞的断言（恒真）

> 位置列是**审计当时**的行号；处置见 §4.5（这些用例已改写，行号已变）。

| # | 位置 | 形态 | 为什么什么都没证明 |
| --- | --- | --- | --- |
| A4 | `compose/Theme/tests/A11yRegression.fs`（原 `:123`） | 正则 `\.IconButton\(\s*…(""|null)\s*,` 扫 `libs` + `compose` 的生产 `.fs` | 本仓库的图标按钮 API 是**小写** `iconButton`（`libs/DevicesPane/Ui.fs:110`、`compose/MarkdownView/MarkdownView.fs:83`、`compose/MessageCardView/Looks.fs:41`）。在 `libs` 与 `compose` 的**每一个生产文件**上**正则命中 0 次** → `bad` 恒为空 → `Assert.True(bad.IsEmpty, …)` **永远通过**。**已迁为 `tools/lint-copy.sh` 的口径（覆盖 libs+compose+app），并在 `./x lint` 与 CI hygiene 作业里执行**——但它是文案规则，不是「控件对外暴露了什么」的断言；控件的可访问名称仍应由控件自己的 headless 测试断言。 |
| A5 | `compose/Theme/tests/A11yRegression.fs`（原 `:194`） | 扫「`"取消并关闭` 开头且不含 `Esc`」的字符串，范围只覆盖 `compose` + `app` | 全仓 14 处「取消并关闭…」字符串（7 个取消按钮 × `SetHelpText` + `SetTip`）**全部住在 `libs`**（`libs/ConfirmPane/*`、`libs/DevicesPane/*`、`libs/ExportSheet/*`），而扫描范围不含 `libs` → 断言**永远在空集合上运行，永远通过**。**已迁为 `tools/lint-copy.sh` 的 C2 规则（覆盖 libs+compose+app），并在 `./x lint` 与 CI hygiene 作业里执行。** |
| A1 | `compose/AppShell/tests/ComposerTests.fs`（旧 `:41`，现 `:38-49`） | `\| _ -> false \|\| true)` | 谓词恒为 `true`。`Assert.Contains(rig.Fake.RecordedEffects, fun eff -> ...)` 退化成「RecordedEffects 列表非空」——**它对 `Effect.Focus` 存在与否一字未验**。而这个断言所在的测试名叫「附件上传按开始-分块-完成发事件，图片另走缩略图端口」，恰恰声称在验上传事件序列。**已改为** `Assert.Equal(1, thumbnails.Length)`（`:41`）+ 缩略图字节逐字节相等（`:43`）+ 非图片不走该端口（`:49`）。 |
| A2 | `compose/AppShell/tests/ComposerTests.fs`（旧 `:68`，现 `:81-83`） | `\| ConnPhase.Connecting _ -> Assert.True(true)` | 分支体是恒真断言。该分支里的 `Connecting` 与否**没有任何语义**。**已改为** `Assert.Equal(1, attempt)`（`:82`），其余状态 `Assert.Fail`（`:83`）。 |
| A3 | `compose/AppShell/tests/ComposerTests.fs`（旧 `:69`，现 `:83`） | `\| _ -> Assert.True(true)` | 同上，且这是**兜底分支**——无论 `Conn` 落到哪个状态（包括完全不合理的 `Authed`），测试都通过。整个测试「断开连接：清空状态并转入断开」**不验证任何断开语义**。**已改为** `Assert.Fail`（`:83`）。 |

### 4.2 B 层：弱断言（关键正向结果无人断言）

| # | 位置 | 形态 | 为什么弱 |
| --- | --- | --- | --- |
| B1 | `compose/AppShell/tests/ComposerTests.fs`（旧 `:111-113`，现 `:141-143`） | `match vs.Composer.Send.State with \| SendButtonState.GeneratingStop -> Assert.Fail "…" \| _ -> ()` | **旅程 2「引导流程配置模型」的唯一断言是负向的**：只证明「状态不是 GeneratingStop」。测试名声称验的是「引导流程配置模型」，但**没有任何断言检查目录是否装载、预设是否选中、引导卡是否投影**。且负向断言只能捕捉「恰好是 GeneratingStop」这一种错误值。**已改为**目录就绪且 `providers` 为空（`:151`）、引导事实为空（`:154-155`）、输入区禁用且原因正确（`:157-158`）、空态是 `NoConversation`（`:160-162`）。 |
| B2 | `compose/AppShell/tests/ComposerTests.fs`（旧 `:238-240`，现 `:424-453`） | 同上形态 | 旅程 5 的收尾断言只验「不是 GeneratingStop」；错误状态若是别的值（如 `Sending`）照样通过。**已改为** `Assert.Equal(SendButtonState.Idle, …)`（`:429`，负向 match 已删除）+ `RunState.Idle(Some err)` 带 kind/message/retryable（`:432-441`）+ `GenerationCancel` 发给那一个对话与代号（`:450-453`）。 |
| B3 | `compose/AppShell/tests/ComposerTests.fs`（旧 `:275-279`，现 `:482-497`） | `match toastUndoAction with \| Some actionId -> … \| None -> ()` | 「撤销删除」这一步的断言**只在 `UndoAction` 是 `Some` 时才执行**。如果撤销入口消失（`None`），测试静默通过。上面 `Assert.True(rig.Shell.Model.Toasts.Length > 0)` 只证明有 toast，不证明 toast 带撤销动作。**这正是「批量删除恢复」旅程最该守住的那一环。** **已改为** `None -> Assert.Fail`（`:497`）。 |
| B4 | `compose/WsClient/tests/ClientStateAndWsClientTests.fs:802-840` | `with \| :? OperationCanceledException as oce when oce.Message.Contains "superseded" -> caughtSuperseded <- true \| _ -> ()` | `caughtSuperseded` 被赋值后**从未被断言**；非匹配异常被 `\| _ -> ()` 静默吞掉。最后那句注释「Either it threw superseded or was canceled cleanly」把**两种行为都当成通过**——测试名「generation check」想验的「连接代次检查生效」实际上没被断言（只断言了 `ConnectionGeneration >= 2`）。 |
| B5 | `libs/ConfigFeedback/tests/DownloadBuffers.Tests.fs:45-57` | `match oldBuffers.Append(sha256, bytes) with \| DownloadChunkResult.Capped when not downloadCappedNotified -> … \| _ -> ()` + `with _ -> ()` | 旧实现摘录里，**非 Capped 结果与所有异常都被静默吞掉**。`oldStep` 的返回值只反映「是否 toast 过」，因此「坏 base64 被忽略」「第二次触顶不再 toast」等语义**只由新旧两份实现的相等性担保**，而 `newStep` 那侧一旦也退化成同样行为，测试仍然绿。 |

### 4.3 C 层：事实级空转（函数体没有任何断言、也不 failwith）

这些 `[<Fact>]` 只证明「不抛异常」，对所声称的行为**零断言**：

| # | 位置 | 测试名 | 空转原因 |
| --- | --- | --- | --- |
| C1 | `compose/ServerApp/tests/ServerAppTests.fs:169` | 「ServerApp 启动时正确处理截尾事件文件并恢复运行」 | 写了半截损坏字节 → 启动 → 停止。**没有断言截尾恢复的结果**（日志是否被修好、commit 是否保留）。启动不抛异常即绿。 |
| C2 | `compose/ServerApp/tests/ServerAppTests.fs:215` | 「ServerApp 监听 0.0.0.0 且无 TLS 时正常启动并执行 WarnIfExposedWithoutTls 逻辑」 | **完全没有断言 `WarnIfExposedWithoutTls` 被执行过**（没有任何告警探针）。 |
| C3 | `compose/ServerApp/tests/ServerAppTests.fs:250` | 「ServerApp 连续多次 Stop 与 Dispose 保持严格幂等不抛出异常」 | 只有「不抛异常」；**没有断言端口已释放 / 锁已释放 / writer 已关闭**。 |
| C4 | `compose/ServerApp/tests/ServerAppTests.fs:291` | 「ServerApp 启动时配置不存在安全回退到默认配置并正常启停」 | **没有断言回退到了默认配置**（无配置内容断言）。 |
| C5 | `compose/MarkdownView/tests/Tests.fs:361` | 「markdown renderer renders complex markdown with code, tables and math without error」 | 渲染复杂 Markdown 后 `w.Close()`，**没有断言任何渲染结果**（标题、表格、公式是否出现都不知道）。 |
| C6 | `compose/EventStore/tests/StoreConsistencyTests.fs:466` | 「EventStore 正常 Dispose 释放 writer 且不抛出异常」 | 没有断言 writer 真的释放（无「再开一次能成功」这类反向验证）。 |
| C7 | `libs/BrowserInterop/tests/BrowserPage.Tests.fs:45` | 「没有订阅者时 NotifyHidden 不抛」 | 语义就是「不抛」，作为冒烟可接受，但**不构成任何行为断言**。 |

### 4.4 结构性观察（不是单行空断言，但同样削弱证据）

- **`compose/AppShell/tests/ComposerTests.fs` 整体只断言模型与 Effect**，SPEC 7 要求的「原生交互证明（Headless 窗口中验证焦点流转、无障碍角色）」在这一层**没有发生**。旅程 1–6 中唯一触及原生控件的是缩略图端口断言（`Assert.Equal(1, thumbnails.Length)`，A1 所在测试，现 `:41`）与 `rig.Views.Onboarding.*` 三个属性断言。**旅程 3 已另行补上真实控件树用例**（`compose/AppShell/tests/JourneyAttachmentTests.fs`，2026-10-22 新增，见 §4.5）：它驱动真实「添加附件」按键与真实发送按键，ACK 经真实线协议事件入口注入，并断言发送键的可用性、提示文案与托盘 chip 状态字。
- **2026-10-23 增补**：`ComposerTests.fs` 再补旅程 1/5/6 的缺失路径与原生交互行为用例（真实无头窗口下 Tab 焦点流转、Esc 搜索清空、删除邻位聚焦；名称与角色的整树断言归 `A11yTraversalTests.fs`）。动态运行 `[待验证]`。
- **`docs/ux/r28-pwa-parity.md` 表中明确标注「CI 未断言」的行为**（剪贴板、文件选择/保存、右键菜单、拖放、限流倒计时/搜索防抖）今天仍无断言——这些是**已被记录但从未自动化**的缺口。
- **`compose/EntryShells/AGENTS.md` SPEC 2 自陈**：「`./run-desktop.sh` / `./run-pwa.sh` 产包、构建并启动（**真实界面验收要人看，仓库内未自动化**）」——这是全仓对「界面缺陷为何靠人肉发现」最明确的一句自白。
- **`compose/Theme/tests/A11yRegression.fs` 的 7 条对比度用例**走 `check()` 帮助函数，**函数体内确实有 `Assert.True(bad.IsEmpty, …)`**，**不是空断言**；但它们只算调色板数值，**不构造控件、不看实际渲染出的颜色**（控件若没绑定该资源，对比度全绿而界面仍是错的）。

### 4.5 处置记录（2026-10-21，本轮已落地）

**已修复（断言改为可证伪的真实契约）**

| 文件 | 用例 | 改成了什么 | 现在什么变异会让它红 |
| --- | --- | --- | --- |
| `compose/AppShell/tests/ComposerTests.fs` | 附件上传 | 断言缩略图端口恰好一次且字节相同；非图片不走该端口 | 缩略图不再被调 / 调了两次 / 字节不符 |
| `compose/AppShell/tests/ComposerTests.fs` | 断开连接 | 断言在途计数归零（不变式 3）、`Conn = Connecting 1`、有「已断开连接」提示 | 断线后在途命令未清 / 状态没落回 Connecting / 没提示 |
| `compose/AppShell/tests/ComposerTests.fs` | 旅程 2 | 断言目录就绪且 `providers` 为空、引导事实为空、输入区禁用且原因正确、空态是 `NoConversation` | 目录状态错 / 输入区在无模型服务时仍可发 / 禁用原因丢失 |
| `compose/AppShell/tests/ComposerTests.fs` | 旅程 5 | 断言发送键**精确**回到 `Idle`（B2：不再只验「不是 GeneratingStop」）、`RunState.Idle (Some err)` 带 kind/message/retryable、`GenerationCancel` 发给**那一个**对话与代号 | 停止后仍不可发 / 发送键停在别的忙态 / 错误被丢掉 / 取消发错对话 |
| `compose/AppShell/tests/ComposerTests.fs` | 旅程 5b（新增，提交 `2d6a58e`） | 在**真实控件树**里按可访问名「重试生成」找到 ErrorPanel 的重试按钮并点击；断言重试命令真的作为 `Effect.SendWire` 发出（`chat.regenerate`）、进入待确认队列、发送键进 `Sending`；命令确认后待确认清空、发送键回 `Idle`；重新生成开始时进 `GeneratingStop`、成功后回 `Idle` 且错误卡与重试按钮消失 | 重试按钮点不动 / 命令没发出 / 待确认没入队或没清 / 发送键状态错 / 成功后错误卡仍在 |
| `compose/AppShell/tests/ComposerTests.fs` | 旅程 6 | 断言提示条**必须**带撤销动作、撤销前有一个待删批次且含那两段对话、撤销后批次清空 | 撤销入口消失 / 批次漏记 / 撤销后仍残留 |
| `compose/AppShell/tests/ComposerTests.fs` | 旅程 3 | 断言发送被主机的 `attachment.committed` 确认门控（未就绪时发不出去、草稿与附件都留着），并断言真正发出的 `SendUserMessage` 载荷：`contents` 有 2 条、文本「你好」、附件带 `fileName` / `sha256` / `size`（`:165-238`） | 附件没随消息发出 / 附件元数据不全 / 未就绪时也能发 |
| `compose/AppShell/tests/JourneyAttachmentTests.fs` | 旅程 3（**真实控件树**，2026-10-22 新增） | 走真实「添加附件」按键与真实发送按键；认证/目录/ACK 全经真实线协议事件入口（`FakePorts.Deliver` → 宿主事件订阅 → `ShellComposer.HandleEvent` → 状态树）。断言：上传完成（`attachment.complete`）后附件**仍未就绪**——发送键 `IsEnabled = false`、提示为「附件上传中，请稍候…」、托盘 chip 状态字为「上传中」；走真实控件的提交入口也**发不出**任何 `SendUserMessage`；主机 `attachment.committed` 之后附件才 `Ready`、发送键启用、chip 状态字变为文件大小；点真实发送键后出线的 `SendUserMessage` 真的带上附件（`type` / `fileName` / `mediaType` / `size` / `sha256`） | 附件没等主机确认就能发 / 确认后仍发不出去 / 发送键的可用性或提示与真实状态不一致 / 发出的消息不带附件或元数据不全 |
| `compose/AppShell/tests/ExpiryDeletionTests.fs` | 旅程 6（到期真删） | 假时钟推进过宽限期后，断言客户端**恰好**发出一条 `conversation.delete` 命令、待删批次清空；再把**同一条**命令送进服务端事件存储，断言投影里该对话被标记删除、列表里不再出现 | 到期不发命令 / 发多条 / 发错对话 / 批次不清 / 命令落库后对话还在列表里 |
| `compose/AppShell/tests/ComposerTests.fs` | 旅程 1（2026-10-23 增补） | 断言配对后装载可用目录、输入首发文本、发出 CreateConversation + SendUserMessage + ObserveConversation、快照到达呈现活跃消息与标题 | 首发未发出建对话命令 / 消息载荷丢失 / 快照到达未进入活跃态 |
| `compose/AppShell/tests/ComposerTests.fs` | 原生交互证明（2026-10-23 新增） | 真实无头窗口下 Tab 焦点流转、Esc 搜索清空、删除邻位聚焦与列表变空回落搜索框（名称与角色的整树断言在 `A11yTraversalTests.fs`） | 焦点不可移动 / Esc 未清空 / 邻位焦点算法退化 |
| `compose/WsClient/tests/ClientStateAndWsClientTests.fs` | 连接被抢占 | 断言真的抛 `OperationCanceledException("superseded")`（旧写法吞掉异常且从不断言标志位） | 不抛 superseded / 抛别的异常 |
| `libs/ConfigFeedback/tests/DownloadBuffers.Tests.fs` | 旧实现摘录 | 逐个写出 `Accepted` / `Unknown` / 坏 base64 三条分支，不再 `| _ -> ()` 一锅端 | 新旧摘录行为不一致 |
| `compose/ServerApp/tests/ServerAppTests.fs` | 截尾事件文件 | 断言进入**只读降级模式**并留下 `read-only-degraded` 诊断、且**没有**偷偷改用户的日志（S-001 契约） | 降级诊断缺失 / 启动擅自截断日志 |
| `compose/ServerApp/tests/ServerAppTests.fs` | 监听 0.0.0.0 无 TLS | 断言写出 `insecure-listen` 告警 | 告警缺失 |
| `compose/ServerApp/tests/ServerAppTests.fs` | 幂等 Stop/Dispose | 断言目录锁**真的释放**（同目录能再开一个 ServerApp） | 锁没释放 |
| `compose/ServerApp/tests/ServerAppTests.fs` | 配置缺失回退 | 断言写出了默认配置文件且监听地址正确、并正常监听 | 回退没写配置 / 没监听 |
| `compose/MarkdownView/tests/Tests.fs` | 复杂 Markdown 渲染 | 断言标题、正文、行内代码、表头、代码块、引用块、链接（按可访问名称）、任务条目、公式**都在渲染树里** | 任一块没渲染 / 渲染成别的内容 |
| `compose/EventStore/tests/StoreConsistencyTests.fs` | Dispose 释放 writer | 断言同目录能再开一个 EventStore | 锁没释放 |
| `libs/BrowserInterop/tests/BrowserPage.Tests.fs` | 无订阅者时 NotifyHidden | 断言零订阅者时不误触发，且之后新订阅者仍能被触发 | 状态被破坏 / 误触发 |

**旅程层仍缺的（不是断言强度问题，是「用例没走到那一步」）** —— 见 `展望.md` §2：

| 旅程 | 还缺什么 | 证据位置 |
|---|---|---|
| 旅程 1 | 用例止于 `ConnPhase.Authed(inst, CatalogPhase.LoadingCatalog)`，**没有走完配对后的首条消息** | `compose/AppShell/tests/ComposerTests.fs:90-121` |
| 旅程 5 | ~~断言了失败落进 `RunState.Idle (Some err)` 且带 `retryable`，但**没有触发重试**~~ —— **已补**：旅程 5b 在真实控件树里点 ErrorPanel 的「重试」按钮走完整条重试路径（用例提交 `2d6a58e`） | `compose/AppShell/tests/ComposerTests.fs:455-644`（旅程 5b）；重试按钮在 `libs/ActionStrip/ErrorPanel.fs:369-393`，派发在 `compose/AppShell/AppShell.fs` 的 `retry ()` → `ChatUserMsg.Regenerate`，命令构造在 `libs/WorkspaceState/Update.fs:1208-1215` |
| 旅程 6 | ~~断言了待删批次与撤销把批次清空，但**没有验到期提交真删**~~ —— **已补**：到期真删另有独立用例（见 §4.5 的 `ExpiryDeletionTests.fs` 行） | `compose/AppShell/tests/ExpiryDeletionTests.fs`；到期删除在 `libs/WorkspaceState/Update.fs` 的 `MsgTick` 分支（不是 `libs/BoundedWait`） |

旅程 1 这条属**代码侧缺口**，本轮未改代码，故上表只登记、不伪称已修。旅程 5 的真实重试已由 `compose/AppShell/tests/ComposerTests.fs` 的旅程 5b 覆盖（失败 → 真实「重试」按钮 → `chat.regenerate` 发出 → 待确认 → `CommandCommitted` → 重新生成 → 错误卡消失）；旅程 6 的到期真删已由 `compose/AppShell/tests/ExpiryDeletionTests.fs` 覆盖（假时钟推进宽限期 → 发出 `conversation.delete` → 同一条命令落进服务端事件存储后对话从投影里消失）。

**已迁移（产品文案规则 → lint 脚本，不是测试）**

产品文案规则（术语表、取消类提示须写明 Esc、中文省略号）**没有运行时可观察**——
它约束的是用户看到的字，不是任何对象的行为。所以它不该伪装成 xunit 用例
（假装自己能被 `Assert` 证明），而应该是一条独立的、扫描源码的 lint 规则。
旧的两条用例还有两个实打实的缺陷：正则匹配的是不存在的 `.IconButton(`（命中 0 次仍绿），
以及扫描范围只覆盖 `compose` + `app`（看不到住在 `libs` 的全部取消类控件）。

| 旧用例（在 `compose/Theme/tests/A11yRegression.fs`） | 新归属（`tools/lint-copy.sh`） | 范围变化 |
| --- | --- | --- |
| `界面文案不使用已淘汰的同义词`（设定/秘钥/Api Key/api key/Apikey） | C1 | `compose`+`app` → **`libs`+`compose`+`app`** |
| `取消按钮的提示文案都写明 Esc` | C2 | `compose`+`app` → **`libs`+`compose`+`app`** |
| `界面文案用中文省略号而不是三个英文句点` | C3 | `compose`+`app` → **`libs`+`compose`+`app`** |
| `PWA 页面文案不使用已淘汰术语` | C4（扫 PWA 壳交付物） | 不变（仍是那三个文件） |
| `所有图标按钮调用都带非空名称` | **不属于文案规则** → 不迁移 | 控件的可访问名称应由控件自己的 headless 测试断言（§3 G5 待建） |

**已删除（源码接线扫描器）**

| 文件 | 删除的用例 | 为什么删而不是修 |
| --- | --- | --- |
| `compose/Theme/tests/A11yRegression.fs` | `模型选择不再保留隐藏顶栏入口` / `r18 菜单与入口一致性` | **接线扫描器**：读 .fs 源码查符号在不在。这验的是「文件里有没有这个字符串」，不是「接线对不对」；接线对不对应由行为测试断言。 |

**新增门禁（两条，都不碰共享构建缓存）**

- `tools/assert-hygiene.sh`（`./x hygiene`，CI 的 `hygiene` 作业第一步）：R1 恒真断言、R2 恒真谓词、R3 空转的事实。
  - 实现是**词法扫描 + 括号配对**，不是正则扫行：先剥掉注释（`//` 与**可嵌套的** `(* *)`）与字符串（`"..."`、`$"..."`、`"""..."""`、`@"..."`、字符字面量），保留行列；再在代码骨架上用**平衡括号**取 `Assert.*` 的参数。所以跨行的原始缺陷 `Assert.Contains(xs, fun eff -> match eff with | _ -> false || true)` 认得，带空格的 `Assert.True (true)` 也认得，而注释与字符串里的同形文本**不会**误报。
  - R3 的事实体界定为「**它自己的 `let` 那一行起**，到下一个顶层声明为止」（不是「到下一个 `[<Fact>]`」），单行事实（`let ``x`` () = Assert.Equal(...)`）也算有断言；辅助函数按本文件的顶层 `let` 界定并做**传递闭包**（A 调 B、B 会断言 → A 也算会断言），没有行数截断。
  - 已用「植入违规」验证（每条都植完即撤）：植入**原始跨行** `| _ -> false || true` → R2 红；植入带空格的 `Assert.True (true)` → R1 红；植入只有 `ignore` 的 `[<Fact>]` → R3 红；植入**注释里**写 `Assert.True(true)` 的事实 → R3 仍红（因为确实没有断言）但**不**触发 R1（注释被正确忽略）；植入调用 `check()` / 经 `check2` 间接调用 `check()` 的事实 → **不**红（辅助函数识别正确，含传递）；植入**嵌套**块注释 `(* outer (* inner *) *)` 之后的真实违规 → R1 仍能抓到（嵌套注释没有吞掉后续代码）。
- `tools/lint-copy.sh`（`./x lint`，CI 的 `hygiene` 作业第二步）：C1 禁用同义词、C2 取消类提示须写明 Esc、C3 中文省略号、C4 PWA 壳术语。
  - 验收标准是「**在当前树上通过**」：当前扫描 **376** 个生产 `.fs`（`libs` + `compose` + `app`）、**1737** 个中文串字面量 + 3 个 PWA 壳交付物，全部通过。（文件数与字面量数随仓库变动，脚本每次运行都会重新报实际数字。）
  - 已用「植入违规」验证四条规则都会红：植入「设定」→ C1 红；植入「取消并关闭…」但去掉 `Esc` → C2 红；植入中文串里的 `...` → C3 红；往 `app/Pwa/wwwroot/index.html` 植入「会话」→ C4 红。

---

## 5. 界面改动交付清单（DoD）——由改界面的工程师自己走完

> **所有权原则**：交付界面的人**自己**对端到端交付闭环与验证证据负全责，不把「验证」外包给下游。
> 下游（CI、评审、QA）只能**发现**问题，不能**代替**发现问题。所以下面每一步的证据都由**改这处界面的人**产出并附在改动里。
> **职权边界与分工说明**：本原则要求的是交付责任闭环，绝不意味着 Engineer 角色可越权执行 bash 命令或自行跑构建冒烟。Engineer 负责代码实现、编写回归测试与文档契约；真实编译构建、运行测试、冒烟执行与真机采集全归 DevOps 履职，严禁伪造测试结果或绕过角色分工。
>
> 这不是引用某个外部权威标准的条款表——它是本仓库自己定的**本项目交付契约**，与根 `AGENTS.md` 的 SPEC 7 同源。
> 清单会随实践变；改它要连同证据一起改。

### 5.0 什么时候用这份清单

**任何会改变用户看到的东西的改动**：控件外观、文案、键盘与焦点行为、无障碍属性、状态投影、对话框与浮层的开合。
纯逻辑重构、只改内部实现而不影响界面的改动，走普通代码评审即可（但 §5.1 的第 1 步仍然要做——它决定这次改动**到底要不要**走这份清单）。

### 5.1 清单（九步，按顺序）

每步都要留下**可核对的产物**（命令 + 输出 + 截图路径），而不是「我看过了」。

| # | 步骤 | 做什么 | 留什么证据 |
| --- | --- | --- | --- |
| 1 | **需求 / 用户旅程** | 说清这次改动服务哪条用户旅程、用户在哪一步看到变化、什么算「做对了」。写不出具体旅程的，先别改。 | 改动说明里的两三句话 |
| 2 | **状态 / 交互设计** | 说清改动落在哪个状态、哪些状态会变、哪些**不会**变；键盘与焦点怎么走；无障碍名称与角色是什么。 | 状态转移表或一段说明 + 对应的 `AutomationProperties` 取值 |
| 3 | **实现** | 改代码。**状态从唯一主人来**（不手搓视图状态记录）。 | 代码本身 |
| 4 | **真实界面看一遍** | 打开真实界面看，不看单元测试。控件库与组合者用**画廊**逐状态看；整体用真实入口看。 | **截图**（路径写在改动说明里）+ 键盘操作的实际结果（按什么键、焦点去哪了） |
| 5 | **无障碍行为** | 读出这个控件的无障碍名称与角色；确认 Tab 序与视觉序一致；确认读屏不会重复朗读。 | 无障碍名称/角色的实际取值 + 焦点轨迹 |
| 6 | **针对性测试** | 为这次改动写**能因它而红**的测试。断言必须能被某个具体变异打红；写不出变异 = 这个测试是空转的（`./x hygiene` 会拦一部分，但语义判断靠你）。 | 测试名 + 「什么变异会让它红」的一句话 |
| 7 | **包与干净消费** | 改动所在的库要打成交付包；下游只能**从包**消费，不能引用源码。 | `./x pack` 通过 + 该库的消费者测试通过 |
| 8 | **两个入口** | 桌面与 PWA 两个入口都能从交付包构建并启动。 | 两个入口的构建通过 |
| 9 | **已发布产物冒烟** | 对**发布出来的产物**跑冒烟（不是对源码跑）。 | 冒烟全绿 |

### 5.2 第 4 步的画廊怎么用

```
bash tools/gallery.sh --list                                # 看有没有这个控件的条目
bash tools/gallery.sh --window                               # 真实桌面窗口，逐条看
bash tools/gallery.sh --render <条目名> /tmp/gallery.png     # 单个状态 → PNG（720×900）
```

- **画廊是看状态的工具，不是门禁**：它出的 PNG 供人看，**没有基线、没有 diff、CI 不会因为渲染变化而红**。
- **跑本仓组合者测试时的两个陈旧缓存坑**（都实测踩过，写在这里免得下一个人再踩）：
  1. **全局 NuGet 缓存会留下旧的本仓包**——`~/.nuget/packages/wanxiang2.*` 里存着旧版本的本仓包（实测 84 个目录）。跑本仓组合者测试若**不带** `-p:RestorePackagesPath="$PWD/artifacts/packages"`，会解析到旧程序集，表现为「**我明明改了却不生效**」。
  2. **`bin/` 里的陈旧同名 dll 会让 F# 编译器解析到错误的程序集**——包合并之后输出目录里可能还留着合并前的独立 dll（例如合并 `ComposerSendState` 时留着旧的 `ComposerSendState.dll`），编译器就以为它还是独立程序集，报一堆「类型找不到」。合并后先清输出目录再构建。
- 画廊覆盖 **40** 条条目（`--list` 实测）。**覆盖不到的条目要如实说「画廊里没有这一条」**，不要假装看过了。
- 画廊的夹具**已收口**：附件、侧栏多选、错误卡三条状态归属都落定了（见 `compose/Gallery/AGENTS.md`《已知缺口与各自的归属》）。画廊**如实渲染当前真实状态**——看到的就是现在的真实样子，不是应有的样子。
- 侧栏多选已收口：画廊的 `SidebarDesk.batch` 条目走**真状态树消息**（`SidebarUserMsg.EnterBatch` + `SidebarUserMsg.ToggleBatch` → `update` → `project` → 渲染），不再调侧栏自己的 API。
- **画廊是确定性的**：状态用固定时钟（`Scenes.fixedNow = 2026-10-21T08:00:00Z`），同一条目连渲两次 PNG **逐字节相同**（实测 `MessageCardView.toolcall` 两次渲染 md5 一致）。这让「同一状态渲染两次做像素 diff」成为可能——但**基线与判定仍没建**（G3 未建）。

### 5.3 这次改动的已知边界（必须写）

清单的最后一项产物不是「全绿」，而是**边界**。改动说明里必须写清：

- 这次**没有**覆盖到什么（例如「只验了桌面，没验 PWA」「这一条画廊里没有」「读屏器没实测」）。
- 哪些结论是**推断**而不是**观察**（例如「按状态机推断按钮会禁用，但我没在真实界面里按过」）。
- 哪些是**已知不具备条件 / 尚未成立**的（宿主环境无 orca / speech-dispatcher / at-spi-bus-launcher，adb 无物理设备，因此本机不具备原生屏幕阅读器发声与真实手机实测条件，见 `docs/evidence/2026-10-23-handoff/initial-state.md`；全仓无障碍树整树自动遍历断言 G5 与像素级回归 G3 仍未建；真实 Tab 轨迹录像与 PWA Lighthouse 仍未建；2026-10-23 补齐的旅程与原生交互测试待 DevOps 执行构建与验证）。

### 5.4 现在还没有的自动化（不要假装有）

这份清单里，**第 4、5、8、9 步目前靠人**；能自动化的部分只有：

- 第 6 步的一部分由 `./x hygiene` 兜底（恒真断言 / 恒真谓词 / 空转的事实）。
- 第 3 步的**格式**部分由 `./x check` 里的格式门禁兜底（**只查改动文件**的 `dotnet fantomas --check`；全仓运行被禁止，见 §2.(f)）；语义仍靠评审。
- 文案类规则由 `./x lint` 兜底（禁用同义词 / 取消类提示须写明 Esc / 中文省略号 / PWA 壳术语）。
- 第 7 步由 `./x compose` 与 `./x consume` 兜底（干净包缓存、仓库外真实消费）。
- 第 9 步由 `tools/smoke_published.sh` 兜底一部分（画布、DPR、上屏桥、草稿落盘与恢复、无页面异常）。

**仍然没有的**：像素级视觉回归（§2.(a)）、浏览器侧真实交互 e2e（§2.(d)）、无障碍树整树遍历断言（§2.(e)）。
**不要把画廊当成像素回归门禁，也不要把 `smoke_published` 的「画布非纯色」当成交互 e2e。**

### 5.5 门禁速查

| 命令 | 管什么 | 需要构建产物吗 |
| --- | --- | --- |
| `./x check` | 依赖边界（库不引源码、组合者只用包、文档点名的路径存在）**+ F# 格式**（**只查改动文件**的 `dotnet fantomas --check`，未格式化即红；全仓运行被禁止；见 §2.(f) 与 §3 G6） | 否 |
| `./x hygiene` | 断言卫生（恒真断言 / 恒真谓词 / 空转的事实） | 否 |
| `./x lint` | 界面文案（禁用同义词 / 取消类提示须写明 Esc / 中文省略号 / PWA 壳术语） | 否 |
| `bash tools/gallery.sh --render …` | 人工看单个状态（**不是门禁**） | 否（用独立缓存） |

CI 里这三道秒级门禁在同一个 `hygiene` 作业里跑；`./x all` 的顺序是 check → hygiene → lint → 逐库 test → pack → compose → consume。

---

## 6. 汇总

- **现有界面验证**：**1907** 条 `[<Fact>]`（`compose/*/tests` 957 + `libs/*/tests` 950；按 `[<Theory>]` 的 `[<InlineData>]` 行展开计，则为 980 + 1034 = **2014** 条用例），其中界面相关的绝大多数是**控件树结构与状态机**断言；**像素**断言只有公式一处（`compose/MathFigure/tests/Probe.fs`），且那里的整块像素哈希**已从金样移除**（跨平台光栅化不稳定，现只做同进程内前后对比与布局量锁定，见 §1.2）；交互断言在 PWA 侧只有「文本能进输入框」一处。（`compose` 的 957 含 2026-10-22 新增的旅程 3 真实控件树用例 `compose/AppShell/tests/JourneyAttachmentTests.fs`；此前记为 956。）
- **本轮加上的三样**：
  1. **断言卫生门禁**（`./x hygiene`）——把「恒真断言 / 恒真谓词 / 空转的事实」变成红灯；已用植入**原始**缺陷验证会红。
  2. **界面文案门禁**（`./x lint`）——产品文案规则（术语表、取消类提示须写明 Esc、中文省略号、PWA 壳术语）从测试套件迁出，覆盖范围补到 `libs` + `compose` + `app`。
  3. **组件画廊**（`compose/Gallery` + `tools/gallery.sh`）——40 条状态条目可单独列出、渲染（720×900 PNG）与人工查看。**它是工具，不是门禁。**
- **仍然没有的**：像素级视觉回归、浏览器侧真实交互 e2e、无障碍树整树遍历断言、真机屏幕阅读器实测。
- **Top 3 缺口（按严重度）**：
  1. **没有任何像素级或真实交互级的界面门禁**（§2.a + §2.d）——界面正确性仍然**没有自动化担保**；§5 的清单里第 4、5、9 步靠人。
  2. **画廊不是回归门禁**——渲染变了 CI 不会红。
  3. **没有快速回路**（§2.c）——改一个控件后仍要走 `pack → 构建入口 → 起 app`；画廊补上了「单独看一个状态」，但没补上「改完立刻看到」。
- **交付契约**：§5 是改界面的工程师自己走完的清单（需求/旅程 → 状态与交互设计 → 实现 → 真实界面截图 + 键盘 + 无障碍行为 → 针对性测试 → 包与干净消费 → 两个入口 → 已发布产物冒烟 → 证据与已知边界）。


---

## 7. 2026-10-23 交付状态准备（严格保持待验证）

- **基线与时间范围澄清**：Git 完全干净仅为**接手时初始快照**（HEAD `14e4625`）的事实；当前各团队已有多路源码修改落盘，工作树处于活跃多团队协作态；历史字体修复与 11 条测试通过属于提交 `14e4625` 的历史证据，不得视为本轮动态核准。
- **PWA 与真机/读屏限制**：PWA 新增 16 项端到端交互断言纯属源码待运行；截图存在或画布色彩/像素差异**绝不能**证明中文逐字清晰可读，必须由 DevOps 真实跑测并对截图进行人工目检；宿主环境无 orca/speech-dispatcher/at-spi，adb 无物理设备，真机读屏与手机端严格保持 `[待验证]`。
- **收口方式**：不转交未确定的未来人员，本轮我们将继续至验证与收口，先保持待验证，等待接收 DevOps 的最终实跑结果与留证后，由本会话依据最终证据统一更新收口。
