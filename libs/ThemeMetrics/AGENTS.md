# ThemeMetrics（库）

旧 `Design/Tokens.fs` 数值段（字号、不透明度、结构尺寸、品牌尺寸）与 `Design/Metrics.fs`（ControlMetrics、ContentMetrics）共 131 项度量（末尾 16 项为布局与阅读常量：CaptionLineHeight、TouchTarget、ApprovalButtonRowMaxWidth、LogoGlyphRatio、MenuMaxHeight、SidebarSplitterWidth、StartWorkspaceMinHeight、CompactBreakpoint、ModelSwitcherWidth、PaletteDialogWidth、TransferDialogWidth、FormSingleColumnBreakpoint、DialogContentMaxHeight、PairingCodeTracking、QrModuleSize、SelectCaptionMaxWidth，取自旧 ReadingRhythm、Spacing.Hit、LayoutPolicy、ToolApprovalCard、Brand、SettingsDevices、Menu）迁入：`Metric`（旧名首字母大写）、`ThemeMetrics.all`、`ThemeMetrics.initial`，以及两种字体的资源地址。旧代码里对 `Spacing`、`ReadingRhythm` 的引用已换成数值。只有初始值；活的可变值归主题管理者。不依赖界面框架，不引用任何其他库。模块名避开旧宿主的 `Metrics`、`Tokens`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **每项初始值与旧实现相同；旧别名仍与源同值。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/thememetrics.txt`；`tests/Tests.fs` 别名用例。

# 迁入或覆盖的旧测试
旧 `DesignConvergenceTests` 一组（如 ChipDensityHasSingleSourceUnderBothNames）断言的是同值关系，本库别名用例覆盖其数据部分；起窗口的部分留在旧仓库。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 编译 `tools/OldMetrics.fs`（照抄旧 Tokens 数值段、Metrics.fs 与 SpacingRamp）逐项输出。

## 字体
不含字体地址：内嵌字体与资源地址归 ThemeFonts 库（旧 Tokens.fontFamily / monoFontFamily 对应 `ThemeFonts.body` / `ThemeFonts.mono`）。
