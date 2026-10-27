# SettingsScreen（组合者）

SettingsFrame + ProvidersPane + ToolsPane + GeneralPane + DevicesPane + OnboardingPane + SectionMemory + ProbeSlot + BoundedWait + DevicesModel + OnboardingFlow + OnboardingRules + ProviderPresets + ProviderForm + ProviderProbeText + ProbeFailureKind + McpForm + ToolsPolicy + GenerationForm + GenerationDraft + HostHealth + LocalDraftControls + UiPrefs + FieldCheck + OverlayDesk + Theme = 旧 `Views/SettingsView.fs`、`SettingsProviders.fs`、`SettingsTools.fs`、`SettingsGeneral.fs`、`SettingsDevices.fs`、`OnboardingCard.fs` 的协作部分。界面全在各面板库；这里不造原始控件、不写字面样式值（`SettingsLooks` 从主题管理者取全部活实例）。根规则见项目根目录 AGENTS.md。

## 宿主接口
- `SettingsScreen(theme, overlay, hooks: SettingsHooks, initialPrefs: UiPrefs)`：单入口 `Render(vs: SettingsVS)`（WorkspaceState 的设置页总投影）；一次性命令 `ShowSection SettingsPart`、`FocusInitial()`、`ApplySectionScrollOffset`、`ApplyProbe(id, ok, models, error)`、`ApplyOpenAddProvider()`（兼容 `OpenAddProvider()`）；插槽注入 `WireImport handler`、`WirePresets(rename, delete)`；`HasUnsavedChanges`、`GetSectionScrollOffset`、`Health`（HostHealth）、`LocalDrafts`（LocalDraftControls）、`Devices`（DevicesDesk）、`Providers`、`Tools`、`General`；事件 `PrefsChanged`、`CloseRequested`。
- `SettingsHooks`：`UpsertProvider`、`UpsertMcp`、`UpdateGeneration`（JsonObject → 回执）、`DeleteProvider`、`DeleteMcp`、`ProbeProvider`、`SavePrefs`、`RequestDoctorCheck`（「检查主机」：设置页点按钮后由宿主向主机发 doctor 请求）；`SettingsHooks.none`。
- `DevicesDesk`：单入口 `Render(SettingsDevicesVS)`；插槽注入 `WireLanSource`；一次性命令 `ApplyLoadTimeout()`、`ApplyDisconnected()`、`Tick now`、`Refresh()`；`IsLoading`、`CurrentInvite`；事件 `ListRequested`、`InviteRequested`、`RenameRequested (id, name)`、`RevokeRequested DeviceInfo`（确认由宿主弹，与旧代码一致）。
- `GeneralDesk`：单入口 `Render(SettingsGeneralVS)`；插槽注入 `WireImportHandler`、`WirePresetHandlers`；一次性命令 `SaveGeneration()`、`DiscardGenerationChanges()`。面板侧只走 `GeneralPane` 的单入口 `Render(GeneralSectionVS)` 与 `Apply*` 命令：`ApplyPresetCaption`、`ApplyVisionCurrent`、`ApplyProbeState`、`ApplyPending`、`ApplyEnabledValue`、`ApplyAdvanced`、`ApplyMaxHeight`、`ApplyCaptions`、`ApplyContent`（设备面板与框架无 `Set*` 公开方法）。主机状态归 `HostHealth`：面板点「检查主机」→ `health.BeginCheck()`（转 Running 并发 `CheckRequested`）→ 本组合者订阅 `CheckRequested` 并转成 `hooks.RequestDoctorCheck`（宿主发 `doctor.request`）；`HostConfigRejected` / `DoctorReport` 由宿主经 `Health.ApplyConfigIssue` / `Health.ApplyReport` 回填。
- `ProvidersDesk`：单入口 `Render(SettingsProvidersVS)`；一次性命令 `ApplyOpenAddEditor()`、`ApplyOpenEditor(id)`、`ApplyProbe(id, ok, models, error)`。
- `ToolsDesk`：单入口 `Render(SettingsToolsVS)`。
- `OnboardingDesk(theme, overlay, hooks: OnboardingHooks)`：单入口 `Render(SettingsOnboardingVS)`；一次性命令 `ApplyProbe(...) : bool`、`ApplyDiscovered(result)`、`TakeConnected catalog`、`StartDiscovery()`、`Reset()`、`FocusInitial()`、`IsBusy`、`Step`、`Dispose()`。`OnboardingHooks`：`UpsertProvider`、`DiscardProvider`、`ProbeProvider`、`TakenIds`、`DiscoverLocal`、`OpenAdvanced`、`Report`（引导卡把所选预设 / 本机发现 / 兼容端点建议回报给宿主，事实进模型后投影回来）。
- `SettingsPart`（旧 SettingsSection，改名避开旧宿主）：`groups`、`all`、`scope`、`label`、`glyph`、`key`、`ofKey`。

