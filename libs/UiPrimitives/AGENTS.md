# UiPrimitives

历史遗留的控件原语库（迁自旧 `Controls/Primitives.fs` 的 `Ui` 模块与 `ActionBorder`/`ToggleBorder`）。无样式原语：`InvokeBorder`/`ToggleBorder` 把「按钮」「开关」语义暴露给自动化层，`ButtonTone`/`TagTone` 是语义层级，`Primitives` 工厂产出全应用统一的文字、结构、徽标、忙指示器、按钮、输入、开关、字段组、设置行与空态。命名空间为 `Wanxiang2.UiPrimitives`（历史遗留，与其他库的 `Wanxiang.UI` 不同）。依赖 Avalonia。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **不写任何字面样式值。** 颜色、尺寸、间距、圆角、时长全部由主题管理者（`compose/Theme` 的 `ThemeStyles` 与活实例）提供：`PrimitivesLook` 记录持有 `IObservable`/`IBrush` 活值，状态视觉（焦点环、悬停、按压、禁用）按类名给。
2. **语义与视觉分离。** 本库只暴露语义（自动化模式、tone 层级、类名），不接管测量、校验状态与跨库协作；提示条抑制、校验描边、输入方式由组合者接线。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 在 Avalonia headless 下覆盖：`button`/`tagWith` 类名、`toggle` 翻转与写值、`setEnabled`/`setSquareTarget`/`setReservedActionVisible` 状态切换、`textField` 外壳结构、活值跟随主题管理者。

# 样式类
`surface`、`icon-button`、`icon-button-accent`、`button-primary`/`button-secondary`/`button-ghost`/`button-danger`、`hover-plain`/`hover-raised`/`hover-accent`/`hover-danger`、`focus-soft`、`toggle-track`、`toggle-knob`、`on`、`hairline`、`grouping-card`、`tag`、`tag-warning`、`tag-danger`。

# 迁入或覆盖的旧测试
旧仓库的按钮/开关/输入相关 UI 测试留在旧仓库；本库新增 headless 行为测试。
