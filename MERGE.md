# 包合并方案(未执行,待编译解禁后落地)

> 状态:**方案**。本文是 2026-10-19 构建加速调查的产物,合并尚未执行——执行需要搬 162 个库的源码与测试并跑一次全量编译验证,与当时的「禁止编译」约束冲突。判据数据全部来自仓库当前状态的实际扫描,不是估计。

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

## 执行步骤(解禁后)

1. 每组选一个成员名作为新包名(建议取组内最能代表领域的,如第 4 组可叫 `MessageCardParts`);
2. 源文件按文件名字节序并入新包工程;组内无依赖,顺序不敏感;
3. 测试工程合并:各成员的 `Tests.fs`/`Render.fs`/`Samples.fs` 普遍同名,须逐个改名为 `<成员名>.Tests.fs` 等,顶层 `module Tests` 改为 `module <成员名>Tests`(或套上成员命名空间),golden 目录按成员建子目录;
4. 消费者的 `PackageReference` 从组成员替换为新包名,同工程内去重(NU1504);
5. `./x sln` 重新生成;`./x check` 必须通过(文档引用、消费者存在性、无兄弟引用);
6. 全量验证:`./x all`(逐库测试 + pack + compose + consume),PWA 侧 `getAssemblyExports` 的程序集名如有变化须同步 `app/Pwa/wwwroot/main.js`。

## 预期收益

- `libs` 242 → 122,`dotnet pack` 的工程数同比例下降;
- 每少一个包 = 少一次 fsc 进程启动 + 少一份 obj/bin + 少一个 nupkg 的解包与还原;
- 配合已落地的波次 solution 打包(每波一次 restore/pack,而非每包一个 dotnet 进程),pack 的固定开销从 316 次进程级降到约 18 次 solution 级。
