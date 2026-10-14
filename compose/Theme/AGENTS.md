# Theme（组合，主题管理者）

拥有全部活的样式值并负责切主题；控件库从不认识它，组合者从它取实例填进各控件的 Look。协作者（包）：ThemePalette（配色数据）、ThemeMetrics（度量初始值）、MotionSwitch（减弱动效与时长）、ThemeTween（补间记账）、SpacingRamp（间距刻度）、ScrollTween（easeOutCubic 曲线）、ColorBlend（悬停叠色）。来源：旧 `Design/Tokens.fs`、`Design/MotionPolicy.fs`、`Controls/Primitives.fs` 与 `Controls/Interaction.fs` 的外观部分（extract-ledger@0c0b2a6）。根规则见项目根目录 AGENTS.md。

## 活的值
- `ThemeManager.Brush key`：每个配色项一支 `SolidColorBrush`，生命周期内同一实例，切主题原地改色。`Derived HoverRaised | HoverDanger`：叠色画笔，基础画笔每次改色都重算。
- `Metric m`、`Space s`、`Timing t`、`FontFamily`、`MonoFontFamily`：`LiveValue<'T>`（实现 `IObservable<'T>`，订阅先给当前值），与控件模板 Look 里的 `IObservable<_>` 直接相容，模板不用改。
- `Apply mode`：经 ThemeTween 补间（200 毫秒，帧间隔 1/12），后来的切换接管；减弱动效或同主题同步到位。代次计数 `Generation`：过期帧只画不停帧。`CompleteTransition`、`Pump`（`autoFrames = false` 时由调用方推帧，测试据此控制时间）、`Changed`、`ShadowPopup` / `ShadowDialog`、`StateTransitions`。

## 样式类（`ThemeStyles`，`StyleClass` 列出类名）
`surface` 与 `hover-plain | hover-accent | hover-raised | hover-danger | hover-bordered`、`focus-soft`、`focus-by-program`（按压、禁用、悬停、焦点环，次序同旧 Interaction）；`button-primary | secondary | ghost | danger`；`icon-button`、`icon-button-accent`；`toggle-track`（`on` 为开启态）、`toggle-knob`；`grouping-card`；`tag`、`tag-warning`、`tag-danger`；`hairline`。样式只建一次：画笔是活实例，尺寸经 `ToBinding` 绑到活值，焦点环颜色是随 `Changed` 更新的活值。这些是挂在 Border 上的类选择器样式，不是 `ControlTheme`：ControlTheme 只按控件类型或 Theme 属性生效，不能按类名套在 Border 上。

# LEMMA
1. **切主题时已建好的控件跟着变，不重建视图树。** [根 AXIOM: 5]
2. **补间途中再切换，从当前色接着走，不跳色，两段不叠加。**
3. **改一项尺寸，已显示的控件跟着变。**

# SPEC
1. [LEMMA: 1] `tests/Tests.fs`：`switching the theme recolors controls that were built before the switch`、`hover over a raised surface follows the theme`、`reduced motion and complete transition land at once`。
2. [LEMMA: 2] `a second switch mid-tween takes over from the current color`（含过期代次的帧不停帧）。
3. [LEMMA: 3] `changing a metric restyles controls already on screen`、`live values push to bound controls`、`button classes carry the old geometry`（几何取旧 Ui.button / iconButton 的值）、`state transitions are zero under reduced motion`。

# 与旧代码的差异
- 旧 hover-raised / hover-danger 的叠色在注册样式时算一次，之后切主题不变；这里随主题重算。
- 旧焦点环颜色在注册时取一次；这里随主题更新。
- 减弱动效开关是管理者拥有的实例，不再是全局模块状态。

# 迁入或覆盖的旧测试
旧 `UiStabilityTests.theme color transition tweens brushes and the latest switch takes over` 的规则由 SPEC 1、2 覆盖；旧 Interaction 与 Primitives 的窗口用例（焦点环、按压、禁用）仍在旧仓库，等宿主改用本组合后再迁。
