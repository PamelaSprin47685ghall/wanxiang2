# DevicesPane

合并包：DevicesPane、GeneralPane、OnboardingPane、ProvidersPane、SettingsFrame、ToolsPane。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

接口命名（根规则）：本库对外只暴露单入口 `Render(viewState)` 与事件；一次性写投影走 `Apply*`、插槽走 `Wire*`，并在各成员的接口节逐条登记。库内没有公开的 `Set*` 状态修改器。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `DevicesPane`

## DevicesPane

设置 → 设备分区的界面（迁自旧 `Views/SettingsDevices.fs` 的界面部分与 `QrCodeView`）。根规则见项目根目录 AGENTS.md。

- `DevicesPane(look)`；`View` 是整块；`Render state` 整块重建一帧（与旧实现每次 render 相同）。
- `DevicesPaneState`：`Invite`（`Unavailable 文字` / `Offer 被拒说明` / `Code(分组码, 逐字读法, 倒计时文字, 是否过期, 地址)`）、`List`（`Note 文字` / `Rows 已排序的行`）、`Editing`（正在重命名的设备 id）。
- `DevicesPaneAddress`：`Reachable(url, 二维码矩阵)` / `LoopbackOnly(首个局域网地址与矩阵, 其余地址)` / `NoAddress`。
- `QrPlate(text, matrix, moduleSize)`：白底黑模块、4 模块静区（与主题无关，扫码要对比度）；模块边长是活值。
- 事件：`AddClicked`、`RegenerateClicked`、`CloseInviteClicked`、`RefreshClicked`、`RenameClicked id`、`RenameSaved(id, 去空白的新名)`（空名也发，由组合者提示）、`RenameCancelled id`、`RevokeClicked id`。编辑时 `NameBox` 是名称输入框，打开编辑器后焦点自动进入它。
- 读取设备列表、有界等待（BoundedWait）、倒计时时钟、地址推断（DevicesModel.inviteTarget）、二维码矩阵（DevicesModel.qrMatrix）、排序与行文字都由组合者接。
- 自动化 id 与读屏名与旧实现逐字相同（`devices-add`、`devices-code`、`devices-countdown`、`devices-address`、`devices-qr`、`devices-regenerate`、`devices-close-invite`、`devices-list`、`devices-row-<id>`、`devices-rename-<id>`、`devices-revoke-<id>`、`devices-name-<id>`、`devices-name-save-<id>`、`devices-name-cancel-<id>`、`devices-refresh`、`devices-invite-unavailable`）。

### 外观

- `DevicesPaneLook`：公共字段（画笔、字号、间距、圆角、输入壳度量、等宽字体、`Glyph` 图标槽）加 `CodeTracking`（配对码字距）、`QrModuleSize`。全部是主题管理者的活实例。
- 用到的样式类（compose/Theme）：按钮 `surface button-* hover-*`（吊销为 `button-danger hover-danger`）、卡 `grouping-card`、分节标签 `section-label`、输入壳 `surface hover-raised focus-soft`（出错时组合者的 FieldCheck 挂 `invalid`，聚焦描边让位）。

### 证据与旧测试

- DevicesUiTests：`device list marks current desktop online and revoked and sorts revoked last`（呈现部分）、`revoke button passes the device to the action`、`rename edits inline trims and sends the new name`（呈现与事件部分；空名提示在组合者）、`only a desktop capable connection sees the add device button`、`invite card shows grouped code address qr and countdown`（呈现部分；时钟在组合者）、`loopback only host explains the listen setting and uses a lan address for the qr`、`rejected invite explains that only the desktop app can add devices`、`qr view measures with a quiet zone and draws on a white background`
- 组合者 SettingsScreen 覆盖：`device list read without a reply times out with the older host hint`、`a disconnect while loading explains the host may be too old`、`selecting the devices section requests the list once`

### 与旧实现的差异

- 「完成」不再由面板自己清掉邀请：发 `CloseInviteClicked`，组合者清掉后重绘。可见行为不变。
- 空名不由面板弹提示：`RenameSaved` 带空串，组合者弹提示并保持编辑。

## 成员 `GeneralPane`

## GeneralPane

设置里的三块通用分区（迁自旧 `Views/SettingsGeneral.fs` 的界面部分）：对话默认值、外观与交互（含本机草稿）、关于与系统诊断（含主机状态）。根规则见项目根目录 AGENTS.md。

