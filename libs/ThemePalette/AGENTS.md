# ThemePalette（库）

旧 `Design/Palette.fs` 的两套配色数据迁入：`PaletteMode.Light | Dark`、`PaletteKey`（旧字段逐项，外加旧 Tokens 的两支派生色 `SurfaceSoft`、`UserBubbleSelection`，以及本仓库补的 `ControlStroke`）、`ThemePalette.keys`、`ThemePalette.color mode key`。只有数据；活的画笔、补间与切换归主题管理者（`compose/Theme`）。依赖 Avalonia（`Color`）。不引用任何其他库。模块名避开旧宿主的 `Palette`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **每一项颜色与旧实现相同。** [根 AXIOM: 5]
2. **`ControlStroke` 是本仓库补的令牌，不是旧配色数据。** 语义：描边就是边界的控件（输入壳、开关轨道）用它；对四种基础表面（Surface / SurfaceRaised / SurfaceContainer / Canvas）两套主题都 ≥ 3:1（WCAG 1.4.11 非文本对比）。 [根 AXIOM: 1；根 SPEC 6]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`both modes cover every key and differ on the canvas`、`surface soft follows the surface of the same mode`。
2. [LEMMA: 2] 浅 `#8D8A7E`（对 Surface 3.31 / SurfaceRaised 3.46 / SurfaceContainer 3.14 / Canvas 3.14）、深 `#76767F`（3.51 / 3.07 / 3.68 / 4.04）；机械断言在 `compose/Theme/tests/A11yRegression.fs` 的 `控件边界描边在四种底上都过 1.4.11`。

# 迁入或覆盖的旧测试
旧仓库的对比度与配色用例（如 `A11yRegressionTests.代码块内全部着色与弱化文字在代码面和代码头上过 AA`）经旧 Tokens 取色，留在旧仓库；配色值本身由 `compose/Theme/tests/A11yRegression.fs` 的对比度断言覆盖（见 SPEC 2）。
