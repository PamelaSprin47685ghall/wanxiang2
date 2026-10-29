# ModelSwitchPanel

合并包：ModelSwitchPanel、ModelPickerList。生产消费者集合相同，且合并后的依赖并集（`Avalonia`）已被消费者 `compose/ModelSwitchDesk` 直接声明——按 MERGE.md《追加判据（2026-10-21 W7-CLEANBREAK）：依赖闭包判据》合并是纯减法，消费者不多拿任何 API。

快速切换模型的浮层内容：搜索框、按模型服务分组的行（默认标签、当前打勾）、空结果播报、「从下一条回复开始生效」说明（迁自旧 `Views/ModelSwitcherView.fs`）。根规则见项目根目录 AGENTS.md。

- 插槽 `ModelSwitchSlots`：`Groups`（组合者填 ModelSwitcher.filter + ModelPickerList.groups）、`SearchHelp` / `EmptyText`（ModelPickerList）、`AppliesNote`（ModelSwitcher.appliesNote）。
- 激活（点击、行上 Enter/Space、搜索框 Enter 选第一条命中）调用 `activate`；组合者先关浮层，再按 ModelPickerList.shouldPick 决定是否真正切换（当前项不切换）。放进浮层（旧 ShowPopup 宽 300）也归组合者。
- 键盘：搜索框 Down 进列表首行；列表内 Up/Down 不循环，首行再上回搜索框（与 ModelPickerList.move 同规则，本库重写）。
- 读屏：行角色 MenuItem，名字是模型名，HelpText「服务，默认，当前」；tooltip「服务 · 模型」。

## 外观

- 废除 `ModelSwitchPanelLook`，外观全走 Avalonia 样式类与主题资源。
- 用到的样式类（compose/Theme）：行 `model-row menu-item surface hover-plain`；分组标题 `section-label`；分隔 `hairline`；输入壳 `surface control-stroke`；文本类 `muted`、`faint`。

## 证据与旧测试

- ModelChipUiTests：`switcher shows provider, default and current, plus the next reply note`、`typing filters rows and enter picks the first hit`、`picking the current model is a no-op`（shouldPick 由测试里的组合者替身给）
- A11yRegressionTests：`模型切换器写明键盘路径且空结果会播报`（旧测试查源码字符串，这里改查控件属性）
- 新增：列表方向键与组头显示。

## 与旧实现的差异

- 选项类型改名 `ModelChoice`，字段大写开头。
- 关浮层与「当前项不切换」从控件移到组合者：控件只发激活。

## 成员 `ModelPickerList`

## ModelPickerList（库）

旧 `Views/ModelSwitcherView.fs` 的列表呈现迁入：`groups`（按模型服务分组，只在无查询且多组时显示组头）、`enterPick`（回车选第一条）、`shouldPick`（当前模型不再切换）、`move`（列表内上下，不循环，在顶端上移回搜索框；`PickerMove` 加了 RequireQualifiedAccess），以及文案。筛选本身在 ModelSwitcher.filter。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一组输入，结果与旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/ModelPickerList.Tests.fs` 中 `headers only without a query and with several providers` 覆盖组头条件，`up from the top returns to search; down stops at the end` 覆盖方向键边界。`tests/Tests.fs` 中 `typing filters rows and enter picks the first hit`、`picking the current model is a no-op` 覆盖组合后的回车选择与当前项行为。本次已核对用例内容，未重新运行测试。

### 迁入或覆盖的旧测试
旧模型切换浮层用例（决策 304）要起窗口，留在旧仓库。本库新增：2 条规则用例与文案断言。

### 合并说明
2026-10-21（W7-CLEANBREAK）并入本包：源文件 `ModelPickerList.fs` 落盘为本包源文件（编译序在 `ModelSwitchPanel.fs` 之后）；测试按 MERGE.md《测试工程的合并约定》改名 `tests/ModelPickerList.<原文件名>.fs`，顶层模块改名 `ModelPickerList<模块名>`（`ModelPickerListTests` / `ModelPickerListSamples` / `ModelPickerListRender`）；原 `Wanxiang2.ModelPickerList` 包不再交付，消费方 `compose/ModelSwitchDesk` 的 `PackageReference` 改为 `Wanxiang2.ModelSwitchPanel`。命名空间与类型名不变（仍是 `Wanxiang.UI` 下的 `ModelPickerList` 模块与 `PickerMove` 类型），消费方源码零改。