- `GenerationSection(look, validationFor)`：五个数值框（两列表单，窄于 `FormSingleColumnBreakpoint` 退化为单列）、多行系统指令、「自动生成对话标题」开关、错误摘要（`Summary`）、保存状态行（polite）、保存 / 放弃、对话预设列表、导入入口。
  - `Read()` / `Fill GenerationText`；单入口 `Render(vs: GeneralSectionVS)`（状态行、保存往返 `Pending` + `CanSave`、对话预设 `Presets`）；插槽 `WireImportHandler(handler)`（注入即显示「导入对话…」，可见性归这里独占）。
  - 事件：`Edited`（任一字段或开关变化，填表时也发）、`SaveRequested`（保存键、任意处 Ctrl/⌘+Enter、数值框回车；指令框回车是换行）、`DiscardRequested`、`PresetRenameClicked id`、`PresetDeleteClicked id`。「导入对话…」的回调走插槽 `WireImportHandler`，没有单独事件。
- `AppearanceSection(look)`：主题选择器（读屏 ComboBox，`ThemeClicked anchor`）、字号 −/+（`FontStepped ±0.5`；到边界由 `Render` 禁用）、五个交互开关与「保存未发送的草稿」（`Toggled(AppearanceSwitch, 值)`，只在用户拨动时发）、本机草稿概况与「清除本地草稿」（`ClearDraftsClicked`）。`Render AppearanceState`。
- `AboutSection(look)`：单入口 `Render(vs: GeneralSectionVS)`（信息行 `AboutRows`（等宽、可选中）、主机状态 `Health(ConfigIssue, Doctor)`；检查中按钮禁用，`CheckHostClicked`）、复制诊断报告（`CopyDiagnosticsClicked`）、设计与致谢卡。
- 草稿状态（GenerationDraft）、校验（GenerationForm + FieldCheck）、写回、偏好（UiPrefs）、主题菜单、剪贴板、确认框、HostHealth 订阅都由组合者接。
- 自动化：`settings-presets`、`preset-row`、`settings-import`、`local-drafts-summary`、`clear-local-drafts`、`host-config-issue`、`host-config-issue-detail`、`host-config-ok`、`check-host`、`doctor-headline`、`doctor-check-<name>`，与旧实现相同。

### 外观

- `GeneralPaneLook`：公共字段加 `SpaceXXs`、`TextAreaMinHeight`、`FormSingleColumnBreakpoint`、`AboutKeyMinWidth`、`FontSizeValueMinWidth`、`PendingMinWidth`、`SelectCaptionMaxWidth`、`SelectPaddingY`、`Selection`。`Glyph` 用到：`minus`、`plus`、`chevron-down`、`check-circle`、`alert`。
- 样式类：按钮 `surface button-* hover-*`、图标按钮 `icon-button`、选择器 `surface select-button hover-bordered`、卡 `grouping-card`、分隔 `hairline`、开关 `toggle-track` / `toggle-knob` / `on`、输入壳 `surface hover-raised focus-soft`。

### 证据与旧测试

- SettingsSurfaceTests：`editing a generation number stays local until explicitly saved`、`generation save button advertises the ctrl enter shortcut`、`generation form fields sit inside a grouping card`、`generation form consumes shared column spacing and text metrics`、`about section drops the misplaced spacer`
- SettingsViewTests：`SettingsGeneral BuildAppearance adjusts fontScale within bounds and updates prefs`（界面部分；夹值在组合者）、`SettingsGeneral BuildAbout formats system diagnostics with version and platform`（界面部分；报告文字在组合者）
- 组合者 SettingsScreen 覆盖：`SettingsGeneral SaveGenerationCore validates numeric ranges and generates payload`、`generation save failure surfaces a failure toast instead of silence`
- 新增：`validation rows sit in each field group and hide the hint`、`presets list offers rename and delete per preset and an empty explanation`、`theme selector reads as a combo box and opens the menu at itself`、`host status shows config issues and doctor results`

### 与旧实现的差异

- 两列表单的断点是主题度量（活值），改了立即重排。
- 外观页的开关在组合者给值时不触发事件；旧实现同样只在用户拨动时写偏好。

## 成员 `OnboardingPane`

