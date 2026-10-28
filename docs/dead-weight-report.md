# 死重清单（dead-weight report）

> **历史快照（采集于 2026-10-22 前后，118 库 / 70 组合者时代）。** 本文件之后未按包合并与画廊补齐逐项重算，正文表格里的名单、行数、消费者数都是那个时点的数据，**不代表当前规模**。
>
> 当前值以命令为准：
>
> - 库数 / 组合者数 / 控件库数：`./x check` 的输出（2026-10-28 领域事实下沉后为 `104 libs with no sibling references; 71 composer(s) using packages only`，控件库 17 个）。
> - 库与组合者的生产 LOC：`find libs -name '*.fs' -not -path '*/tests/*' -not -path '*/obj/*' -not -path '*/bin/*' -exec cat {} + | wc -l`（`compose/` 同理）。
> - 生产消费者数：`grep -rl 'Wanxiang2\.<Name>' --include='*.fsproj' libs compose app | grep -v /tests/`。
>
> 下文「当前规模」与「所有 118 个库」都是采集时点的事实。结论（哪些包是宽接口薄实现、哪些是真正的大头）仍可参考，具体数字请按上面命令重取。

本报告只做数据采集，不修改任何包。统计口径：

- **文件数**：包目录下除 `bin/`、`obj/` 之外的全部文件数（含 `AGENTS.md`、工程文件、`tests/`、`tools/`）。
- **生产 LOC**：`*.fs`（以及 `*.cs`）文件的 `wc -l` 之和，排除 `tests/` 与 `tools/`；`compose/*` 的生产源码按同一口径。全仓只有 `libs/BrowserInterop` 一个包用 C#。
- **生产消费者**：`libs/`、`compose/`、`app/` 下非 `tests/`、非 `tools/` 的工程文件里，引用 `Wanxiang2.<Name>` 的工程个数。
- **UNWIRED.md**：包目录里存在该文件即为「是」（登记为无消费者的例外）。

采集时点规模（见文首历史快照说明）：`libs/` 118 个包，生产源码合计 29896 行；`compose/` 70 个组合者，生产源码合计 19211 行。当前值：`libs/` 104 个包（2026-10-28 将 `ForkLineage`、`ConversationSummary`、`UiPrefs`、`HostCatalog`、`MessageView` 五个库下沉并入 `libs/WorkspaceState` 后由 109 减至 104）；`compose/` 71 个组合者。

## 一、`libs/*` 包清单（按生产 LOC 升序）

