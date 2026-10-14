# SettingsScreen（组合者）

SettingsFrame + ProvidersPane + ToolsPane + GeneralPane + DevicesPane + OnboardingPane + SectionMemory + ProbeSlot + BoundedWait + DevicesModel + OnboardingFlow + OnboardingRules + ProviderPresets + ProviderForm + ProviderProbeText + ProbeFailureKind + McpForm + ToolsPolicy + GenerationForm + GenerationDraft + HostHealth + LocalDraftControls + UiPrefs + FieldCheck + OverlayDesk + Theme = 旧 `Views/SettingsView.fs`、`SettingsProviders.fs`、`SettingsTools.fs`、`SettingsGeneral.fs`、`SettingsDevices.fs`、`OnboardingCard.fs` 的协作部分。界面全在各面板库；这里不造原始控件、不写字面样式值（`SettingsLooks` 从主题管理者取全部活实例）。根规则见项目根目录 AGENTS.md。

## 宿主接口
- `SettingsScreen(theme, overlay, hooks: SettingsHooks, initialPrefs: UiPrefs)`：`View`、`Bind hooks`、`ShowSection SettingsPart`、`FocusInitial()`、`SetCatalog`、`SetPrefs`、`SetConnection(instance, url)`、`ApplyProbe(id, ok, models, error)`、`OpenAddProvider()`、`HasUnsavedChanges`、`Get/SetSectionScrollOffset`、`Health`（HostHealth）、`LocalDrafts`（LocalDraftControls）、`ConfigureImport handler`、`ConfigurePresets(rename, delete)`、`Devices`（DevicesDesk）、`Providers`、`Tools`、`General`；事件 `PrefsChanged`（已调 SavePrefs 之后）、`CloseRequested`。
- `SettingsHooks`：`UpsertProvider`、`UpsertMcp`、`UpdateGeneration`（JsonObject → 回执）、`DeleteProvider`、`DeleteMcp`、`ProbeProvider`、`SavePrefs`；`SettingsHooks.none`。
- `DevicesDesk`：`SetDevices(items, canInvite)`、`SetInvite(code, expires, now)`、`SetInviteRejected reason`、`SetConnection url`、`Tick now`、`Refresh()`、`NotifyLoadTimeout()`、`NotifyDisconnected()`、`SetLanSource`、`IsLoading`、`CurrentInvite`；事件 `ListRequested`、`InviteRequested`、`RenameRequested (id, name)`、`RevokeRequested DeviceInfo`（确认由宿主弹，与旧代码一致）。
- `OnboardingDesk(theme, overlay, hooks: OnboardingHooks)`：`View`、`ApplyProbe(...) : bool`、`TakeConnected catalog`、`StartDiscovery()`、`ShowDiscovered`、`Reset()`、`FocusInitial()`、`IsBusy`、`Step`、`Dispose()`。`OnboardingHooks`：`UpsertProvider`、`DiscardProvider`、`ProbeProvider`、`TakenIds`、`DiscoverLocal`、`OpenAdvanced`。
- `SettingsPart`（旧 SettingsSection，改名避开旧宿主）：`groups`、`all`、`scope`、`label`、`glyph`、`key`、`ofKey`。

## 证据与旧测试
- DevicesUiTests：`settings has a devices section in the host group`、`selecting the devices section requests the list once`、`device list read without a reply times out with the older host hint`、`device list marks current desktop online and revoked and sorts revoked last`、`rename edits inline trims and sends the new name`、`a disconnect while loading explains the host may be too old`、`invite card shows grouped code address qr and countdown`
- SettingsViewTests：`SettingsView navigates sections, remembers scroll positions and invalidates cache`、`SettingsProviders saveCore with invalid advanced field automatically expands advanced layer and focuses error`、`SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`、`SettingsGeneral SaveGenerationCore validates numeric ranges and generates payload`、`SettingsTools ShowEditor validates id, command/url, timeout and saves valid config`
- OnboardingUiTests：`密钥为空时不发起任何请求并就地说明`
- 新增：`changing a theme metric updates shown settings controls`。

## 与旧实现的差异
- 提示条走 OverlayDesk，出口里不再有 toast；设备出口由函数记录改成事件；DevicesDesk 构造即建好界面（旧 Build 惰性）。
- 对话层宽改为主题度量 SettingsDialogWidth（520）/ SettingsSmallDialogWidth（480），内容窗高 DialogContentMaxHeight，内容区留白 Space.X6l / X8l，都可变。
- 首次运行卡片的兼容端点切换键在状态里保留（旧代码直接改按钮可见性），时机不变。
