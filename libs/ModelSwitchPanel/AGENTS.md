# ModelSwitchPanel

快速切换模型的浮层内容：搜索框、按模型服务分组的行（默认标签、当前打勾）、空结果播报、「从下一条回复开始生效」说明（迁自旧 `Views/ModelSwitcherView.fs`）。根规则见项目根目录 AGENTS.md。

- 插槽 `ModelSwitchSlots`：`Groups`（组合者填 ModelSwitcher.filter + ModelPickerList.groups）、`SearchHelp` / `EmptyText`（ModelPickerList）、`AppliesNote`（ModelSwitcher.appliesNote）。
- 激活（点击、行上 Enter/Space、搜索框 Enter 选第一条命中）调用 `activate`；组合者先关浮层，再按 ModelPickerList.shouldPick 决定是否真正切换（当前项不切换）。放进浮层（旧 ShowPopup 宽 300）也归组合者。
- 键盘：搜索框 Down 进列表首行；列表内 Up/Down 不循环，首行再上回搜索框（与 ModelPickerList.move 同规则，本库重写）。
- 读屏：行角色 MenuItem，名字是模型名，HelpText「服务，默认，当前」；tooltip「服务 · 模型」。

## 外观

- `ModelSwitchPanelLook`：画笔 `Text`、`Muted`、`Faint`、`Accent`、`TagText`、`TagSurface`、`FieldSurface`、`FieldEdge`、`Caret`；图标插槽 `Check`；字号、边距、尺寸、面板宽度都是活的值。
- 用到的样式类（compose/Theme）：行 `model-row menu-item surface hover-plain`；组头 `section-label`；分隔 `hairline`；输入壳 `surface focus-soft`。

## 证据与旧测试

- ModelChipUiTests：`switcher shows provider, default and current, plus the next reply note`、`typing filters rows and enter picks the first hit`、`picking the current model is a no-op`（shouldPick 由测试里的组合者替身给）
- A11yRegressionTests：`模型切换器写明键盘路径且空结果会播报`（旧测试查源码字符串，这里改查控件属性）
- 新增：列表方向键与组头显示。

## 与旧实现的差异

- 选项类型改名 `ModelChoice`，字段大写开头。
- 关浮层与「当前项不切换」从控件移到组合者：控件只发激活。