| # | 包 | 文件数 | 生产 LOC | 生产消费者 | UNWIRED.md |
| --- | --- | --- | --- | --- | --- |
| 1 | `Canonical` | 5 | 19 | 1 | 否 |
| 2 | `KeyChord` | 6 | 20 | 1 | 否 |
| 3 | `MathBaseline` | 9 | 20 | 1 | 否 |
| 4 | `ThemeFonts` | 31 | 20 | 2 | 否 |
| 5 | `RichBackend` | 5 | 21 | 2 | 否 |
| 6 | `CommandId` | 5 | 23 | 2 | 否 |
| 7 | `ErrorRecovery` | 7 | 23 | 3 | 否 |
| 8 | `UrlSchemeRegistration` | 7 | 23 | 3 | 否 |
| 9 | `PairingHint` | 10 | 26 | 2 | 否 |
| 10 | `ServerErrorRoute` | 8 | 27 | 1 | 否 |
| 11 | `ToolNames` | 5 | 27 | 2 | 否 |
| 12 | `VerifiedCopy` | 5 | 32 | 1 | 否 |
| 13 | `ModelPickerList` | — | — | — | 否（2026-10-21 并入 `ModelSwitchPanel`） |
| 14 | `SharedHttpClients` | 5 | 34 | 1 | 否 |
| 15 | `LinkInbox` | 5 | 35 | 1 | 否 |
| 16 | `PairingCodeInput` | 9 | 35 | 1 | 否 |
| 17 | `AguiMessageMap` | 5 | 37 | 1 | 否 |
| 18 | `AttachmentRefs` | 5 | 37 | 2 | 否 |
| 19 | `ModelSwitcher` | 5 | 38 | 2 | 否 |
| 20 | `DirLock` | 5 | 39 | 2 | 否 |
| 21 | `ClientTomlFile` | 5 | 43 | 2 | 否 |
| 22 | `ImeShield` | 6 | 44 | 2 | 否 |
| 23 | `JintScriptHost` | 5 | 44 | 1 | 否 |
| 24 | `ApprovalCardText` | 5 | 45 | 1 | 否 |
| 25 | `RefreshingPool` | 5 | 46 | 1 | 否 |
| 26 | `AgentHistory` | 5 | 55 | 3 | 否 |
| 27 | `ListenAddress` | 7 | 55 | 1 | 否 |
| 28 | `BreadcrumbAction` | — | — | — | 否（2026-10-21 并入 `ChatHeader`） |
| 29 | `Approvals` | 5 | 57 | 1 | 否 |
| 30 | `InputSense` | 6 | 58 | 1 | 否 |
| 31 | `SandboxFiles` | 5 | 64 | 1 | 否 |
| 32 | `ToolSummary` | 5 | 64 | 4 | 否 |
| 33 | `ClientConnections` | 6 | 66 | 1 | 否 |
| 34 | `ClientToken` | 7 | 66 | 3 | 否 |
| 35 | `ContinuableReply` | 7 | 67 | 1 | 否 |
| 36 | `HljsCatalog` | 65 | 69 | 1 | 否 |
| 37 | `ConnectionIssue` | 5 | 70 | 4 | 否 |
| 38 | `ImageFit` | 6 | 70 | 2 | 否 |
| 39 | `NavigationState` | 5 | 70 | 3 | 否 |
| 40 | `PlainText` | 5 | 72 | 3 | 否 |
| 41 | `MessageText` | 7 | 73 | 1 | 否 |
| 42 | `PayloadFields` | 5 | 74 | 1 | 否 |
| 43 | `CommandFeedback` | 5 | 79 | 3 | 否 |
| 44 | `ContextTrim` | 7 | 79 | 1 | 否 |
| 45 | `PromptHistory` | 9 | 80 | 1 | 否 |
| 46 | `ShortcutKeys` | 6 | 84 | 2 | 否 |
| 47 | `JsonLinesLog` | 5 | 85 | 1 | 否 |
| 48 | `BlockDiff` | 7 | 86 | 1 | 否 |
| 49 | `HttpFailureFacts` | 5 | 86 | 1 | 否 |
| 50 | `ForkLineage` | 5 | 90 | 4 | 否 |
| 51 | `FuzzyMatch` | 7 | 90 | 1 | 否 |
| 52 | `PendingDeletion` | 8 | 90 | 1 | 否 |
| 53 | `MediaTypes` | 5 | 94 | 6 | 否 |
| 54 | `Agui` | 7 | 96 | 5 | 否 |
| 55 | `ColorBlend` | 7 | 96 | 1 | 否 |
| 56 | `ExchangeJump` | 9 | 97 | 2 | 否 |
| 57 | `CopyButton` | 6 | 105 | 3 | 否 |
| 58 | `CliArgs` | 7 | 113 | 2 | 否 |
| 59 | `LocalState` | 5 | 113 | 3 | 否 |
| 60 | `AtomicTextFile` | 5 | 114 | 1 | 否 |
| 61 | `MotionSwitch` | 7 | 115 | 8 | 否 |
| 62 | `BrowserInterop` | 15 | 124 | 3 | 否 |
| 63 | `LocalHostGate` | 10 | 125 | 1 | 否 |
| 64 | `AttachmentAudit` | 15 | 131 | 1 | 否 |
| 65 | `FieldCheck` | 6 | 137 | 2 | 否 |
| 66 | `ModelListing` | 5 | 142 | 1 | 否 |
| 67 | `FailureLimiter` | 7 | 143 | 1 | 否 |
| 68 | `SpacingRamp` | 5 | 149 | 5 | 否 |
| 69 | `BreathPhase` | — | — | — | 否（2026-10-21 并入 `ChatEmpty`） |
| 70 | `DeviceListJson` | 9 | 151 | 1 | 否 |
| 71 | `DevicesModel` | 6 | 157 | 3 | 否 |
| 72 | `ComposerDrafts` | 5 | 165 | 4 | 否 |
| 73 | `ChangeWatch` | 13 | 167 | 3 | 否 |
| 74 | `ModelSwitchPanel` | 6 | 173 | 1 | 否 |
| 75 | `CommitTimeline` | 7 | 176 | 1 | 否 |
| 76 | `AttachmentBlobs` | 7 | 180 | 1 | 否 |
| 77 | `ApprovalPanel` | 6 | 182 | 1 | 否 |
| 78 | `PalettePanel` | 6 | 184 | 1 | 否 |
| 79 | `ConversationSummary` | 5 | 191 | 5 | 否 |
| 80 | `HttpRetry` | 9 | 192 | 1 | 否 |
| 81 | `IconSet` | 8 | 193 | 14 | 否 |
| 82 | `KatexHtml` | 5 | 193 | 1 | 否 |
| 83 | `BrandMark` | 10 | 195 | 1 | 否 |
| 84 | `UiPrefs` | 7 | 200 | 9 | 否 |
| 85 | `BoundedCache` | 9 | 205 | 1 | 否 |
| 86 | `ToastStrip` | 6 | 218 | 5 | 否 |
| 87 | `MarkdownBlocks` | 6 | 243 | 1 | 否 |
| 88 | `ConversationTransfer` | 7 | 251 | 4 | 否 |
| 89 | `PopupMenu` | 6 | 252 | 5 | 否 |
| 90 | `ThemePalette` | 8 | 258 | 12 | 否 |
| 91 | `ConfigFeedback` | 14 | 275 | 3 | 否 |
| 92 | `ComposerSendState` | 9 | 277 | 1 | 否 |
| 93 | `DraftStore` | 11 | 312 | 6 | 否 |
| 94 | `JsonRpc` | 11 | 322 | 1 | 否 |
| 95 | `ExportFileName` | — | — | — | 否（2026-10-21 并入 `ExportSheet`） |
| 96 | `MessageView` | 7 | 334 | 10 | 否 |
| 97 | `MediaContent` | 7 | 361 | 3 | 否 |
| 98 | `ChatHeader` | 6 | 363 | 1 | 否 |
| 99 | `HostCatalog` | 9 | 377 | 6 | 否 |
| 100 | `FocusOrigin` | 10 | 379 | 1 | 否 |
| 101 | `ExportSheet` | 10 | 392 | 1 | 否 |
| 102 | `RowFocus` | 15 | 428 | 1 | 否 |
| 103 | `ThemeMetrics` | 7 | 464 | 12 | 否 |
| 104 | `BoundedWait` | 11 | 467 | 3 | 否 |
| 105 | `JsonSeq` | 14 | 487 | 1 | 否 |
| 106 | `CardBits` | 21 | 498 | 1 | 否 |
| 107 | `ChatEmpty` | 16 | 629 | 1 | 否 |
| 108 | `Conversations` | 8 | 704 | 2 | 否 |
| 109 | `Anthropic` | 9 | 749 | 1 | 否 |
| 110 | `AttachmentTray` | 13 | 819 | 1 | 否 |
| 111 | `GenerationDraft` | — | — | — | 否（2026-10-21 并入 `DevicesPane`） |
| 112 | `ConfirmPane` | 28 | 1068 | 1 | 否 |
| 113 | `BatchBar` | 28 | 1117 | 1 | 否 |
| 114 | `AppConfig` | 6 | 1321 | 13 | 否 |
| 115 | `ActionStrip` | 34 | 1558 | 1 | 否 |
| 116 | `Wire` | 13 | 1862 | 27 | 否 |
| 117 | `WorkspaceState` | 15 | 2095 | 20 | 否 |
| 118 | `DevicesPane` | 29 | 2162 | 1 | 否 |

