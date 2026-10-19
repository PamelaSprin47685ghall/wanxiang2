# ReadingLook（组合，正文与公式的外观填写）

用主题管理者的活实例填 `MarkdownMetrics` 与 `MathFigureMetrics`（旧 Tokens、ControlMetrics、Spacing、MotionLedger 的同名值），按钮挂 ThemeStyles 安装的共享样式类。协作者（包）：Theme、IconSet、MarkdownView、MathFigure、CopyButton、ThemePalette、ThemeMetrics、MotionSwitch、SpacingRamp。根规则见项目根目录 AGENTS.md。

## 接口
- `ReadingLook.markdown manager styles`、`ReadingLook.math manager`、`ReadingLook.copy manager`、`ReadingLook.reduced manager`。
- 字距强调档没有对应的度量项，取 SpacingRamp 的值作为本组合自己的活值。

# LEMMA
1. **用真实主题填好的 MarkdownView，渲染出的控件树与旧 MarkdownRenderer 逐行相同。** [根 AXIOM: 5]
2. **切主题、改度量，已显示的正文跟着变。**

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/markdown.txt` 由 `tools/mdgold` 生成：引用旧仓库构建出的 Wanxiang.UI（`tests/Wanxiang.Tests/bin/Debug/net10.0`），16 段样例（`tests/Samples.fs`）各渲染两遍（不装后端；装查表后端，公式 HTML 取 `tests/golden/figure-html.txt`，即 MathFigure 金样的副本），用共用的 `tests/Dump.fs` 在内嵌正文字体的窗口里转写解析后的属性（位置尺寸、外边距、内边距、画笔颜色、字体、字号、行高、文字片段、读屏名与角色、提示、网格与停靠、图标几何与画笔、公式基线）。`every sample renders the same tree as the old MarkdownRenderer` 逐行相同（744 行）。
2. [LEMMA: 2] `theme switch recolors shown code in place and metric edits reach shown blocks`、`code header buttons wear the shared icon button classes from the theme`。

# 与旧代码的差异
无行为差异；外观值从主题管理者取，旧代码直接读全局 Tokens。

# 迁入或覆盖的旧测试
旧 UiStabilityTests `markdown and primitive controls consume shared craft metrics` 由 SPEC 1（几何同旧）与 SPEC 2（值来自主题）覆盖。
