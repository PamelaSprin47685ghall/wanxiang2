# ProvidersPane

设置 → 模型服务分区的列表，以及「添加 / 编辑模型服务」对话框的内容（迁自旧 `Views/SettingsProviders.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `ProvidersPane(look)`：`View`；`Render ProviderRow list`（状态点 + 名称 + 状态标签「已配置 / 缺少密钥（警示）/ 已停用」+「默认」标签 + 地址行；停用时名称弱化，编辑与更多仍可点）。列表为空时显示空态卡，「添加模型服务」由空态里的主按钮接管、顶部同名键隐藏。
  - 事件：`AddClicked`、`EditClicked id`、`MoreClicked(id, 键)`。
- `ProviderEditor(look, isNew, validationFor)`：预设选择器（读屏 ComboBox，`PresetClicked anchor`）、传输说明（`SetKindText`）、预设说明（`SetPresetHint`，空则隐藏）、错误摘要（`Summary`）、快速区（名称、密钥 ●、地址）、「高级设置 ▼ / 收起高级设置 ▲」（`IsAdvanced`、`SetAdvanced`）、高级区（传输切换 `SetTransport` / `TransportClicked`、标识、模型列表、默认模型、获取模型列表 `ProbeClicked` / `SetProbePending` / `SetProbeStatus`、看图能力 `SetVisionCurrent` 与两框、超时、重试、启用开关 `Enabled` / `SetEnabledValue`）、取消 / 添加（或保存）。
  - 单行框回车、任意处 Ctrl/⌘+Enter 发 `SaveRequested`；多行框回车是换行。`SetPending true` 时保存键换「正在保存…」并禁用、取消也禁用、不再发 `SaveRequested`。页脚左右方向键在两键间移动。
  - 新建时「获取模型列表」禁用并就地说明要先保存。`InitialFocus` 是名称框。`LockId()` 让编辑时标识只读。`AdvancedBoxes` 是高级区里的框（出错时组合者先展开）。
- 预设套用与分歧判定（ProviderPresets）、传输切换、探活（ProbeSlot、ProviderProbeText）、校验（ProviderForm + FieldCheck）、写回、菜单与对话框都由组合者接。
- 自动化：`provider-probe-models`、`provider-vision-yes`、`provider-vision-no`、`provider-vision-current`，与旧实现相同。

## 外观

- `ProvidersPaneLook`：公共字段加 `SpaceXXs`、`TextAreaMinHeight`、`EmptyLogoSize`、`PendingMinWidth`、`StatusDotSize`、`SelectCaptionMaxWidth`、`SelectPaddingY`。`Glyph` 用到：`pencil`、`more`、`chevron-down`、`logo`。
- 样式类：按钮 `surface button-* hover-*`、图标按钮 `icon-button`、选择器 `surface select-button hover-bordered`、卡 `grouping-card`、标签 `tag` / `tag-warning`、分隔 `hairline`、开关、输入壳 `surface hover-raised focus-soft`。

## 证据与旧测试

- SettingsSurfaceTests：`missing key reads as warning tag and disabled provider dims its text`
- SettingsViewTests：`SettingsProviders two-tier form defaults to quick access and preserves drafts when toggling advanced settings`、`SettingsProviders ShowEditor validates required fields and default model existence`（界面与键盘部分；校验在组合者）、`SettingsProviders applyPreset updates endpoint, models and tracks divergence to custom`（选择器与说明部分；套用与分歧在组合者）
- 组合者 SettingsScreen 覆盖：`SettingsProviders saveCore with invalid advanced field automatically expands advanced layer and focuses error`、`SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`
- 新增：`empty provider list hands the add action to the empty state`、`new provider cannot probe until saved and explains why`、`look values stay live in the editor`

## 与旧实现的差异

- 无可见差异。空态卡是本库的简化形态（标志 + 说明 + 主按钮）。
