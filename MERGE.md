# 包合并方案(已执行)

> 状态:**已执行**（2026-10-19）。41 组合并全部落地：`libs` 242 → **123** 个包,
> 组件工程 316 → 196（123 个库 + 73 个组合者）。
> 证据见本文末尾「执行结果」。

## 执行结果

本机（`XJOBS` 默认 12）跑完 `./x all`：

```
check ok: 123 libs with no sibling references; 72 composer(s) using packages only
逐库测试：123 个工程全部通过
pack wave 1..9（111 / 44 / 18 / 14 / 2 / 2 / 2 / 2 / 1 个工程）
compose（组合者测试）：67 个工程全部通过
consume：仓库外消费者提交后重开得到 Used 1UL
```

`./x app` 两个入口（Desktop、Pwa）从交付包构建成功。

### 与本文原方案的出入

1. **库的终数是 123，不是 122。** 表中 161 个成员名里，`LocalHostGate` 在旧仓库
    不是独立目录，而是「StartupTrace 目录下的 `LocalHostGate.fsproj`」（异名工程，见 `x` 的
    `component_projs` 注释）。它连同 `StartupTrace` 的其余文件一起并入 `LocalHostGate`
    包，所以被去掉的目录是 119 个：`242 - 119 = 123`。
2. **依赖图分 9 波不变**（`111 / 44 / 18 / 14 / 2 / 2 / 2 / 2 / 1`），但工程总数
    从 316 降到 196：合并掉的库少一票，组合者与 app 入口不变。
3. **组内成员并非「按文件名字节序编译即可」。** 合并后的包按各成员**原工程文件的
    编译顺序**拼接（`Look.fs` → `Kit.fs` → 主体），因为组内同名文件（`Look.fs`、
    `Kit.fs`）在各自命名空间里，先定义者才能被后者引用。

### 测试工程的合并约定

- 测试文件按 `<成员名>.<原文件名>.fs` 落盘（如 `ToastDwell.Render.fs`），
  避免同名文件互相覆盖。
- 成员自己的顶层模块（`Tests` / `Samples` / `Render` / `Adapter`）改名为
  `<成员名><模块名>`（如 `ToastDwellRender`），成员之间才不撞名；
  控件库的测试沿用各自的 `Wanxiang2.<成员>.Tests` 命名空间，本来就不撞。
- golden 目录按成员分目录（`tests/golden/<成员名>/…`），测试里的路径同步改成
  `golden` / `<成员名>` / `<文件名>`。
- 控件库的测试工程加 `AssemblySettings.fs`（`DisableTestParallelization`）：
  Avalonia 的平台状态是进程级全局量，一个工程里多个 headless 会话并发初始化会炸。

### 顺带修掉的两个既有缺陷

1. **`./x pack` 从未成功过**（`x` 的波次打包在引入时就是坏的）。临时 solution 放在
   `artifacts/` 下，而 MSBuild 按 solution 自身所在目录解析工程路径，于是去找
   `artifacts/libs/…`（MSB3202）。改成 `../$proj` 后波次打包可用。
2. **`./x consume` 装不上 FSharp.Core**。仓库外的临时项目读不到
   `Directory.Packages.props`，F# SDK 隐式引的那份跟 SDK 走（本机 10.0.111），
   而交付包按登记的 10.1.401 编译：NuGet 判降级、运行时装不上程序集。
   消费者工程关掉隐式引用并显式引同一版本。

## 判据

两个库满足以下**全部**条件才合并为一个包:

1. **生产消费者集合完全相同**(`libs/`、`compose/`、`app/` 下非 tests/tools 工程引用该包的集合逐一致);
2. **外部依赖相同**(`PackageReference` 中非 `Wanxiang2.*` 的集合逐一致);
3. **Wanxiang2 依赖相同**(通常为空;控件库统一只依赖 `UiKit`)。

合并后任何消费者都不会多拿到它本来不用的 API——消费者集合相同意味着它要么全要、要么全不要。这是「适当合并但不变大泥球」的机械判据,不靠主观判断。组内成员互不依赖(依赖不同就进不了同一组),合并后按文件名字节序编译即可。

## 已验证的前提

- 依赖图分 9 波:`201 / 74 / 18 / 14 / 2 / 2 / 2 / 2 / 1`(共 316 个组件工程)。
- 41 个合并组、覆盖 162 个库;合并后 `libs` 从 242 → **122**。
- 全部组内**顶层 `module`/`type` 无重名**(仅 `internal`/`private` 关键字级别的名字重复,不构成冲突)。
- 组内源码互不引用(依赖集合相同且非空时依赖指向组外)。

