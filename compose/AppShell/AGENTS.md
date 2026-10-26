# AppShell（组合）

旧宿主 `src/Wanxiang.UI/AppShell.fs`（旧仓库 bfdda85 删除，本机退役检出 `/tmp/wx-old`）的编排核心。命名空间沿用 `Wanxiang.UI`（与 `ShellCommands`、`ProtocolDispatch` 同模式），包 id 是 `Wanxiang2.AppShell`；`app/Desktop` 与 `app/Pwa` 只经交付包消费它。根规则见项目根目录 AGENTS.md。

公开面：
- `ShellComposer(ports: ShellPorts)`：连接与认证步骤序列、重连调度、视口轮询处理、协议事件界面面、对话操作、附件流程、导入导出、草稿策略、视图驱动（`Render`）、快捷键与命令面板分派入口。成员按职责分组：连接（`ShowConnectDialog` / `ResumeReconnectNow`）、侧栏（`NewConversation` / `OpenConversation` / `RenameConversation` / `SetPinned` / `SetPinnedMany` …）、对话（`RenameTitle` / `Regenerate` / `RegenerateLatest` / `EditAndFork` / `DeleteMessage` / `SwitchVersion` / `ContinueGeneration` …）、附件（`UploadOwner` / `AttachFiles`）、转移（`ImportConversation` / `ExportConversation`）、设置与主题（`ShowSettings` / `SavePrefs` / `OnSystemThemeChanged` / `ApplyPreset` / `SaveConversationAsPreset`）、动作记录工厂（`SidebarActions` / `CardActions` / `ChatActions` / `ComposerActions` / `SettingsHooks` / `OnboardingHooks` / `WorkspaceHooks`）、生命周期（`Build` / `AutoConnect` / `FlushDrafts` …
- `ShellPorts`：入口壳注入的端口——连接与发送（`Connect` / `SendAuth` / `Send` / `SendCommand` / `SendAtGeneration`）、计时（`After` / `Every`）、**当前时间**（`Now`：组合者不直接读系统时钟，到期删除这类按时间推进的行为要能被假时钟确定性地测；生产入口给 `DateTimeOffset.UtcNow`）、线程（`Post`）、提示条（`Toast` / `ToastUndo`）、IO（`CopyToClipboard` / `OpenLink` / `SaveDownload` / `ApplyDraftWrite` / `PrefsLoad` / `PrefsSave` / `TryLoadClientToml` / `TryLoadBrowserConnection` / `SaveBrowserConnection`）、浏览器（`BrowserViewportWidth` / `BrowserResumeEpoch` / `BrowserPageUrl` / `BrowserConversationSucceeded` / `BrowserAddHidden`）、外观（`SystemPrefersDark` / `DefaultServerUrl` / `ApplyTheme` / `SetReducedMotion` / `SetCodeWrap`）、焦点（`CaptureFocus` / `IsEditingText`）、连接事实（`ConnectionGeneration` / `IsConnected` / `SubscribeHostEvents` / `SubscribeClosed`）、窗口与身份（`CloseWindow` / `ClientName`）、缩略图（`MakeThumbnail`）、视图组装（`BuildViews`）。
- `ShellViews`：入口壳构造并交回的视图集合——九个 Desk（`Sidebar` / `Chat` / `Composer` / `Settings` / `Onboarding` / `Workspace` / `Approval` / `Palette` / `ModelSwitch`）、浮层 `Overlay`、对话框 `Dialogs`、导出 / 导入面板工厂 `NewExportDesk` / `NewImportDesk`，外加宿主显隐与设置分区驱动函数。
- `KeyShortcut`：Avalonia 按键事件 → `KeyChord` 四字段 → `ShortcutAction` 的换算与分派（迁自旧 `Controls/KeyShortcut.fs`）。
- `UnansweredReply`：末尾用户消息久无回复时补出的失败卡形状（迁自旧 `Model/UnansweredReply.fs`）。