## OnboardingPane

聊天主界面上的「添加一个模型服务」卡片（迁自旧 `Views/OnboardingCard.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `OnboardingPane(look)`；`View` 是卡片；`Render state` 同步一帧 `OnboardingPaneState`（服务商键、「更多」文字、说明、密钥 / 地址框是否可见、连接中、状态行、兼容端点切换键、本机发现说明）。
- 输入：`KeyBox`（密码字符 ●，回车即连接）、`CustomUrlBox`、`KeyText` / `CustomUrlText`（已去空白）、`ClearKey()`；`FocusInitial needsKey`（要密钥进密钥框，否则落在「连接」）。
- 事件：`ChipClicked key`、`MoreClicked anchor`（连接中不发；组合者在锚点弹菜单）、`ConnectClicked`、`SwitchClicked`、`AdvancedClicked`、`UseLocalClicked`。
- 连接中：「连接」保持最小宽，只换成「正在连接…」并禁用（读屏帮助文字同步），两个输入框禁用。
- 步骤与状态文字（OnboardingFlow）、预设与载荷（ProviderPresets、OnboardingRules）、探测、本机发现、写回与「更多」菜单（PopupMenu / OverlayDesk）都由组合者接。

### 外观

- `OnboardingPaneLook`：公共字段加 `Space3xl`（卡片内边距）、`CardMaxWidth`、`PendingMinWidth`。全部是主题管理者的活实例。
- 样式类：按钮 `surface button-* hover-*`、卡 `grouping-card`、输入壳 `surface hover-raised focus-soft`。

### 证据与旧测试

- OnboardingUiTests：`密钥为空时不发起任何请求并就地说明`（呈现与焦点部分）、`路径错时改用兼容端点后重试，载荷换成兼容传输`（切换键部分）、`发现本机 Ollama 时给出一键使用，点击后用发现的模型保存且不要密钥`（呈现部分）、`没有 Ollama 时不显示一键使用`
- 新增：`连接中按钮保持几何只换文字并禁用，选择键与更多不再响应`、`服务商键选中为主色，点击发出预设 key`、`引导卡内连续按 Tab 依次走过服务商键与更多并进入 API Key 输入框，且重复渲染保留焦点`、`look values stay live after the card is shown`
- 写回、探测、撤销半成品、外壳旅程（`外壳里走完整条旅程…` 等）在组合者与宿主。

### 与旧实现的差异

- 无可见差异。服务商键清单相同时原位更新样式与文字（不销毁重建持焦按钮），并为 `WrapPanel` 补齐行内顺序 `Tab` 流转。

## 成员 `ProvidersPane`

## ProvidersPane

设置 → 模型服务分区的列表，以及「添加 / 编辑模型服务」对话框的内容（迁自旧 `Views/SettingsProviders.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `ProvidersPane(look)`：`View`；`Render ProviderRow list`（状态点 + 名称 + 状态标签「已配置 / 缺少密钥（警示）/ 已停用」+「默认」标签 + 地址行；停用时名称弱化，编辑与更多仍可点）。列表为空时显示空态卡，「添加模型服务」由空态里的主按钮接管、顶部同名键隐藏。
  - 事件：`AddClicked`、`EditClicked id`、`MoreClicked(id, 键)`。
- `ProviderEditor(look, isNew, validationFor)`：预设选择器（读屏 ComboBox，`PresetClicked anchor`）、错误摘要（`Summary`）、快速区（名称、密钥 ●、地址）、「高级设置 ▼ / 收起高级设置 ▲」（`IsAdvanced`；展开/收起由控件自己在途态持有，外部强制展开走命令 `ApplyAdvanced`）、高级区（标识、模型列表、默认模型、获取模型列表 `ProbeClicked`、看图能力、超时、重试、启用开关 `Enabled`）、取消 / 添加（或保存）。
  - 命令类操作登记（一次性写投影，无先后依赖）：
    - `ApplyPresetCaption(label, hint, kind, transport)`：预设标题、预设说明（空则隐藏）、传输说明与传输切换键（`transport` 为 `None` 时隐藏）。
    - `ApplyVisionCurrent(text, inheritOverride, customOverride)`：看图能力的当前判断与手动指定的两框。
    - `ApplyProbeState(pending, status)`：获取模型列表往返（换「正在获取…」并禁用）与探活失败的常驻行（空则隐藏）。
    - `ApplyPending(value)`：保存往返（换「正在保存…」并禁用、取消也禁用、不再发 `SaveRequested`）。
    - `ApplyEnabledValue(v)`：「启用这个模型服务」开关的值。
    - `ApplyAdvanced(expanded)`：强制展开高级区（表单错误落在收起的高级区时，组合者先展开）。
  - 单行框回车、任意处 Ctrl/⌘+Enter 发 `SaveRequested`；多行框回车是换行。页脚左右方向键在两键间移动。
  - 新建时「获取模型列表」禁用并就地说明要先保存。`InitialFocus` 是名称框。`LockId()` 让编辑时标识只读。`AdvancedBoxes` 是高级区里的框（出错时组合者先展开）。