## 合并组清单

按消费者分组;括号内为该组的外部依赖与 Wanxiang2 依赖(组内一致)。

| # | 消费者 | 外部依赖 | Wanxiang2 依赖 | 成员(合并为一个包) |
|---|---|---|---|---|
| 1 | AppShell | — | — | ChangeWatch, ConversationSurface, DeferredReveal, DraftSummaryText, ForkAnchor, IdleSurface, MessageLink, OlderPageLoad, ReconnectBackoff, ReconnectLoop, RevealTarget, StartReady, StartupTrace |
| 2 | MessageCardView | — | — | CardBits, CaretBreath, CountdownTicker, DurationText, GenerationErrorTitle, MessageMenu, MessageTimestamp, ToolArgsPreview, ToolCallState, ToolErrorSummary, ToolOverview |
| 3 | SettingsScreen | — | — | GenerationDraft, GenerationForm, LocalDraftControls, McpForm, OnboardingFlow, ProbeFailureKind, ProbeSlot, ProviderForm, ProviderProbeText, SectionMemory, ToolsPolicy |
| 4 | MessageCardView | Avalonia | UiKit | ActionStrip, AttachmentChip, ErrorPanel, FoldHeader, RevealPane, StreamCaret, ThinkingBlock, ToolCallCard, UsageFoot, UserBubble |
| 5 | SidebarDesk | — | — | RowFocus, SearchHit, SearchRequery, SearchSegments, SearchSession, SidebarEmpty, SidebarSelection |
| 6 | Doctor | — | — | AttachmentAudit, HostProbe, McpCheck, ProviderCheck, SecretText, SnapshotCheck |
| 7 | SidebarDesk | Avalonia | UiKit | BatchBar, ConversationRows, HostSwitcher, ListEmptyCard, SidebarBrand, SidebarSearch |
| 8 | DialogsDesk | Avalonia | UiKit | ConfirmPane, ConnectPane, PromptPane, SessionPrefsPane, ShortcutHelpPane, TextEditPane |
| 9 | SettingsScreen | Avalonia | UiKit | DevicesPane, GeneralPane, OnboardingPane, ProvidersPane, SettingsFrame, ToolsPane |
| 10 | TransferDesk | — | — | ExportFileName, ExportView, ImportView, ProgressThrottle, TranscriptMarkdown |
| 11 | AppShell, SettingsScreen | — | — | BoundedWait, HostHealth, OnboardingRules, ProviderPresets |
| 12 | ChatPane | Avalonia | UiKit | ChatEmpty, ChatScroller, ExchangeNav, SkeletonList |
| 13 | McpClient | — | — | JsonRpc, LineProcess, McpProtocol, StreamableHttp |
| 14 | app/Desktop(Wanxiang.App) | — | — | LocalHostGate, PwaAssets, ShutdownSignals, SingleInstance |
| 15 | ComposerDesk | Avalonia | UiKit | AttachmentTray, ComposerBox, ModelChip |
| 16 | CodeTint | — | — | BoundedCache, HighlightQueue, HljsHtml |
| 17 | ChatScreen | — | — | BreadcrumbAction, RevealFlash |
| 18 | ChatPane | — | — | BreathPhase, ChatEmptyText, ScrollFollow |
| 19 | AppShell, ProtocolDispatch | — | — | ConfigFeedback, DownloadBuffers, HostRequests, UploadFacts |
| 20 | WsConnection | — | — | DeviceListJson, DeviceRules, DeviceSightings |
| 21 | ProviderTransports | — | — | HttpRetry, JsonPick, Sse |
| 22 | LedgerStore | — | — | JsonSeq, Ledger, Snapshot |
| 23 | OverlayDesk | — | — | PopupPlacement, ToastDwell, ToastQueue |
| 24 | ProviderTransports | Microsoft.Extensions.AI.Abstractions | — | Anthropic, Gemini |
| 25 | Attachments | — | — | AttachmentBlobs, MediaSniff |
| 26 | MarkdownView | — | — | BlockDiff, LinkChunks |
| 27 | ShellDesk | Avalonia | — | BrandMark, ShellLayout |
| 28 | LaunchPlan, app/Desktop | — | — | CliArgs, RunModes |
| 29 | DesktopPairing, PairingDesk, WsConnection | — | — | ClientToken, DesktopClient |
| 30 | Theme | — | — | ColorBlend, ThemeTween |
| 31 | WsClient | — | — | CommitTimeline, TextSocket |
| 32 | ChatOrchestrator | Microsoft.Extensions.AI.Abstractions | — | ContextTrim, UsageTally |
| 33 | ChatOrchestrator | — | — | ContinuableReply, TitleText |
| 34 | TransferDesk | Avalonia | UiKit | ExportSheet, ImportSheet |
| 35 | PairingDesk | — | — | FailureLimiter, PairingCode |
| 36 | OverlayDesk | Avalonia | — | FocusOrigin, OverlayLayer |
| 37 | CommandPalette | — | — | FuzzyMatch, PinyinInitials |
| 38 | RichAssets | — | — | HljsCatalog, RichScripts |
| 39 | ServerApp | — | — | ListenAddress, WsHandshake |
| 40 | AgentContent, ChatOrchestrator, app/Desktop | Microsoft.Extensions.AI.Abstractions | — | MediaContent, MessageSerde |
| 41 | Search | — | — | MessageText, TextMatch |

