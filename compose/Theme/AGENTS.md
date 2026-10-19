# Theme（组合，主题管理者）

拥有全部活的样式值并负责切主题；控件库从不认识它，组合者从它取实例填进各控件的 Look。协作者（包）：ThemePalette（配色数据）、ThemeMetrics（度量初始值）、MotionSwitch（减弱动效与时长）、ThemeTween（补间记账）、SpacingRamp（间距刻度）、ScrollTween（easeOutCubic 曲线）、ColorBlend（悬停叠色）、ThemeFonts（内嵌字体，正文与等宽字体的资源地址）。来源：旧 `Design/Tokens.fs`、`Design/MotionPolicy.fs`、`Controls/Primitives.fs` 与 `Controls/Interaction.fs` 的外观部分（extract-ledger@0c0b2a6）。根规则见项目根目录 AGENTS.md。

## 活的值
- `ThemeManager.Brush key`：每个配色项一支 `SolidColorBrush`，生命周期内同一实例，切主题原地改色。`Derived HoverRaised | HoverDanger | HoverSelected`：叠色画笔，基础画笔每次改色都重算。
- `Metric m`、`Space s`、`Timing t`、`FontFamily`、`MonoFontFamily`：`LiveValue<'T>`（实现 `IObservable<'T>`，订阅先给当前值），与控件模板 Look 里的 `IObservable<_>` 直接相容，模板不用改。
- `Apply mode`：经 ThemeTween 补间（200 毫秒，帧间隔 1/12），后来的切换接管；减弱动效或同主题同步到位。代次计数 `Generation`：过期帧只画不停帧。`CompleteTransition`、`Pump`（`autoFrames = false` 时由调用方推帧，测试据此控制时间）、`Changed`、`ShadowPopup` / `ShadowDialog`、`StateTransitions`。

## 资源字典与原生控件 ControlTheme（新 UI 规则底座）
- `ThemeManager.Resources`：Avalonia `ResourceDictionary`，包含：
  - `Palette.<Key>` 与 `Brush.<Key>`：管理者的活画笔实例（原地改色，DynamicResource 获取）。
  - `ThemeDictionaries[ThemeVariant.Light / Dark]`：Light/Dark ThemeVariant 的标准调色板映射。
  - `Metric.<Name>`、`Space.<Name>`、`Timing.<Name>`、`FontFamily.Body`、`FontFamily.Mono` 以及预置派生值 `CornerRadius.Radius*` / `Thickness.BorderThin`：LiveValue 改值时自动同步字典值。
- 原生控件样式与 ControlTheme（`ThemeStyles.InstallControlThemes` / `ThemeStyles.InstallAll`）：
  - `Button` / `ToggleButton`：去默认聚焦蓝框（`FocusAdorner = null`），支持 `primary` / `secondary` / `ghost` / `danger` 及对应 `button-*` 别名，`:focus-visible` 挂统一 Accent 描边（≥ 3:1），`:pointerover` / `:pressed` / `:disabled` 伪类齐备。
  - `TextBox` / `TextArea`（`text-area`）：边界描边统一接入 `ControlStroke`（WCAG 1.4.11 对四种底色均 ≥ 3:1），聚焦切 Accent 描边；支持 `code-box` 等宽代码输入。
  - `ComboBox`：静止态接入 `ControlStroke`，`:pointerover` 换 Line 描边，`:focus-visible` 换 Accent 描边。
  - `FlyoutPresenter` / `MenuFlyoutPresenter`：挂万象统一圆角（`RadiusLg`）、底色（`SurfaceRaised`）与描边（`Border`）。
  - 声明式动效类名：`anim-breathe`（呼吸）、`anim-spin`（360度旋转），当 `MotionSwitch.IsReduced` 打开时时长自动归零。

## 样式类（`ThemeStyles`，`StyleClass` 列出类名）
`surface` 与 `hover-plain | hover-accent | hover-raised | hover-danger | hover-bordered`、`focus-soft`、`control-stroke`、`focus-by-program`（按压、禁用、悬停、焦点环，次序同旧 Interaction）；`primary | secondary | ghost | danger` 与兼容旧类名 `button-primary | secondary | ghost | danger`；`icon-button`、`icon-button-accent`；`toggle-track`（`on` 为开启态）、`toggle-knob`；`text-area`、`code-box`；`grouping-card`；`tag`、`tag-warning`、`tag-danger`；`hairline`；`section-label`（TextBlock，旧 Ui.sectionLabel）；`menu-item`（菜单项静止底透明）、`select-button`（下拉选择器的底与描边）；声明式动效 `anim-breathe`、`anim-spin`。旧 Border 类名选择器与原生控件样式并存，P2 逐面板迁移。

# LEMMA
1. **切主题时已建好的控件跟着变，不重建视图树。** [根 AXIOM: 5]
2. **补间途中再切换，从当前色接着走，不跳色，两段不叠加。**
3. **改一项尺寸，已显示的控件跟着变。**

# SPEC
1. [LEMMA: 1] `tests/Tests.fs`：`switching the theme recolors controls that were built before the switch`、`hover over a raised surface follows the theme`、`reduced motion and complete transition land at once`。
2. [LEMMA: 2] `a second switch mid-tween takes over from the current color`（含过期代次的帧不停帧）。
3. [LEMMA: 3] `changing a metric restyles controls already on screen`、`live values push to bound controls`、`button classes carry the old geometry`（几何取旧 Ui.button / iconButton 的值）、`state transitions are zero under reduced motion`。
4. 控件库挂的类在本主题下有外观并随切换走：`tests/WidgetClassTests.fs` 的 `menu items hover and press through theme classes and follow a theme switch`、`select button rests on surface and hover swaps the stroke`、`toast action is a secondary button that hovers and presses`（测试工程引用 PopupMenu、ToastStrip 包，组合本身不依赖它们）。
5. 原生控件样式与活值底座：`tests/Tests.fs` 的：
   - `native Button recolors foreground and background when ThemePalette changes`
   - `native Button focus-visible and focus adorner removal works`
   - `native TextBox uses ControlStroke and respects a11y 3:1 ratio`
   - `reduced motion sets declarative animations duration to zero`

# 与旧代码的差异
- 旧 hover-raised / hover-danger 的叠色在注册样式时算一次，之后切主题不变；这里随主题重算。
- 旧焦点环颜色在注册时取一次；这里随主题更新。
- 减弱动效开关是管理者拥有的实例，不再是全局模块状态。

# 迁入或覆盖的旧测试
旧 `UiStabilityTests.theme color transition tweens brushes and the latest switch takes over` 的规则由 SPEC 1、2 覆盖；旧 Interaction 与 Primitives 的窗口用例（焦点环、按压、禁用）仍在旧仓库，等宿主改用本组合后再迁。
