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
    不是独立目录，而是 `libs/StartupTrace/LocalHostGate.fsproj`（异名工程，见 `x` 的
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
| 2 | MessageCardView | — | — | CardReconcile, CaretBreath, CountdownTicker, DurationText, GenerationErrorTitle, MessageMenu, MessageTimestamp, ToolArgsPreview, ToolCallState, ToolErrorSummary, ToolOverview |
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
| 17 | ChatScreen | — | — | BreadcrumbAction, MountWindow, RevealFlash |
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