注:第 1、3 组的成员数比最初扫描少 1(KeyChord 在 AppShell 组、PairingCodeInput 在 DialogsDesk 组因外部依赖不同被分到别组),以本表为准。

## 执行步骤（已按此执行）

1. 每组取组内第一个成员名作为新包名（表里已经是）；
2. 源文件按各成员**原工程的编译顺序**并入新包工程（不是字节序：见上「与本文原方案的出入」第 3 条）；
3. 测试工程合并：文件改名为 `<成员名>.<原文件名>.fs`，顶层模块改名为 `<成员名><模块名>`，golden 目录按成员建子目录；
4. 消费者的 `PackageReference` 从组成员替换为新包名，同工程内去重（NU1504）；
5. `./x sln` 重新生成；`./x check` 通过（文档引用、消费者存在性、无兄弟引用）；
6. 全量验证：`./x all`（逐库测试 + pack + compose + consume）全绿；`./x app` 两个入口从交付包构建成功。PWA 侧 `getAssemblyExports` 只引用 `BrowserInterop.dll`，该库不在任何合并组里，程序集名未变，`app/Pwa/wwwroot/main.js` 无需改。

## 预期收益

- `libs` 242 → 123，`dotnet pack` 的工程数同比例下降;
- 每少一个包 = 少一次 fsc 进程启动 + 少一份 obj/bin + 少一个 nupkg 的解包与还原;
- 配合已落地的波次 solution 打包(每波一次 restore/pack,而非每包一个 dotnet 进程),pack 的固定开销从 316 次进程级降到约 18 次 solution 级。

## 后续合并（2026-10-20 W7-B2）：ModelVision → HostCatalog

ModelVision 库并入 `libs/HostCatalog`（与本文同一条物理并库的先例）。

**起因**：`Catalog.visionOf` 从 `compose/Catalog` 下沉到 `libs/HostCatalog` 后，
它的名字推断分支要用 `ModelVision.classify`，多出一条兄弟包依赖边
（根 AGENTS.md《布局与工具》：库之间零依赖边，唯一例外是登记在册的 WorkspaceState）。
看图判断本身也是「模型能不能看图」这一件事的派生事实，与主机目录同属一处。

**做法**：

1. `ModelVision.fs` 落盘为 `libs/HostCatalog/ModelVision.fs`，编译序在 `HostCatalog.fs` 之前；
2. ModelVision 整个目录删除（含 `AGENTS.md` 与测试工程），`Wanxiang2.ModelVision` 包不再交付；
3. 测试用例并入 `libs/HostCatalog/tests/ModelVision.Tests.fs`（按本文「测试工程的合并约定」的文件命名，顶层模块改名 `ModelVisionTests`，避免与既有 `Tests` 撞名），断言口径原样保留；
4. 消费方的 `PackageReference` 从 `Wanxiang2.ModelVision` 换成 `Wanxiang2.HostCatalog`：
   `libs/WorkspaceState`（状态树投影看图判定）、`compose/Catalog`、`compose/ComposerDesk`（新增 HostCatalog 引用）、
   `compose/SettingsScreen`；`compose/EntryShells` 只引用过该包、代码不用，直接删引用；
5. 命名空间与类型名不变（仍是 `Wanxiang.UI` 下的 `ModelVision` 模块、`VisionSupport` / `VisionSource` 类型），
   消费方源码零改，只是装配进了 `HostCatalog.dll`；
6. `./x sln` 重新生成；`./x check` 通过。

**结果**：`libs` 由 119 个减到 **118** 个包（本文执行时为 123 个，其后各波继续并库）；
HostCatalog 仍是零兄弟依赖的库。