引用的包：`Wanxiang2.ProtocolDispatch`、`Wanxiang2.ShellCommands`、`Wanxiang2.Catalog`、`Wanxiang2.ConversationRuns`、`Wanxiang2.Outbox`、`Wanxiang2.ConversationList`、`Wanxiang2.TransferDesk`、`Wanxiang2.DialogsDesk`、`Wanxiang2.OverlayDesk`、`Wanxiang2.SidebarDesk`、`Wanxiang2.ChatScreen`、`Wanxiang2.ComposerDesk`、`Wanxiang2.SettingsScreen`、`Wanxiang2.Workspace`、`Wanxiang2.ApprovalDesk`、`Wanxiang2.PaletteDesk`、`Wanxiang2.ModelSwitchDesk`、`Wanxiang2.MessageCardView`、`Wanxiang2.Theme`、`Wanxiang2.ImageShrink`…（完整清单见 `AppShell.fsproj`；库侧含 `Wanxiang2.Wire`、`Wanxiang2.WsClient`、`Wanxiang2.ConversationTransfer`、`Wanxiang2.ConfigFeedback`、`Wanxiang2.MediaTypes`、`Wanxiang2.ConfigFeedback`、`Wanxiang2.HostFeatures` 等）。

本组合者不持 DispatcherTimer、不做文件 IO、不做 Browser 调用、不构造 Avalonia 控件：计时器、对话框、剪贴板、文件选择与保存、主题应用、焦点捕获、窗口关闭全部经 `ShellPorts` 由 `app/Desktop`、`app/Pwa` 注入。

# LEMMA
1. **行为逐条对照旧 `AppShell.fs`，架构按端口切开。** [根 AXIOM: 1, 4]
    - 推理：旧壳把编排、IO、计时、控件构造混在一个类里；本组合者只保留编排与状态，其余经端口留给入口。用户可见文案、命令载荷、状态迁移次序不改，逐条以旧文件为权威（对照表见「拆分与复核」）。
2. **组合者拥有协作，参与者拥有自身。** [根 LEMMA: 4]
    - 推理：协议事件模型面归 `ProtocolDispatch`、快捷键与命令面板分派归 `ShellCommands`、删除即撤销归 `WorkspaceModel.Update`、草稿落盘归端口、命令与配置回执归 `CommandFeedback` / `ConfigFeedback`、传输面板归 `TransferDesk`、对话框归 `DialogsDesk`。本组合者只发消息、收结果、安排下一步。
3. **视图事件在组合者接线，入口壳只装配。** [根 AXIOM: 3, 5]
    - 推理：`Build()` 里接的设置页 / 侧栏 / 命令面板事件（`CloseRequested`、`Devices.*`、`SearchHandler`、`Palette.Run` …）都需要编排判断；入口壳只把 Desk 建出来、把动作记录接到组合者的工厂方法上。`Health.CheckRequested` 由 `GeneralDesk` 订阅并转成 `SettingsHooks.RequestDoctorCheck`，本组合者在 `SettingsHooks()` 里给出真实实现（发 `doctor.request`）。
4. **入口壳提供副作用端口，组合者不猜平台。** [根 AXIOM: 3]
    - 推理：`OperatingSystem.IsBrowser()` 的分支在旧壳里散落多处；新架构把平台差异收进端口（选择器、剪贴板、存储、深色偏好、视口），组合者只调用。

# W7-B3 接线补全（事实归属与引导分支）
- **谱系只有一个来源**：`WorkspaceModel.Lineage` 出自对话列表快照，`ExportConversation` 直接读模型；
  侧栏经 `SidebarVS.Lineage` 投影消费（旧的 `SidebarDesk.SlotLineage` 旁路插槽已删）。
- **引导分支**：`ReprojectInternal` 里，目录就绪但 `Catalog.isReady` 为假（没有可用模型服务）时，
  聊天区嵌引导卡（`ChatPane.ShowEmbedded` + `OnboardingDesk.StartDiscovery`），
  卡片的三项在途事实按单一入口 `OnboardingDesk.Render(SettingsOnboardingVS)` 投影回去——事实归 `WorkspaceModel.Onboarding`。
- **引导交接**：目录里出现可用模型服务时，`OnboardingDesk.TakeConnected` 取走卡片刚接上的 (id, 模型)，
  预填第一句话并提示（旧 `CompleteOnboarding`）；卡片写入回执后重投影一次，交接才接得住。
- **引导卡的回报端口**：`OnboardingHooks.Report` 把卡片里的选择/发现/建议回报成模型消息（`User(Onboarding(...))`），
  事实进模型，宿主再投影回卡片——卡片不留副本。
