# GeneralPane

设置里的三块通用分区（迁自旧 `Views/SettingsGeneral.fs` 的界面部分）：对话默认值、外观与交互（含本机草稿）、关于与系统诊断（含主机状态）。根规则见项目根目录 AGENTS.md。

- `GenerationSection(look, validationFor)`：五个数值框（两列表单，窄于 `FormSingleColumnBreakpoint` 退化为单列）、多行系统指令、「自动生成对话标题」开关、错误摘要（`Summary`）、保存状态行（polite）、保存 / 放弃、对话预设列表、导入入口。
  - `Read()` / `Fill GenerationText`；`SetStatus`；`SetPending(pending, canSave)`；`SetPresets`；`SetImportVisible`。
  - 事件：`Edited`（任一字段或开关变化，填表时也发）、`SaveRequested`（保存键、任意处 Ctrl/⌘+Enter、数值框回车；指令框回车是换行）、`DiscardRequested`、`PresetRenameClicked id`、`PresetDeleteClicked id`、`ImportClicked`。
- `AppearanceSection(look)`：主题选择器（读屏 ComboBox，`ThemeClicked anchor`）、字号 −/+（`FontStepped ±0.5`；到边界由 `Render` 禁用）、五个交互开关与「保存未发送的草稿」（`Toggled(AppearanceSwitch, 值)`，只在用户拨动时发）、本机草稿概况与「清除本地草稿」（`ClearDraftsClicked`）。`Render AppearanceState`。
- `AboutSection(look)`：`SetRows [(键, 值)]`（等宽、可选中）、复制诊断报告（`CopyDiagnosticsClicked`）、`SetHealth(配置问题, AboutDoctor)`（检查中按钮禁用，`CheckHostClicked`）、设计与致谢卡。
- 草稿状态（GenerationDraft）、校验（GenerationForm + FieldCheck）、写回、偏好（UiPrefs）、主题菜单、剪贴板、确认框、HostHealth 订阅都由组合者接。
- 自动化：`settings-presets`、`preset-row`、`settings-import`、`local-drafts-summary`、`clear-local-drafts`、`host-config-issue`、`host-config-issue-detail`、`host-config-ok`、`check-host`、`doctor-headline`、`doctor-check-<name>`，与旧实现相同。

## 外观

- `GeneralPaneLook`：公共字段加 `SpaceXXs`、`TextAreaMinHeight`、`FormSingleColumnBreakpoint`、`AboutKeyMinWidth`、`FontSizeValueMinWidth`、`PendingMinWidth`、`SelectCaptionMaxWidth`、`SelectPaddingY`、`Selection`。`Glyph` 用到：`minus`、`plus`、`chevron-down`、`check-circle`、`alert`。
- 样式类：按钮 `surface button-* hover-*`、图标按钮 `icon-button`、选择器 `surface select-button hover-bordered`、卡 `grouping-card`、分隔 `hairline`、开关 `toggle-track` / `toggle-knob` / `on`、输入壳 `surface hover-raised focus-soft`。

## 证据与旧测试

- SettingsSurfaceTests：`editing a generation number stays local until explicitly saved`、`generation save button advertises the ctrl enter shortcut`、`generation form fields sit inside a grouping card`、`generation form consumes shared column spacing and text metrics`、`about section drops the misplaced spacer`
- SettingsViewTests：`SettingsGeneral BuildAppearance adjusts fontScale within bounds and updates prefs`（界面部分；夹值在组合者）、`SettingsGeneral BuildAbout formats system diagnostics with version and platform`（界面部分；报告文字在组合者）
- 组合者 SettingsScreen 覆盖：`SettingsGeneral SaveGenerationCore validates numeric ranges and generates payload`、`generation save failure surfaces a failure toast instead of silence`
- 新增：`validation rows sit in each field group and hide the hint`、`presets list offers rename and delete per preset and an empty explanation`、`theme selector reads as a combo box and opens the menu at itself`、`host status shows config issues and doctor results`

## 与旧实现的差异

- 两列表单的断点是主题度量（活值），改了立即重排。
- 外观页的开关在组合者给值时不触发事件；旧实现同样只在用户拨动时写偏好。