## 追加判据（2026-10-21 W7-CLEANBREAK）：依赖闭包判据

上文《判据》的三条是**充分**条件，不是必要条件。本节追加一条可机械核验的**放宽**判据，
它与上文同一条目的：「合并后任何消费者都不会多拿到它本来不用的 API」。

### 判据本体

两个库 A、B 满足以下全部条件时可合并：

1. **生产消费者集合完全相同**（与上文《判据》第 1 条一致）；
2. **依赖并集已在消费者的直接声明里**：令
   `U = 外部依赖(A) ∪ 外部依赖(B) ∪ Wanxiang2依赖(A) ∪ Wanxiang2依赖(B)`，
   则该消费者的 `.fsproj` 必须**直接声明** `U` 里的每一项。

第 2 条与上文《判据》第 2、3 条（「外部依赖相同」「Wanxiang2 依赖相同」）不同：
它要求的是**并集被包含**，不是**两边相等**。

### 为什么放宽是安全的（纯减法）

若 `U` 已被消费者直接声明，合并后新包声明的依赖集合是 `U`——与消费者原本直接声明的
依赖集合逐项相同或为其子集。合并因此：

- **不新增任何传递依赖边**（消费者的依赖闭包不变）；
- **不让消费者多拿到任何它本来不用的 API**（消费者集合相同意味着它要么全要、要么全不要）；
- 是**纯减法**：包数减一，交付物少一份。

反过来说，上文《判据》原来的第 2、3 条（「两边依赖相等」）**比这条更严**，
并且是它的**子情形而非特例**：两边依赖相等时并集就等于其中一边，
但那一项**是否已被消费者直接声明**仍要另查——若它只经传递依赖拿到，
原判据会放行、本判据不会。所以本判据是原判据的**收紧**：
「原判据通过」推不出「本判据通过」。

### 反向规则：并集不在闭包里则**不合并**

若 `U` 中有任一项不在消费者的直接声明里（只经传递依赖拿到），则**不合并**——
合并后消费者必须新增一条 `PackageReference`，那就变成「多拿到」，判据不成立。
这类组合**只能靠先把依赖下沉/上提成双方共有依赖**来解，不能靠合并绕过。

### 2026-10-21 一次扫描的结果

对当时全部 118 个包做机械枚举（同消费者集合的候选对共 16 对）：

**按本判据可合并（10 组，其中 1 组因死包退出，实际执行 8 组）**——
`ActionStrip`+`CardBits`、`Anthropic`+`HttpRetry`、`ApprovalPanel`+`ApprovalCardText`、
`AttachmentTray`+`ComposerSendState`、`BatchBar`+`RowFocus`、`ChatHeader`+`BreadcrumbAction`、
`ChatEmpty`+`BreathPhase`、`DevicesPane`+`GenerationDraft`、`ExportSheet`+`ExportFileName`、
`ModelSwitchPanel`+`ModelPickerList`（各组的 `U` 均已被其唯一消费者直接声明）。

**按本判据不可合并（5 对，逐条记下缺口，下一波不必重新论证）**：

| 候选对 | 依赖并集 `U` | 缺口来源链 |
| --- | --- | --- |
| `AguiMessageMap` + `LocalHostGate` | `AGUI.Abstractions`, `Microsoft.Agents.AI` | `app/Desktop` 只能经 `Wanxiang2.AguiMessageMap` 传递拿到 `AGUI.Abstractions`，经 `Wanxiang2.AgentHistory` 拿到 `Microsoft.Agents.AI` |
| `BlockDiff` + `MarkdownBlocks` | `Markdig` | `compose/MarkdownView` 经 `Wanxiang2.MarkdownBlocks` 传递拿到 |
| `BoundedWait` + `DevicesModel` | `QRCoder` | 三个消费者都经 `Wanxiang2.DevicesModel` 传递拿到 |
| `ConfirmPane` + `PairingCodeInput` | `Avalonia`, `Wanxiang2.WorkspaceState` | `compose/DialogsDesk` 经 `Wanxiang2.ConfirmPane` 传递拿到 `WorkspaceState` |
| `ContextTrim` + `ContinuableReply` | `Microsoft.Extensions.AI.Abstractions` | `compose/ChatOrchestrator` 经 `Wanxiang2.MediaContent` 传递拿到 |

**因职责边界被否决（2 组）**：即使依赖并集满足本判据，也不合并——
`ActionStrip`+`CardBits`（合并后 1747 行 / 19 源文件，会成为全仓第三大包，
且 `ActionStrip` 已含 10 个成员，继续加料会坐实「外观拼盘」）、
`Anthropic`+`HttpRetry`（协议请求/流整形与重试策略是传输管线上的两件不同职责，
两者各自已不小，合并会掩盖真实的边界）。