## 在途 UI 状态登记与归属理由
- `DevicesDesk.editing: string option`：用户正在行内编辑某台设备名称的在途状态（行内文本框实例与焦点仅在编辑时存在，未提交前属 Desk 纯私有 UI 状态）。
- `GeneralDesk.draft: GenerationDraft<GenerationInfo>`：对话默认值生成参数的在途未提交表单草稿与校验脏标记（避免主机后台目录广播冲刷用户正在键入的数字框）。
- `ProvidersDesk.session / probeEditor`：当前正在弹窗编辑中的服务配置表单及探活挂钩（属于模态/非模态对话框生命周期内的未保存临时状态）。
- `ToolsDesk.mcpEditor / folderEditor`：当前正在弹窗编辑中的 MCP 服务表单与文件夹授权表单（属于对话框在途编辑态）。
- `OnboardingDesk.flow`：引导卡片内单次连接尝试的步进状态机（包含正在探活/正在回填与重试切换标记）。
  卡片的三项**显示**事实（所选预设 / 本机发现的模型 / 兼容端点建议）**不归桌内**：它们在 `WorkspaceModel.Onboarding`，
  卡片经 `hooks.Report` 回报、宿主经 `Render(SettingsOnboardingVS)` 投影回来（`OnboardingDesk` 只有这一条写路径）。
  `OnboardingFlow` 也不再保存发现结果（同一事实不存两份）。

## 证据与旧测试
- DevicesUiTests：`settings has a devices section in the host group`、`selecting the devices section requests the list once`、`device list read without a reply times out with the older host hint`、`device list marks current desktop online and revoked and sorts revoked last`、`rename edits inline trims and sends the new name`、`a disconnect while loading explains the host may be too old`、`invite card shows grouped code address qr and countdown`
- SettingsViewTests：`SettingsView navigates sections, remembers scroll positions and invalidates cache`、`SettingsProviders saveCore with invalid advanced field automatically expands advanced layer and focuses error`、`SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`、`SettingsGeneral SaveGenerationCore validates numeric ranges and generates payload`、`SettingsTools ShowEditor validates id, command/url, timeout and saves valid config`
- OnboardingUiTests：`密钥为空时不发起任何请求并就地说明`
- 新增：`changing a theme metric updates shown settings controls`；模态设置页层拦截 `Key.Tab` 并委托 `overlay.TrapTab(surface, e)` 在设置页卡片内循环。

## 与旧实现的差异
- 提示条走 OverlayDesk，出口里不再有 toast；设备出口由函数记录改成事件；DevicesDesk 构造即建好界面（旧 Build 惰性）。
- 对话层宽改为主题度量 SettingsDialogWidth（520）/ SettingsSmallDialogWidth（480），内容窗高 DialogContentMaxHeight，内容区留白 Space.X6l / X8l，都可变。
- 首次运行卡片的兼容端点切换键在状态里保留（旧代码直接改按钮可见性），时机不变。
