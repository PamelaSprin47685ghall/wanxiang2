# UiKit（库）

控件库共用的基础原语：活值订阅（`Follow`）、可点 Border（`Tap`、`Spot`、`InvokeBorder`）、按钮档 / 标签语气（`Tone`、`TagTone`）、共享 Kit（`KitLook` + `Kit`，含对话框页脚 `DialogFooter`）。迁自 36 个控件库各自复制的同名内部文件（内容相同），是控件库唯一声明的基础依赖；unwire 检查的例外登记处见根 AGENTS.md。

- `Follow`：`now`、`live`、`live2`、`live3`（挂上可视树才订阅，摘下退订，构造时先应用当前值）。
- `Tap`：Child 版（`Tap(child, name, classes, action)`，读屏 Button + Invoke，Enter / Space / 左键松开触发）与 `Tap.TextButton(text, classes, action)`；`SetText` / `Text` / `SetEnabled`。
- `Spot`：自绘 Border 版（`OnInvoke`）；`InvokeBorder`：`SetAction`，键盘与指针由调用方接。
- `KitLook`：控件库 Kit 需要的外观项（25 项，全部活实例或画笔；`Glyph` 是图标槽）。
- `Kit`：`Button` / `IconButton` / `Text` / `Label` / `Caption` / `Heading` / `Title` / `SectionLabel` / `FieldLabel` / `VStack` / `HStack` / `Card` / `Hairline` / `Tag` / `StatusDot` / `TextField` / `TextArea` / `FieldGroup` / `Toggle` / `RowContent` / `RowShell` / `SwitchRow` / `DialogFooter` / `Live` / `Pair`。

# LEMMA

1. **本库只放控件原语与活值管线，不放领域规则，也不依赖任何 `Wanxiang2.*` 包。** [根 AXIOM: 2, 3; 根 LEMMA: 5, 8]
2. **控件库的唯一跨库依赖就是本库；行为与状态仍归各控件库自己。** [根 LEMMA: 4, 5]
3. **迁移前后行为逐字相同。** 本库文件是从 36 份复制件合并出来的同一段代码，member 名、类名、class 名、tab 停靠与读屏状态都未改。[根 AXIOM: 5]

# SPEC

1. [LEMMA: 1] 本库 `UiKit.fsproj` 不引用任何 `Wanxiang2.*` 包；`./x check` 强制。
2. [LEMMA: 3] `tests/Tests.fs` 在 Avalonia headless 下覆盖：`Follow` 三段生命周期（初值 / 跟随 / 摘下停订）与 `live2`/`live3`；`Tap` 键盘、指针、读屏三条路径与 `SetEnabled` 门控、`TextButton` 名字同步；`Spot` 与 `InvokeBorder`；`Kit` 文案角色跟活值、`TextField` 聚焦换边与 invalid 让步、`Toggle` 指针与翻转、`SwitchRow` 行级 Enter、`DialogFooter` 左右键与 Danger 不做行级确认、`Pair` 的到达与退订。
3. [LEMMA: 3] 36 个控件库迁移后各自测试通过（各库 AGENTS.md 记录自己的证明）。

# 样式类

本库只挂类名：`surface`、`button-primary`/`button-secondary`/`button-ghost`/`button-danger`、`hover-plain`/`hover-raised`/`hover-accent`/`hover-danger`、`icon-button`、`focus-soft`、`grouping-card`、`hairline`、`section-label`、`tag`/`tag-warning`/`tag-danger`、`toggle-track`/`toggle-knob`/`on`。

# 迁入或覆盖的旧测试

旧仓库的按钮 / 开关 / 输入用例留在旧仓库；各控件库迁入的行为测试覆盖各自控件，本库补原语自身的 headless 用例（上面的 SPEC 2）。