### 本次执行结果（2026-10-21）

那一轮机械枚举出 **11 个候选**（消费者集合相同的对），去向如下：

- **10 个满足本判据**（依赖并集已被消费者的直接声明包含）→ 可执行；
- **1 个不满足**：`InputSense` + `PromptHistory`——`PromptHistory` 查实为「未接线的真实功能」
  而非死包（见 `docs/W7-CLEANBREAK-AUDIT.md` §2.3），不是可并入的碎片；
- **2 个因职责/体积边界否决**（见上段）：`ActionStrip`+`CardBits`、`Anthropic`+`HttpRetry`。

**11 候选 − 1 不满足判据 − 2 边界否决 = 实际执行 8 组**：

| # | 目标包 | 并入的成员 | 结果 |
| --- | --- | --- | --- |
| A3 | `ApprovalPanel` | `ApprovalCardText` | `./x test ApprovalPanel` 通过（12 项） |
| A4 | `AttachmentTray` | `ComposerSendState` | `./x test AttachmentTray` 通过（16 项） |
| A5 | `BatchBar` | `RowFocus` | `./x test BatchBar` 通过（51 项） |
| A6 | `ChatHeader` | `BreadcrumbAction` | `./x test ChatHeader` 通过（7 项） |
| A7 | `ChatEmpty` | `BreathPhase` | `./x test ChatEmpty` 通过（18 项） |
| A8 | `DevicesPane` | `GenerationDraft` | `./x test DevicesPane` 通过（87 项） |
| A9 | `ExportSheet` | `ExportFileName` | `./x test ExportSheet` 通过（23 项） |
| A10 | `ModelSwitchPanel` | `ModelPickerList` | `./x test ModelSwitchPanel` 通过（7 项） |

每个组的动作顺序：源文件按各成员**原工程的编译顺序**并入目标工程 → 删被并库目录
（源、tests、AGENTS.md）→ 改消费者的 `PackageReference` → 按本文《测试工程的合并约定》
合并测试工程（测试文件改名 `<成员名>.<原文件名>.fs`，顶层模块改名 `<成员名><模块名>`，
Avalonia 控件库的测试工程保留 `AssemblySettings.fs`）→ 把被并成员的 `AGENTS.md` 分节
折进目标包的 `AGENTS.md` → `./x sln` → `./x check` → `./x test <目标>`。

**包数：118 → 109**（8 组合并 −8，另删死包 `AguiMessageMap` −1）。


## 追加判据（2026-10-21 W7-PKGSPLIT）：**包内**拆分判据

上文《判据》与《依赖闭包判据》回答的是**包与包之间**的问题（两个库能不能合并）。
本节回答的是另一个问题：**一个包内部该不该拆**。这是两件事、两套判据，
本节不复用上文任何一条，也不得用上文判据回答拆分问题。

### 判据本体

把一个包内的成员按三条**机械可核验**的轴分类：

- **轴 A（外观）**：成员是否构造 Avalonia 视觉（`open Avalonia*`、控件/视觉元素）。
  构造视觉的成员记为「视觉侧」，其余记为「纯侧」。
- **轴 B（状态归属）**：成员是否持有可变状态（`let mutable`）或发布 `Event<…>`。
  持有者与不持有者分属两侧。
- **轴 C（事件发布）**：成员是否对外发布 `Event<…>`。

**一个包应当被拆分，当且仅当同时满足：**

1. **轴 A 上存在一条可行的切线**（包内同时存在视觉侧与纯侧成员）；
2. **闸门 (i)：没有任何包内引用边横跨切线**。
   「包内引用边」= 成员 A 的源文件里出现了成员 B 声明的顶层类型/模块名
   （`type`/`module` 顶层声明，排除 `private`/`internal` 等关键字造成的词面误报）。
   横跨切线的边说明这两侧是**同一条职责**，拆开即断边，故不可拆；
3. **闸门 (ii)：拆分不得增加组合者侧的同步步骤或布尔量**
   （根 `AGENTS.md` LEMMA 11：拆职责，不拆条件分支与算式）。
   切开后组合者仍须一次装配、一次投影，不得因为拆包而多出「先查 A 再查 B」的分支。

### 轴 B 与轴 C 的实际地位