采集时点：118 个库都有生产消费者，没有 `UNWIRED.md` 登记例外。当前值：109 个库都有生产消费者，`./x check` 的消费者门禁通过。

## 二、`compose/*` 组合者清单（按生产 LOC 升序）

| # | 组合者 | 生产源码文件数 | 生产 LOC | 有 `tests/` 工程 |
| --- | --- | --- | --- | --- |
| 1 | `ExternalLinks` | 1 | 13 | 否 |
| 2 | `RichAssets` | 1 | 17 | 是 |
| 3 | `AgentContent` | 1 | 30 | 是 |
| 4 | `LaunchPlan` | 1 | 30 | 是 |
| 5 | `ConnectIssueText` | 1 | 36 | 是 |
| 6 | `HostLog` | 1 | 37 | 是 |
| 7 | `ModelSwitchDesk` | 1 | 46 | 是 |
| 8 | `RichJint` | 1 | 47 | 是 |
| 9 | `UiPrefsFile` | 1 | 49 | 否 |
| 10 | `RichBrowser` | 1 | 54 | 是 |
| 11 | `PartialReplies` | 1 | 55 | 是 |
| 12 | `DesktopPairing` | 1 | 61 | 是 |
| 13 | `HostStatus` | 1 | 68 | 是 |
| 14 | `CodeTint` | 1 | 70 | 是 |
| 15 | `UrlSchemeRegistrationIo` | 1 | 71 | 否 |
| 16 | `PaletteDesk` | 1 | 72 | 是 |
| 17 | `StoreLaunch` | 1 | 74 | 是 |
| 18 | `CredentialStore` | 1 | 79 | 否 |
| 19 | `PairingDesk` | 1 | 80 | 是 |
| 20 | `ReadingLook` | 1 | 85 | 是 |
| 21 | `ApprovalDesk` | 1 | 99 | 是 |
| 22 | `Search` | 1 | 105 | 是 |
| 23 | `BuiltinTools` | 1 | 106 | 是 |
| 24 | `ConversationList` | 1 | 107 | 是 |
| 25 | `Workspace` | 1 | 114 | 是 |
| 26 | `ConfigStore` | 1 | 116 | 是 |
| 27 | `DesignRhythm` | 2 | 133 | 是 |
| 28 | `Attachments` | 1 | 135 | 是 |
| 29 | `ConversationExport` | 1 | 141 | 是 |
| 30 | `Catalog` | 1 | 142 | 是 |
| 31 | `ProviderTransports` | 2 | 147 | 是 |
| 32 | `ShellDesk` | 1 | 155 | 是 |
| 33 | `ShellCommands` | 1 | 156 | 是 |
| 34 | `ProtocolDispatch` | 1 | 179 | 是 |
| 35 | `AgentRuntime` | 1 | 184 | 是 |
| 36 | `ToolApproval` | 1 | 193 | 是 |
| 37 | `ImageShrink` | 2 | 196 | 是 |
| 38 | `ToolRegistry` | 1 | 199 | 是 |
| 39 | `ProviderFailure` | 1 | 205 | 是 |
| 40 | `ServerModel` | 1 | 211 | 是 |
| 41 | `MathFigure` | 2 | 213 | 是 |
| 42 | `OverlayDesk` | 3 | 214 | 是 |
| 43 | `ConversationRuns` | 1 | 219 | 是 |
| 44 | `CommandPalette` | 2 | 255 | 是 |
| 45 | `AguiWire` | 1 | 262 | 是 |
| 46 | `Outbox` | 1 | 265 | 是 |
| 47 | `ChatPane` | 2 | 271 | 是 |
| 48 | `LedgerStore` | 1 | 276 | 是 |
| 49 | `McpClient` | 1 | 276 | 是 |
| 50 | `WsClient` | 1 | 279 | 是 |
| 51 | `MathTypeset` | 1 | 296 | 是 |
| 52 | `ConfigMutation` | 1 | 301 | 是 |
| 53 | `EventStore` | 3 | 311 | 是 |
| 54 | `ComposerDesk` | 3 | 325 | 是 |
| 55 | `DialogsDesk` | 2 | 331 | 是 |
| 56 | `ProviderCatalog` | 1 | 432 | 是 |
| 57 | `Doctor` | 2 | 463 | 是 |
| 58 | `TransferDesk` | 3 | 463 | 是 |
| 59 | `ChatScreen` | 2 | 565 | 是 |
| 60 | `EntryShells` | 5 | 568 | 是 |
| 61 | `ServerApp` | 1 | 580 | 是 |
| 62 | `SidebarDesk` | 2 | 589 | 是 |
| 63 | `ChatDomain` | 5 | 593 | 是 |
| 64 | `MarkdownView` | 4 | 719 | 是 |
| 65 | `MessageCardView` | 3 | 729 | 是 |
| 66 | `Theme` | 3 | 849 | 是 |
| 67 | `ChatOrchestrator` | 3 | 885 | 是 |
| 68 | `AppShell` | 3 | 928 | 是 |
| 69 | `WsConnection` | 1 | 1094 | 是 |
| 70 | `SettingsScreen` | 9 | 1563 | 是 |