- 预设套用与分歧判定（ProviderPresets）、传输切换、探活（ProbeSlot、ProviderProbeText）、校验（ProviderForm + FieldCheck）、写回、菜单与对话框都由组合者接。
- 自动化：`provider-probe-models`、`provider-vision-yes`、`provider-vision-no`、`provider-vision-current`，与旧实现相同。

### 外观

- `ProvidersPaneLook`：公共字段加 `SpaceXXs`、`TextAreaMinHeight`、`EmptyLogoSize`、`PendingMinWidth`、`StatusDotSize`、`SelectCaptionMaxWidth`、`SelectPaddingY`。`Glyph` 用到：`pencil`、`more`、`chevron-down`、`logo`。
- 样式类：按钮 `surface button-* hover-*`、图标按钮 `icon-button`、选择器 `surface select-button hover-bordered`、卡 `grouping-card`、标签 `tag` / `tag-warning`、分隔 `hairline`、开关、输入壳 `surface hover-raised focus-soft`。

### 证据与旧测试

- SettingsSurfaceTests：`missing key reads as warning tag and disabled provider dims its text`
- SettingsViewTests：`SettingsProviders two-tier form defaults to quick access and preserves drafts when toggling advanced settings`、`SettingsProviders ShowEditor validates required fields and default model existence`（界面与键盘部分；校验在组合者）、`SettingsProviders applyPreset updates endpoint, models and tracks divergence to custom`（选择器与说明部分；套用与分歧在组合者）
- 组合者 SettingsScreen 覆盖：`SettingsProviders saveCore with invalid advanced field automatically expands advanced layer and focuses error`、`SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`
- 新增：`empty provider list hands the add action to the empty state`、`new provider cannot probe until saved and explains why`、`look values stay live in the editor`

### 与旧实现的差异

- 无可见差异。空态卡是本库的简化形态（标志 + 说明 + 主按钮）。

## 成员 `SettingsFrame`

## SettingsFrame

设置页外框（迁自旧 `Views/SettingsView.fs` 的界面部分）：头部「设置」与「返回对话」、分组的分区导航、范围说明、内容区与窄屏布局。根规则见项目根目录 AGENTS.md。

- `SettingsFrame(look, groups)`：本身是 Border。`groups` 是 `(分组名, SettingsNavItem list)`，项为 `{ Key; Label; Glyph }`。
- `Select key`：标出选中分区（选中底、左缘强调条、标题加粗、图标不透明度、读屏 ItemStatus「当前分区」与帮助文字「<名>，当前分区」）并把键滚进视野。
- 命令类操作登记：`ApplyCaptions(范围, 说明)`、`ApplyContent(control, restoreY)`（换内容；`restoreY` 有值时滚到该位置）。其余：`ContentOffset`、`ScrollTo`、`FocusNav key`、`Current`、`IsCompact`。
- 事件：`SelectRequested key`（点击，或在导航键上按上 / 左 / 下 / 右循环移动、Home / End 跳首尾；键盘移动后焦点跟到目标键）、`CloseClicked`。组合者收到后切内容并回调 `Select`。
- 读屏：导航轴是 Tab「设置分区导航」，每个键是 TabItem。
- 窄于 `CompactBreakpoint`：导航转为顶部横条（分组名隐藏、横向滚动），内容内边距收紧；切换时按新边界钳制滚动位置再落位。断点是活值，改了立即重排。
- 分区内容、缓存与滚动记忆（SectionMemory）、范围文字、设备分区的刷新都由组合者接。

### 外观