- **轴 B 从不独立触发拆分。** 逐包扫描 45 个多成员包，**没有**任何包存在
  「持有状态的成员 ↔ 不持有状态的成员」这条横跨的引用边；
  凡是存在状态落差的地方（`ApprovalPanel`、`AttachmentTray`、`BatchBar`、
  `ChatEmpty`、`ModelSwitchPanel`、`DevicesPane`），落差两侧都同时落在
  轴 A 的同一条切线上。也就是说轴 B 要么与轴 A 重合，要么不产生切分点。
- **轴 C ⊂ 轴 B。** 发布 `Event<…>` 的成员必然持有可变状态，
  故轴 C 不提供独立于轴 B 的额外信息。

因此本轮真正起作用的是**轴 A + 闸门 (i)**；轴 B/C 的作用是**排除**
「外观一致但状态归属分裂」的伪拆分点（例如 `Conversations`、`HostCatalog`、
`JsonSeq`、`Wire`、`WorkspaceState`：它们全是纯成员，状态落差处都有横跨引用边）。

### 与上文判据的关系

| | 上文《判据》/《依赖闭包判据》 | 本节《包内拆分判据》 |
|---|---|---|
| 回答的问题 | 两个**包**能不能合并 | 一个**包内部**该不该拆 |
| 方向 | 减少包数 | 增加包数 |
| 依据 | 生产消费者集合、外部依赖、Wanxiang2 依赖 | 外观 / 状态归属 / 事件发布 + 两条闸门 |
| 本轮结果 | 已执行 8 组合并，`libs` 118 → 109 | 本轮执行 7 组拆分，`libs` 109 → **116** |

**不得**用上文的消费者集合判据来回答拆分问题：两个成员的消费者集合相同，
只是说它们该合并，不是说该拆开。

### 逐包评估（全部 45 个多成员包）

「视觉/纯」按轴 A 统计；「包内引用边」按上文闸门 (i) 的定义统计。