- **定位目标**：搜索命中 / 消息链接 → `SidebarUserMsg.SearchHitSelect` → 模型登记定位目标 →
  对话快照到达且目标消息已加载时发 `Effect.ScrollChat (ScrollCommand.ToExchange 消息 id)`，
  入口壳据此滚动并短暂高亮（统一 Effect，不散 Post）。

# 第五批：偏好与退出草稿契约

- 构建视图后，从宿主读取一次偏好并派发 `PrefsLoaded`；模型是偏好的唯一事实源，主题、减弱动效与代码折行须应用到宿主，加载不反写偏好。
- 输入文本由状态树拥有；退出或页面隐藏时取消草稿延时并立即保存当前文本，不能让旧延时覆盖退出时的文本。关闭持久化时不写草稿；销毁时清理定时器。
- 桌面关窗与浏览器隐藏调用同一个组合者 `FlushDrafts` 入口。浏览器写入仍是异步端口，不承诺强制终止页面时 IndexedDB 一定完成。
- 验证：`tests/ComposerTests.fs` 四条「第五批」用例覆盖构建时偏好投影与宿主扇出、800 ms 窗口内退出保存最新文本、关闭持久化不写入、退出以当前模型而非旧待保存文本为准。修改前两条新增回归失败，修复后 AppShell 40/40 通过；`./x all`（109 库、67 组合者及仓库外消费）、`./x app`、桌面冒烟通过。尚未单独断言隐藏事件接线、重复刷新和计时器取消；浏览器强制终止时的异步存储完成不在保证范围内。

# 命令面板与主题接线契约

- 面板选中条目后，由 `Build()` 订阅 `Palette.Run`，交给 `ShellCommands.RunPaletteItem` 执行并回写最近使用；不由入口壳补接。
- 主题切换读取 `WorkspaceModel.Prefs.theme`，经 `PrefsChanged` 更新状态树，再由既有副作用保存与应用。连续切换必须按当前主题推进，不能始终从跟随系统开始。
- 面板候选的对话、预设、归档偏好与主机能力来自当前模型，不以固定空值冒充事实。
- 验证（2026-10-23，本机 macOS ARM64）：`tests/ComposerTests.fs` 新增「主题快捷键从当前偏好推进并回写模型」与「命令面板真实回车执行主题并登记最近使用」。修改前分别复现主题停留原值、设置分区为空抛异常及执行后最近使用仍为空；修复后本工程 43/43 通过。面板用例等待文本事件处理后，向真实搜索框发回车并断言最近使用与主题模型；尚未单独覆盖预设、对话与能力变化的候选投影。后续验证（2026-10-24）：macOS ARM64 干净发布 PWA 冒烟 28 条全过，命令执行、最近使用、设置偏好保存、Esc 回焦、主题与草稿往返均在列；中文截图识别在系统 Vision 与 Tesseract 两条路径分别通过，并拒绝缺字方框反例。`tests/ComposerTests.fs`「外观原生开关修改模型并保存偏好」验证真实开关事件进入模型及保存端口。Tesseract 的菜单验收只要求命中该菜单真有的标签（消息卡菜单「复制消息」等、文本浮层「剪切 / 复制 / 粘贴」各命中两项），不宣称逐字覆盖全部菜单；Linux 宿主真实冒烟仍待验证。
- 冒烟判据修正（2026-10-24）：原「消息卡右键菜单」用例靠固定坐标盲扫，实际一直点到输入框的 Fluent 文本浮层，等于没验过消息卡菜单；收紧为按识别出的菜单文案区分两类菜单后暴露。现在先用截图文字包围盒定位侧栏对话标题并点开、用正文「你好」确认已打开，再用主内容区正文的包围盒右键，并要求识别到「复制消息／编辑并分叉／复制消息链接／删除这条消息」。同类修正：「侧栏对话可打开」原判据只看主内容区有像素变化，空态页面同样有变化，也会误判为通过。定位锚点的识别先把画布放大 2 倍再转文字：原始截图里侧栏字号太小，Tesseract 会把「新建对话」读成 `EHS`。

# SPEC 7 拦截与旅程证据（2026-10-24）