- `SettingsFrameLook`：公共字段加 `Space6xl`、`Space8xl`、画笔 `Canvas` / `Rail` / `Chrome` / `Selected`、`BarHeight`、`NavWidth`、`NavMinHeight`、`SelectedEdgeWidth`、`ContentMaxWidth`、`SubtleOpacity`、`CompactBreakpoint`。`Glyph` 用到各分区的图标名（组合者给：`server`、`wrench`、`sliders`、`devices`、`sun`、`info`）。
- 样式类：导航键 `surface menu-item hover-plain`（未选中时清掉本地底，悬停样式才生效）、「返回对话」`surface button-ghost hover-plain`、分组名 `section-label`。

### 证据与旧测试

- SettingsViewTests：`SettingsView navigates sections, remembers scroll positions and invalidates cache`（导航与滚动落位部分；缓存与记忆在组合者）
- SettingsSurfaceTests：`settings window title dominates section headings`
- 新增：`settings nav arrows wrap and home end jump while keeping focus on the nav`、`narrow window turns the nav into a top strip and hides group labels`、`scope captions follow the composer`

### 与旧实现的差异

- 导航键的悬停底由主题样式给（旧实现在指针进出与焦点变化时手动重算底色）；选中底仍是本地值。看到的效果相同。

## 成员 `ToolsPane`

## ToolsPane