| 包 | 成员数 | 外观切（轴 A） | 状态归属切（轴 B） | 包内引用边 | 结论 |
|---|---|---|---|---|---|
| `ActionStrip` | 10 | 全视觉 10，无纯成员 | 无切分点（见理由） | 成员间有 4 条引用边，均在同侧 | **整包保留**：全视觉，外观轴不产生切分点；外观簇内部还有 4 条引用边（`FoldHeader`→`ThinkingBlock`、`RevealPane`→`ThinkingBlock`、`RevealPane`→`UserBubble`、`AttachmentChip`→`UserBubble`），拆开即断边。 |
| `Agui` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`Capabilities` 静而 `TolerantReader` 动，但两者之间没有引用边，轴 B 无切分点。 |
| `Anthropic` | 4 | 全纯 4，无视觉成员 | 无切分点（见理由） | 成员间有 2 条引用边，均在同侧 | **整包保留**：全纯；`AnthropicRequest`→`AnthropicStream`、`GeminiRequest`→`GeminiStream` 是请求/响应的同一条链，跨边引用即一条职责。 |
| `AppConfig` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间有 1 条引用边，均在同侧 | **整包保留**：全纯；`ConfigModel`→`TomlCodec` 是同一条配置读写链。 |
| `ApprovalPanel` | 2 | 可切：视觉 1 / 纯 1 | 与外观切重合，不额外要求 | 无跨切引用边 | **拆**：新包 `ApprovalCardText`。视觉成员只有 `ApprovalPanel.fs`；纯成员 `ApprovalCardText.fs` 只出文案，无包内引用边；`ApprovalDesk` 同时声明两个包即可。 |
| `AttachmentAudit` | 6 | 全纯 6，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；六个成员同属一次附件审计快照的各段检查，无视觉成员可切。 |
| `AttachmentBlobs` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`AttachmentBlobs`/`MediaSniff` 同一职责。 |
| `AttachmentTray` | 4 | 可切：视觉 3 / 纯 1 | 与外观切重合，不额外要求 | 无跨切引用边 | **拆**：新包 `ComposerSendState`。视觉 3 个与纯成员 `ComposerSendState` 之间没有任何引用边；同名的 `ComposerAttachment` 只是词面重合，是误报。 |
| `BatchBar` | 11 | 可切：视觉 6 / 纯 5 | 与外观切重合，不额外要求 | 无跨切引用边 | **拆**：新包 `RowFocus`。视觉 6 个与纯 5 个（`RowFocus`/`SearchHit`/`SearchSegments`/`SearchSession`/`SidebarSelection`）之间没有引用边；`AppShell` 也用到 `SearchHit.parse`，须同时声明新包。 |
| `BlockDiff` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`BlockDiff`/`LinkChunks` 同一职责。 |
| `BoundedCache` | 3 | 全纯 3，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`BoundedCache`/`HighlightQueue`/`HljsHtml` 同属高亮缓存一条链。 |
| `BoundedWait` | 4 | 全纯 4，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`BoundedWait`/`HostHealth` 与 `OnboardingRules`/`ProviderPresets` 之间无跨边引用，状态切分无切分点。 |
| `BrandMark` | 2 | 全视觉 2，无纯成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全视觉，外观轴不产生切分点。 |
| `CardBits` | 9 | 全纯 9，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`CountdownTicker`/`ToolErrorSummary` 有状态而其余无，但成员之间没有引用边，轴 B 无切分点。 |
| `ChangeWatch` | 4 | 全纯 4，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；四个成员同属一条「变更监视—延迟显影—重连—启动追踪」观察链，状态切分无切分点。 |
| `ChatEmpty` | 6 | 可切：视觉 4 / 纯 2 | 与外观切重合，不额外要求 | 无跨切引用边 | **拆**：新包 `BreathPhase`。视觉 4 个与纯 2 个（`ChatEmptyText`/`ScrollFollow`）之间没有引用边；`ChatEmpty` 内部看似的三条边是 `module private` 关键字误报。 |
| `ChatHeader` | 3 | 可切：视觉 1 / 纯 2 | 与外观切重合，不额外要求 | 无跨切引用边 | **拆**：新包 `BreadcrumbAction`。视觉 1 个 + 纯 2 个（`BreadcrumbAction`/`RevealFlash`），没有引用边。 |
| `CliArgs` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`CliArgs`/`RunModes` 同一职责。 |
| `ClientToken` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`ClientToken`/`DesktopClient` 同一职责。 |
| `ColorBlend` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`ColorBlend`/`ThemeTween` 同一职责。 |
| `CommitTimeline` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`CommitTimeline`/`TextSocket` 同一职责。 |
| `ConfigFeedback` | 4 | 全纯 4，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`ConfigFeedback` 静而 `DownloadBuffers`/`HostRequests`/`UploadFacts` 动，但成员之间没有引用边，轴 B 无切分点。 |
| `ConfirmPane` | 6 | 全视觉 6，无纯成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全视觉，外观轴不产生切分点；`ConfirmPane`→`SessionPrefsPane`/`ShortcutHelpPane` 两条引用边横跨成员，拆开即断边。 |
| `ContextTrim` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`ContextTrim`/`UsageTally` 同一职责。 |
| `ContinuableReply` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；两成员同一职责。 |
| `Conversations` | 4 | 全纯 4，无视觉成员 | 无切分点（见理由） | 成员间有 3 条引用边，均在同侧 | **整包保留**：全纯；`Events`→`Conversations`/`EventJson` 是跨状态（静→动）引用边，横切即断边。 |
| `DeviceListJson` | 3 | 全纯 3，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；三个成员同一职责。 |
| `DevicesPane` | 18 | 可切：视觉 7 / 纯 11 | 与外观切重合，不额外要求 | 无跨切引用边 | **拆**：新包 `GenerationDraft`。视觉 7 个成簇（`Ui`↔`GeneralPane`↔各 Pane），纯 11 个与视觉簇之间没有引用边；`TagTone` 只在视觉簇内被用。`SettingsScreen` 用满 11 个纯成员，新包不会成为死包。 |
| `DraftStore` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`DraftBackend`/`DraftStore` 同一职责。 |
| `ExportSheet` | 7 | 可切：视觉 2 / 纯 5 | 闸门 (i) 不成立（见理由） | 跨切引用边 1 条：ImportSheet→ImportView | **整包保留**：**边界，本轮不拆**。视觉 2（`ExportSheet`/`ImportSheet`）与纯 5 之间存在跨切引用边 `ImportSheet`→`ImportView`：`ImportGate` 判别联合在 `ImportSheet.fs` 与 `ImportView.fs` 各声明一份，`compose/TransferDesk/ImportDesk.fs:34-36` 负责互映。拆开必须先解耦这两份 `ImportGate`（要动判别联合的位置，不只是移动文件），判据闸门 (i) 不成立。 |
| `FailureLimiter` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`FailureLimiter`/`PairingCode` 同一职责。 |
| `FocusOrigin` | 2 | 全视觉 2，无纯成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全视觉，外观轴不产生切分点。 |
| `FuzzyMatch` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`FuzzyMatch`/`PinyinInitials` 同一职责。 |
| `HljsCatalog` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`HljsCatalog`/`RichScripts` 同一职责。 |
| `HostCatalog` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间有 1 条引用边，均在同侧 | **整包保留**：全纯；`ModelVision`→`HostCatalog` 是跨状态引用边，横切即断边。 |
| `HttpRetry` | 3 | 全纯 3，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`HttpRetry`/`JsonPick`/`Sse` 同属一条重试传输链，无视觉成员可切。 |
| `JsonRpc` | 4 | 全纯 4，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；四个成员同属一条 JSON-RPC 传输实现。 |
| `JsonSeq` | 7 | 全纯 7，无视觉成员 | 无切分点（见理由） | 成员间有 8 条引用边，均在同侧 | **整包保留**：全纯；`Types`/`Ledger`/`Frame`/`CheckpointJson`/`Envelope` 之间多条跨状态引用边，横切即断边。 |
| `ListenAddress` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`ListenAddress`/`WsHandshake` 同一职责。 |
| `LocalHostGate` | 4 | 全纯 4，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`LocalHostGate`/`ShutdownSignals` 与 `PwaAssets`/`SingleInstance` 之间无跨边引用，状态切分无切分点。 |
| `MediaContent` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`MediaContent`/`MessageSerde` 同一职责。 |
| `MessageText` | 2 | 全纯 2，无视觉成员 | 无切分点（见理由） | 成员间无引用边 | **整包保留**：全纯；`MessageText`/`TextMatch` 同一职责。 |
| `ModelSwitchPanel` | 2 | 可切：视觉 1 / 纯 1 | 与外观切重合，不额外要求 | 无跨切引用边 | **拆**：新包 `ModelPickerList`。视觉 1 个 + 纯 1 个，没有引用边。 |
| `Wire` | 6 | 全纯 6，无视觉成员 | 无切分点（见理由） | 成员间有 10 条引用边，均在同侧 | **整包保留**：全纯；`Types`/`SessionConfig`/`WireCommands`/`WireEvent`/`WireCodec`/`RateLimit` 之间多条跨状态引用边，横切即断边。 |
| `WorkspaceState` | 11 | 全纯 11，无视觉成员 | 无切分点（见理由） | 成员间有 12 条引用边，均在同侧 | **整包保留**：全纯；`Types`/`Model`/`Project`/`Update`/`ViewState`/`RevealTarget` 之间多条跨状态引用边，横切即断边；本包还是唯一登记的兄弟依赖例外。 |