- `ShellCommands.HandleShortcut` 的真实语义（`compose/ShellCommands/ShellCommands.fs:123`）：`Escape` 永远先走退栈（`OverlayEscape` → 设置页 → 抽屉 → 停止生成），`NoShortcut` 返回 false，之后才是拦截门 `| _ when surface.IsDialogOpen() || surface.IsPopupOpen() -> false`，剩下的动作各自调 surface 并返回 true。`surface.IsDialogOpen` / `IsPopupOpen` 直接读真实控件层（`AppShell.fs` 的 `v.Overlay.Layer.*`），不再查 `model.Overlay` 那个已删的死路径。
- 主题轮转的落盘口径：`CycleTheme` → `surface.SaveTheme` → `User(Settings PrefsChanged)` → `Effect.PrefsSave`；**不产生** `Effect.ApplyTheme`（那条只在连接/偏好回读路径 `Update.fs:741` 出现）。拦截用例因此断 `PrefsSave` 计数，不断 `ApplyTheme`。
- 新增 `compose/AppShell/tests/ShortcutInterceptTests.fs` 四条用例（走 `rig.Shell.Commands.HandleShortcut` 真实入口 + 真实控件层状态，禁塞 `model.Overlay` 假值）：`对话框打开时快捷键被拦，Escape 退栈后恢复可用`（`Dialogs.Confirm` 真实开框）、`真实菜单（OverlayDesk.ShowMenu）打开时快捷键被拦，Escape 退栈后恢复可用`（生产 ChatScreen 菜单同一条 `ShowMenu` 入口）、`原生 ContextMenu（独立弹层宿主）打开时快捷键被拦，Escape 交还原生关闭契约`、`原生 MenuFlyout（独立弹层宿主）打开时快捷键被拦，Escape 关闭后恢复可用`（关闭后 `ToggleTheme` 真的翻转主题、`OpenSettings` 真的打开设置页、`Escape` 再收回）。原生两条依赖 `libs/FocusOrigin/OverlayLayer.fs` 的原生菜单计数与退栈修复；修复前用修复前的交付包跑，这两条失败，修复后 AppShell 50/50 通过。
- 旅程 1（`ComposerTests.fs` 的 `旅程 1：首次配对到就绪`）补到「配对 → 首发」：在统一 `rig.Fake.RecordedEffects` 序列里断 `Effect.SendCommand CreateConversation` < `Effect.SendCommand SendUserMessage` < `Effect.SendWire(ObserveConversation)` 且三者同一个 `conversationId`、命令载荷含用户原文；再投递带同一原文的 `ConversationSnapshot`，断 `WorkspacePhase.Active` 的消息正文与聊天投影文本都等于该原文（不是只数一条消息）。报告 `docs/evidence/2026-10-23-handoff/journeys-report.md` 无需回退——选的是「补用例」而不是「改陈述」。

# SPEC
1. [LEMMA: 1] 组合者成员与旧实现的对照：每个公开成员都能在旧文件里找到同名或同义的来源（连接与认证 `old:586-637`、事件界面面 `old:1552-1719`、侧栏动作 `old:1723-1833`、聊天动作 `old:1845-1925`、附件 `old:1138-1317`、转移 `old:1338-1392`、设置与预设 `old:2006-2130`、主题与偏好 `old:418-536`、快捷键与命令面板 `old:1416-1504`）。
2. [LEMMA: 3] 视图事件在 `Build()` 内接线，入口壳构造 Desk 时只调用动作记录工厂（`SidebarActions` / `CardActions` / `ChatActions` / `ComposerActions` / `SettingsHooks` / `OnboardingHooks` / `WorkspaceHooks`）。
3. [LEMMA: 4] `ShellSurface` 里不再有空操作：`OverlayEscape` / `IsDialogOpen` / `IsPopupOpen` / `IsSettingsOpen` / `IsDrawerOpen` / `CloseDrawer` / `FocusSearch` / `OpenModelPicker` / `JumpExchange` / `ScrollToBeginning` / `ScrollToEnd` / `CopyLastReply` / `RegenerateLast` / `TryUndo` / `FocusToast` / `ShowShortcuts` / `ApplyPreset` / `SavePreset` / `Fork` / `CloseWindow` / `SaveTheme` 全部接真实实现。
4. [LEMMA: 1] 文案不改：附件、保存、剪贴板、模型选择器、导入导出、重连与配对的用户可见字符串与旧实现逐字相同（来源：`Wanxiang2.ConfigFeedback`、`Wanxiang2.MediaTypes`、`Wanxiang2.ConnectIssueText`、`Wanxiang2.PairingHint`、`compose/TransferDesk`）。
5. [LEMMA: 1] headless 测试覆盖宿主桥与装配规则：`compose/AppShell/tests/ComposerTests.fs` 用假端口（`tests/Rig.fs`）钉住——未连接时的导入导出提示、`RequireFeature` 拦截与旧主机放行、附件归属过期与上传事件序列（含缩略图端口）、空文件与超大文件校验、`SavePrefs` 扇出、系统深色只在跟随系统时重应用、侧栏搜索与消息链接分派、命令回执的提交与未发出两态、设置焦点归还、设置页打开期间禁用底层工作区并圈定 Tab 焦点、配置回执提示、侧栏折叠落盘、断开连接的清理与重连调度；`Tests.fs` 钉住 `KeyShortcut` 换算分派与 `UnansweredReply` 失败卡形状。