设置 → 工具分区的界面，以及「添加 / 编辑 MCP 服务」「授权文件夹」两个对话框的内容（迁自旧 `Views/SettingsTools.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `ToolsPane(look)`：`View`；`Render ToolsPaneState`（工具、授权文件夹、沙箱说明、调试开关值、MCP 服务）。空清单时显示空态卡。
  - 事件：`PolicyClicked(toolId, 键)`、`RemoveFolderClicked path`、`DebugToggled 新值`（只在用户拨动时发；`Render` 写值不发）、`AddFolderClicked`、`AddServerClicked`、`EditServerClicked id`、`ServerMoreClicked(id, 键)`。
  - 自动化：`tool-policy-<id>`（读屏名「<名> 的审批方式：<方式>」）、`tools-add-folder`、「移除文件夹授权 <路径>」。
- `McpEditor(look, existing, validationFor)`：六个输入框（`IdBox` … `TimeoutBox`、`Boxes`）、启用开关（`Enabled`）、错误摘要（`Summary`，assertive、可聚焦）、取消 / 添加（或保存）键。
  - 单行框回车、任意处 Ctrl/⌘+Enter 发 `SaveRequested`；参数框回车是换行。
  - 命令类操作登记：`ApplyPending(value)`（保存往返：换「正在保存…」并禁用、取消也禁用、不再发 `SaveRequested`）、`ApplyMaxHeight(h)`（给对话框内容的上限）。
  - 页脚左右方向键在两键间移动。`InitialFocus`：新建是标识，编辑是名称（标识只读）。
  - `validationFor` 是每个框的校验行（组合者的 FieldCheck.MessageFor），放进对应字段组，可见时说明让位。
- `FolderEditor(look, validationFor)`：路径框（`PathBox`）、取消 / 授权；回车或「授权」发 `SaveRequested 去空白的路径`。
- 校验（McpForm、路径规则）、写回（ToolsPolicy.payload、McpForm.payload）、审批与服务菜单、对话框开关、Esc 守卫都由组合者接。

### 外观

- `ToolsPaneLook`：公共字段加 `SpaceXXs`、`TextAreaMinHeight`、`EmptyLogoSize`、`PendingMinWidth`。`Glyph` 用到的名字：`server`、`wrench`、`pencil`、`more`、`trash`、`logo`。
- 样式类：按钮 `surface button-* hover-*`、图标按钮 `surface icon-button hover-plain`、卡 `grouping-card`、标签 `tag`、分隔 `hairline`、开关 `toggle-track` / `toggle-knob` / `on`、分节标签 `section-label`、输入壳 `surface hover-raised focus-soft`。

### 证据与旧测试

- SettingsViewTests：`SettingsTools SetCatalog renders tool list, mcp list and sandbox notes`（呈现部分）、`SettingsTools ShowEditor validates id, command/url, timeout and saves valid config`（界面与键盘部分；校验与载荷在组合者）
- W3ToolApprovalUiTests：`工具设置：每个工具有审批方式入口，授权文件夹有入口；空授权时文案指向入口`（呈现与事件部分；载荷在组合者）
- SettingsSurfaceTests：`disabled mcp card dims its text while keeping row actions alive`、`built-in tool row title shares the row-title body font`、`switch row consumes the shared settings row height and toggle semantics`
- 新增：`editing an existing mcp server locks its id and focuses the label`、`folder editor sends the trimmed path on enter`

### 与旧实现的差异

- 空态卡是本库自己的简化形态（标志 + 说明，无标题、无动作），与旧 `Ui.emptyState` 在这两处的用法一致。
- 调试开关行只建一次，`Render` 写值；旧实现每次 SetCatalog 重建该行。焦点因此不会在目录刷新时丢失。

## 成员 `GenerationDraft`

### GenerationDraft（库）

旧 `Views/SettingsGeneral.fs` 里「生成」草稿的状态（`generationDirty`、`generationSaving`、`generationSyncing`、`generationRevision`、`generationBaseline`、`generationScope`、`draftScope`、`scopeEpoch`）与状态文案迁入，成为泛型对象 `GenerationDraft<'T>`（'T 为生成设置的值，旧代码里是 Catalog.generation）：`Edited`、`CatalogArrived`、`ApplyScope`、`Discard`、`CanSave`、`BeginSave`、`Finish`、`Status`。表单回填由调用方传入的 fill 完成，期间的编辑不算修改。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一串事件，脏、保存中、基线、能否保存与状态文案和旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/GenerationDraft.Tests.fs` 通过：`catalog refills a clean form and edits during the refill do not count`、`catalog never overwrites an unsaved draft`、`a draft cannot be saved to another host but can after coming back`、`an earlier confirmation does not mark later edits as saved`、`switching hosts voids the pending confirmation`、`a failed save keeps the draft and asks for a toast`、`discard is refused while saving and otherwise refills from the given value`。
2. 目录更新不覆盖未保存的草稿；草稿只能保存到开始编辑时的主机；切换主机作废在途保存的确认；较早的保存确认不把之后的编辑标成已保存；保存失败返回 true，调用方弹「保存失败，请重试。」。

### 迁入或覆盖的旧测试
旧 SettingsSurfaceTests 里的生成设置用例要操作设置页，留在旧仓库。本包新增 7 条规则用例。

### 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `GenerationForm`

### GenerationForm（库）

旧 `Views/SettingsGeneral.fs` 里「生成」分组的 `floatText` 与 `SaveGenerationCore` 的校验和载荷迁入：`validate`（5 个输入框文本 → 数值或按字段的错误文案）、`instructions`、`payload`。字段联合类型 `GenerationField` 用例名是泛名，加了 RequireQualifiedAccess。错误怎么挂到控件、提交与回复仍归 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一组输入，错误与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5，线上字节不变]

### SPEC
1. [LEMMA: 1] `tests/GenerationForm.Tests.fs` 通过：`blank optional fields mean unset and required fields must parse`、`ranges are inclusive at the documented bounds`、`decimal separator is always a dot`、`payload writes every key with null for unset values in a fixed order`、`instructions are trimmed and blank means none`。载荷键序 temperature、topP、maxTokens、instructions、maxContextMessages、maxToolRounds、autoTitle，未设项写 null。
2. 数字一律按不变文化解析，小数点只认点号。

### 迁入或覆盖的旧测试
旧 SettingsSurfaceTests 的 `generation save failure surfaces a failure toast instead of silence` 等要操作设置页，留在旧仓库。本包新增：空值与必填、边界、小数点、载荷形状、指令去空白。

### 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `LocalDraftControls`

### LocalDraftControls

设置页「本机草稿」区与外壳之间的桥（迁自旧 `Model/HostHealth.fs` 末尾）。旧的可设置回调 `ClearRequested` 是端口式注入，改为事件 `ClearRequested` 加方法 `RequestClear()`：设置页调用 `RequestClear()`，宿主把 `ClearRequested <- f` 改为 `ClearRequested.Add(fun () -> f ())`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **只在概况真的变化时通知。** [根 AXIOM: 2]

### SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

### 迁入或覆盖的旧测试
- 无旧测试。

## 成员 `McpForm`

### McpForm（库）

旧 `Views/SettingsTools.fs` 里 MCP 编辑器 saveCore 的校验与 `mcpPayload` 迁入：`validate` 返回值和按字段的错误（`McpField` 加了 RequireQualifiedAccess），`payload` 组写入载荷，`parseArgs` 切参数行。挂错误、提交与回复处理仍归 SettingsTools。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一组输入，错误次序、文案与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/McpForm.Tests.fs` 通过：`exactly one of command and url, both fields flagged`、`timeout must be 1 to 3600`、`a taken id only blocks new entries`、`payload omits empty command, args and url`。
2. 本地命令与远程端点必须且只能填一个，出错时两栏都标；超时为 1 到 3600 秒；已占用的标识只挡新增。