## 三、超过 700 行的文件（应考虑拆分）

| 文件 | 行数 | 位置 |
| --- | --- | --- |
| `compose/WsClient/tests/ClientStateAndWsClientTests.fs` | 1316 | tests |
| `compose/WsConnection/WsConnection.fs` | 1094 | prod |
| `libs/Wire/tests/WireCodecTests.fs` | 1082 | tests |
| `libs/AppConfig/TomlCodec.fs` | 994 | prod |
| `libs/Wire/WireCodec.fs` | 968 | prod |
| `compose/AppShell/AppShell.fs` | 900 | prod |
| `libs/AppConfig/tests/Tests.fs` | 895 | tests |
| `compose/ServerApp/tests/ServerE2eTests.fs` | 870 | tests |
| `compose/ChatOrchestrator/ChatOrchestrator.fs` | 765 | prod |
| `compose/ChatDomain/tests/CoreTests.fs` | 727 | tests |

其中生产源码 5 个、测试 5 个。生产源码里最长的这几个最值得拆分：

- `compose/WsConnection/WsConnection.fs`（1094 行）
- `libs/AppConfig/TomlCodec.fs`（994 行）
- `libs/Wire/WireCodec.fs`（968 行）
- `compose/AppShell/AppShell.fs`（900 行）
- `compose/ChatOrchestrator/ChatOrchestrator.fs`（765 行）