# 迁入或覆盖的旧测试
- 旧 `AppShellTests` 的 `HandleEvent` / `HandleShortcut` 用例已按名迁入 `compose/ProtocolDispatch/tests` 与 `compose/ShellCommands/tests`（见各自 AGENTS.md）。
- 本组合者的测试工程 `compose/AppShell/tests`：`Tests.fs` 3 条（`KeyShortcut` 按键换算与分派、`UnansweredReply` 失败卡形状），`ComposerTests.fs`（装配规则、端口扇出、SPEC 7 闭环旅程 1–6 完整闭环，以及新增的 `原生交互证明：真实控件树下的 Tab 序、Esc 退栈、删除邻位焦点与可访问名称角色`）。旧壳的视图事件接线本身没有独立旧测试：它由旧整套 UI 测试间接覆盖，随旧视图退役；新的装配规则改由上面这组假端口测试直接钉住。
- 旧壳里随视图退役的用例（`OverlayMenuTests`、`UiStabilityTests`、`DismissFocusTests`、`R21ImeTests`、`SettingsSurfaceTests`、`DialogsTests`、`A11yRegressionTests` 等）已分别迁入对应 Desk 库 / 组合者与 `compose/Theme`，见各自 AGENTS.md。

# 拆分与复核
- 旧壳的模型面（协议事件、导出分页、探测、搜索、下载）归 `ProtocolDispatch`；快捷键、命令面板、主题轮转归 `ShellCommands`；删除即撤销归 `WorkspaceModel.Update`；草稿落盘归端口；发件箱归 `Outbox`；对话运行状态归 `ConversationRuns`；目录与预设归 `Catalog`；下载缓冲与附件上传字节归 `DownloadBuffers`、`UploadFacts`、`MediaTypes`；图片缩小归 `ImageShrink`（平台实现经 `ImageShrinkIo.platformShrinker`，PWA 注入浏览器实现）；导入导出面板归 `TransferDesk`；对话框归 `DialogsDesk`。
- 旧壳的副作用（StorageProvider 选择与保存、剪贴板、启动器、系统深色、主题应用、焦点捕获、窗口关闭、计时器、浏览器视口轮询）全部移到入口壳端口；组合者只保留编排。
- 旧壳里的 `ParityProbe` 打点在新仓库没有对应物，未迁移（它记录的是旧宿主的运行探针，不是产品行为）。

# 证据
- 构建：`dotnet build compose/AppShell/AppShell.fsproj -c Release -p:RestorePackagesPath="$PWD/artifacts/packages"`（需 `artifacts/feed` 已有依赖包）。
- 测试：`dotnet test compose/AppShell/tests/AppShell.Tests.fsproj -c Debug -p:RestorePackagesPath="$PWD/artifacts/packages"`；`./x compose` 会跑全部有测试的组合者。`Headless.fs` 声明 `CollectionBehavior(DisableTestParallelization = true)`，防止多测试模块并发调用 `HeadlessUnitTestSession.EnsureIsolatedApplication` 抢占 Avalonia UI 线程。
- 交付独立：`./x pack` 产出 `artifacts/feed/Wanxiang2.AppShell.0.0.0-local.nupkg`；`./x compose` 在干净包缓存里消费它。
- 入口消费：`grep -n Wanxiang2.AppShell app/Desktop/Wanxiang.App.fsproj app/Pwa/Wanxiang.Pwa.fsproj`。
- 端到端：`tools/smoke_desktop.sh`（构建桌面入口 → 临时 home/data 真机启动 → 断言 `startup.firstFrame` / `startup.listReady`、无异常、存储文件与锁收尾）。