### 迁入或覆盖的旧测试
旧 SettingsTools 的用例要起窗口，留在旧仓库。本包新增 4 条规则用例。

## 成员 `OnboardingFlow`

### OnboardingFlow

首次运行「添加一个模型服务」卡片的步骤机（迁自旧 `Views/OnboardingCard.fs`：`step`、`attemptId`、`connected`、`useAlternate`、`discoveryStarted`，以及 `connect`、`applyProbed`、`fail`、`ApplyProbe`、`TakeConnected`、`StartDiscovery`、`ShowDiscovered`、一键 Ollama、`Reset` 里的转移与状态文字）。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

W7-B3：`discovered`（本机发现的模型清单）已从本对象移除——该事实归 `WorkspaceModel.Onboarding`，
引导卡经端口回报、宿主投影回来；本对象只回答「要不要显示一键使用」。

宿主仍负责：预设选择与载荷（ProviderPresets）、默认模型挑选（OnboardingRules.pickDefaultModel）、失败原因解析（ProbeFailureKind）、保存与探测命令、目录就绪判断、控件。本库不认识这些类型。

### LEMMA
1. **只在空闲时接受新动作**（选家、切传输、连接、一键本机、复位）；忙时一律不动。
2. **探测结果只认本卡片在探测阶段发起的那一次**；失败时回到空闲并交出要撤销的 id。
3. **连接成功的 (id, 模型) 只交出一次。**
4. **发现本机 Ollama 每次显示只探一次**，复位后可再探。

### SPEC
1. [LEMMA: 1–4] `tests/Tests.fs` 通过，状态文字逐字断言。

### 迁入或覆盖的旧测试（OnboardingUiTests，步骤与文字部分）
- `mock 服务上：选预设、填密钥、点连接后模型列表自动填充且默认模型被选中`
- `密钥为空时不发起任何请求并就地说明`
- `密钥无效、路径错、不通各自给出不同的人话原因，并撤销半成品`
- `路径错时改用兼容端点后重试，载荷换成兼容传输`
- `发现本机 Ollama 时给出一键使用，点击后用发现的模型保存且不要密钥`
- `没有 Ollama 时不显示一键使用`

这些旧用例同时验证 mock HTTP、载荷与控件，那部分留在旧仓库；旧仓库删用例前需确认那部分另有覆盖。

### 与旧实现的差异
- `OnboardingStep` 加了 `RequireQualifiedAccess`（Idle 等是通用名字）。
- 旧 `TakeConnected` 自己查目录；这里由宿主先查目录、确认可用再调用 `TakeConnected`。

## 成员 `ProbeFailureKind`

### ProbeFailureKind

模型探测失败的类型：解析线上 `[code] 说明`，判断是否值得建议切换传输，迁自旧 `Model/Catalog.fs`。与主机 ProviderCatalog.ProbeFailure 错误码的一一对应由组合者 Catalog 的测试核对。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **没有前缀或认不出的码一律归为 ProbeUnclassified，说明原样保留。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

### 迁入或覆盖的旧测试
- OnboardingConfigTests：`错误码编码解码往返，客户端类型与主机错误码一一对应` 的旧主机部分（其余在组合者 Catalog）

## 成员 `ProbeSlot`

### ProbeSlot

模型服务探活的归属与结果缓存（迁自旧 `Views/SettingsProviders.fs` 的 `activeProbe`、`probeResults`，及编辑器打开时的模型清单回填规则）。编辑器的按钮与输入框回调、提示文字（ProviderProbeText）仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **结果只交给发起它的编辑器。** 不是当前编辑器的模型服务时不夺归属，只缓存；绝不后台自动保存（W2.5）。
2. **非空缓存优先于配置**；默认模型不在清单里时取第一项。

### SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

### 迁入或覆盖的旧测试
- SettingsViewTests：`SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`（归属与缓存部分；输入框回填留旧仓库）

## 成员 `ProviderForm`

### ProviderForm（库）

旧 `Views/SettingsProviders.fs` 里模型服务编辑器 saveCore 的校验段与 `isLoopbackUrl` 迁入：`validate` 返回各值、按字段的错误（`ProviderField` 加了 RequireQualifiedAccess）和自动选用的默认模型（`autoPicked`，配合 `autoPickedNote`）。端点归一（ProviderPresets.normalizeBaseUrl）、载荷、是否要密钥都属 ProviderPresets（writer 2），由调用方做。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一组输入，结果与旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/ProviderForm.Tests.fs` 通过：`empty default picks the first model and reports it`、`blank timeout and retries keep the existing values, else 120 and 2`、`models split on newline and comma and dedupe`、`vision lists must name listed models and not both`。
2. 默认模型留空时自动选第一个；超时与重试留空时沿用原值，否则用 120 和 2；看图两栏只能写已列出的模型，且同一模型不能两栏都写。
3. 已知旧行为，原样保留：即使有错误，`autoPicked` 也照样给出（旧代码有错时也会回填并提示）。

### 迁入或覆盖的旧测试
旧 SettingsProviders 的用例要起窗口，留在旧仓库。本包新增 5 条规则用例。

## 成员 `ProviderProbeText`

### ProviderProbeText（库）

旧 `Views/SettingsProviders.fs` 的看图说明（visionAutoText 的 describe）与探活回执（ApplyProbe）的文案和分支迁入：`visionLine`、`visionSummary`、`feedback`。看图判断用本库自己的 `VisionVerdict` 和 `VisionWhy`（与 ModelVision 同形，组合时映射）；错误说明先由调用方用 ProbeFailureKind.parse 解析。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一组输入，文案与分支和旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/ProviderProbeText.Tests.fs` 通过：`open editor fills models, closed editor caches them`、`failure shows the parsed message as a failure`、`vision summary lists at most twelve`。
2. 编辑器打开时把模型填进草稿；没打开时只缓存，绝不自动保存；看图说明最多列 12 个模型。

### 迁入或覆盖的旧测试
旧探活用例要起窗口，留在旧仓库。本包新增 3 条规则用例。

## 成员 `SectionMemory`

### SectionMemory

分区的构建缓存与滚动位置（迁自旧 `Views/SettingsView.fs` 的 `builtSections`、`scrollPositions`、`current`、`Select` 里的记位/恢复规则、`Get/SetSectionScrollOffset`）。控件、滚动容器与导航按钮仍归宿主。分区与构建结果都是类型参数，不认识 SettingsSection。公开成员：`Current`、`TryBuilt`、`Keep`、`Invalidate`、`Select`、`Offset`、`ApplyOffset`。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **每个分区只构建一次**，失效后才重建（重复构建会把同一控件挂到两个父级上）。
2. **切换才记位与恢复**：离开时位置大于 0 或该分区还没记过才记下；切到的分区没记过就回到顶部。

### SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

### 迁入或覆盖的旧测试
旧 SettingsViewTests 的滚动位置用例要起窗口，留旧仓库。本库新增 3 条行为用例。

## 成员 `ToolsPolicy`

### ToolsPolicy（库）

旧 `Views/SettingsTools.fs` 的 `policyLabel` 与 `toolsPayload` 迁入，原样照抄（原本就是纯函数）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **文案与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/ToolsPolicy.Tests.fs` 通过：`unknown policy reads as automatic`、`payload nests under tools with default first`。

### 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本包新增 2 条规则用例。

### 合并说明

2026-10-21（W7-CLEANBREAK）并入本包：源文件按各成员**原工程的编译顺序**落盘为本包源文件；
测试按 MERGE.md《测试工程的合并约定》改名 `tests/GenerationDraft.<原文件名>.fs`，顶层模块改名 `<成员名><模块名>`；
原 `Wanxiang2.GenerationDraft` 包不再交付，消费方改引 `Wanxiang2.DevicesPane`。命名空间与类型名不变，消费方源码零改。