测试文件超长只影响测试可读性，不影响交付边界，优先级低于生产源码。

## 四、少于 40 行的文件（纯转发壳候选）

生产源码共 91 个文件少于 40 行。按行数升序列出，供人工判断是否只是转发壳：

| 文件 | 行数 |
| --- | --- |
| `compose/ChatPane/Looks.fs` | 5 |
| `libs/LocalHostGate/LocalHostGate.fs` | 7 |
| `libs/CardBits/DurationText.fs` | 9 |
| `libs/ColorBlend/ColorBlend.fs` | 9 |
| `compose/ComposerDesk/Dyn.fs` | 12 |
| `compose/ExternalLinks/ExternalLinks.fs` | 13 |
| `compose/AppShell/KeyShortcut.fs` | 14 |
| `compose/AppShell/UnansweredReply.fs` | 14 |
| `libs/AttachmentAudit/McpCheck.fs` | 14 |
| `libs/BrowserInterop/BrowserImage.cs` | 14 |
| `libs/BrowserInterop/BrowserLocalStore.cs` | 15 |
| `libs/CardBits/GenerationErrorTitle.fs` | 15 |
| `libs/WorkspaceState/DraftSummaryText.fs` | 15 |
| `libs/CardBits/MessageTimestamp.fs` | 16 |
| `compose/RichAssets/RichAssets.fs` | 17 |
| `libs/ChangeWatch/ChangeWatch.fs` | 17 |
| `libs/AttachmentAudit/AttachmentAudit.fs` | 18 |
| `libs/AttachmentAudit/ProviderCheck.fs` | 18 |
| `libs/FocusOrigin/FocusOrigin.fs` | 18 |
| `libs/AttachmentAudit/SnapshotCheck.fs` | 19 |
| `libs/BrowserInterop/BrowserRich.cs` | 19 |
| `libs/Canonical/Canonical.fs` | 19 |
| `libs/WorkspaceState/RevealTarget.fs` | 19 |
| `compose/EntryShells/SharedTheme.fs` | 20 |
| `libs/BrowserInterop/BrowserDrafts.cs` | 20 |
| `libs/KeyChord/KeyChord.fs` | 20 |
| `libs/ListenAddress/ListenAddress.fs` | 20 |
| `libs/MathBaseline/MathBaseline.fs` | 20 |
| `libs/ThemeFonts/ThemeFonts.fs` | 20 |
| `libs/WorkspaceState/OlderPageLoad.fs` | 20 |
| `libs/RichBackend/RichBackend.fs` | 21 |
| `libs/BrowserInterop/BrowserCredentials.cs` | 23 |
| `libs/CommandId/CommandId.fs` | 23 |
| `libs/ErrorRecovery/ErrorRecovery.fs` | 23 |
| `libs/UrlSchemeRegistration/UrlSchemeRegistration.fs` | 23 |
| `compose/MarkdownView/ActionSpot.fs` | 24 |
| `libs/CardBits/ToolCallState.fs` | 24 |
| `libs/DevicesPane/LocalDraftControls.fs` | 24 |
| `libs/HljsCatalog/RichScripts.fs` | 24 |
| `libs/LocalHostGate/ShutdownSignals.fs` | 24 |
| `compose/OverlayDesk/ToastDwell.fs` | 25 |
| `libs/LocalHostGate/PwaAssets.fs` | 25 |
| `libs/WorkspaceState/ReconnectBackoff.fs` | 25 |
| `libs/DevicesPane/ToolsPolicy.fs` | 26 |
| `libs/PairingHint/PairingHint.fs` | 26 |
| `libs/AttachmentAudit/SecretText.fs` | 27 |
| `libs/DeviceListJson/DeviceRules.fs` | 27 |
| `libs/ServerErrorRoute/ServerErrorRoute.fs` | 27 |
| `libs/ToolNames/ToolNames.fs` | 27 |
| `compose/ChatDomain/Projection.fs` | 28 |
| `compose/CommandPalette/PaletteRecentFile.fs` | 28 |
| `compose/EventStore/DataPaths.fs` | 28 |
| `libs/Agui/Capabilities.fs` | 28 |
| `libs/ChatHeader/BreadcrumbAction.fs` | 28 |
| `libs/ChatHeader/RevealFlash.fs` | 28 |
| `libs/ContinuableReply/ContinuableReply.fs` | 28 |
| `libs/ExportSheet/ExportFileName.fs` | 28 |
| `libs/WorkspaceState/ForkAnchor.fs` | 28 |
| `compose/OverlayDesk/ToastQueue.fs` | 29 |
| `compose/SettingsScreen/Hooks.fs` | 29 |
| `libs/CliArgs/RunModes.fs` | 29 |
| `compose/AgentContent/AttachmentContent.fs` | 30 |
| `compose/LaunchPlan/LaunchPlan.fs` | 30 |
| `libs/CardBits/CountdownTicker.fs` | 31 |
| `libs/BoundedCache/HighlightQueue.fs` | 32 |
| `libs/VerifiedCopy/VerifiedCopy.fs` | 32 |
| `libs/BrowserInterop/BrowserPage.cs` | 33 |
| `libs/ClientToken/ClientToken.fs` | 33 |
| `libs/ClientToken/DesktopClient.fs` | 33 |
| `libs/ContextTrim/UsageTally.fs` | 34 |
| `libs/FuzzyMatch/FuzzyMatch.fs` | 34 |
| `libs/ModelSwitchPanel/ModelPickerList.fs` | 34 |
| `libs/SharedHttpClients/SharedHttpClients.fs` | 34 |
| `compose/MarkdownView/MarkdownStream.fs` | 35 |
| `libs/AttachmentAudit/HostProbe.fs` | 35 |
| `libs/LinkInbox/LinkInbox.fs` | 35 |
| `libs/ListenAddress/WsHandshake.fs` | 35 |
| `libs/MessageText/MessageText.fs` | 35 |
| `libs/PairingCodeInput/PairingCodeInput.fs` | 35 |
| `compose/ConnectIssueText/ConnectIssueText.fs` | 36 |
| `libs/DeviceListJson/DeviceListJson.fs` | 36 |
| `compose/HostLog/Stderr.fs` | 37 |
| `libs/AttachmentRefs/AttachmentRefs.fs` | 37 |
| `compose/ImageShrink/ImageShrink.fs` | 38 |
| `libs/MessageText/TextMatch.fs` | 38 |
| `libs/ModelSwitcher/ModelSwitcher.fs` | 38 |
| `libs/AttachmentBlobs/MediaSniff.fs` | 39 |
| `libs/BlockDiff/BlockDiff.fs` | 39 |
| `libs/ContinuableReply/TitleText.fs` | 39 |
| `libs/DirLock/DirLock.fs` | 39 |