### 本轮执行结果（2026-10-21 W7-PKGSPLIT）

按上表，判据明确要求拆的共 **7 组**，逐组一个提交落地（每个提交只改文件位置与
引用声明，不改行为）：

| # | 原包 | 新包 | 迁入成员 | 组合者改动 |
|---|---|---|---|---|
| S1 | `ChatEmpty` | `BreathPhase` | `ChatEmptyText`、`ScrollFollow` | `compose/ChatPane` 同时声明两包 |
| S2 | `BatchBar` | `RowFocus` | `RowFocus`、`SearchHit`、`SearchSegments`、`SearchSession`、`SidebarSelection` | `compose/SidebarDesk`、`compose/AppShell` 同时声明两包 |
| S3 | `ChatHeader` | `BreadcrumbAction` | `BreadcrumbAction`、`RevealFlash` | `compose/ChatScreen` 同时声明两包 |
| S4 | `AttachmentTray` | `ComposerSendState` | `ComposerSendState` | `compose/ComposerDesk` 同时声明两包 |
| S5 | `ApprovalPanel` | `ApprovalCardText` | `ApprovalCardText` | `compose/ApprovalDesk` 同时声明两包 |
| S6 | `ModelSwitchPanel` | `ModelPickerList` | `ModelPickerList` | `compose/ModelSwitchDesk` 同时声明两包 |
| S7 | `DevicesPane` | `GenerationDraft` | `GenerationDraft`、`GenerationForm`、`LocalDraftControls`、`McpForm`、`OnboardingFlow`、`ProbeFailureKind`、`ProbeSlot`、`ProviderForm`、`ProviderProbeText`、`SectionMemory`、`ToolsPolicy` | `compose/SettingsScreen` 同时声明两包 |

**保留不拆的边界包 1 个**：`ExportSheet`——跨切引用边 `ImportSheet`→`ImportView`
使闸门 (i) 不成立（两份 `ImportGate` 判别联合 + `compose/TransferDesk/ImportDesk.fs`
的互映），要解耦得先动判别联合的位置，超出「移动文件」的范围。

其余 37 个多成员包按上表理由整包保留（全纯或全视觉，或状态落差处有横跨引用边）。

**包数：109 → 116**（7 组拆分 +7）。