`tools/` 下另有 9 个少于 40 行的文件，属旧代码留存副本，不在生产路径上：

| 文件 | 行数 |
| --- | --- |
| `compose/ConnectIssueText/tools/OldConnectIssueText.fs` | 5 |
| `libs/PairingHint/tools/OldPairingHint.fs` | 6 |
| `compose/MathFigure/tools/figuregold/Shims.fs` | 8 |
| `libs/MathBaseline/tools/OldMathBaseline.fs` | 14 |
| `libs/PendingDeletion/tools/OldPendingDeletion.fs` | 21 |
| `libs/PairingHint/tools/OldPairingDialogs.fs` | 25 |
| `libs/ServerErrorRoute/tools/OldServerErrorRoute.fs` | 27 |
| `libs/PairingCodeInput/tools/OldPairingCodeInput.fs` | 28 |
| （已随 2026-10-21 金样脚手架清理波删除） | — |

## 五、读法

- 第一张表里生产 LOC 很低而消费者很多（`Wire`、`ThemePalette`、`WorkspaceState` 等）的包，是「宽接口 + 薄实现」的骨架，删除风险低但拆分收益也低。
- 生产 LOC 最大的几个包（`ActionStrip` 等控件合并包）是真正的大头，其中已经出现过单文件拆分（`CardBits` 按职责分成多个小文件），是值得参照的形态。
- 第四张表里的短文件多数是 `type X = ...` 的一行类型别名或小记录，属于「纯声明」而非「纯转发」；转发壳的判定要看它是否只是把参数原样传给另一个包。

