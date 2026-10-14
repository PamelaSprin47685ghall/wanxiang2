# ThemePalette（库）

旧 `Design/Palette.fs` 的两套配色数据迁入：`PaletteMode.Light | Dark`、`PaletteKey`（旧字段逐项，外加旧 Tokens 的两支派生色 `SurfaceSoft`、`UserBubbleSelection`）、`ThemePalette.keys`、`ThemePalette.color mode key`。只有数据；活的画笔、补间与切换归主题管理者（`compose/Theme`）。依赖 Avalonia（`Color`）。不引用任何其他库。模块名避开旧宿主的 `Palette`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **每一项颜色与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/themepalette.txt`（两套共 106 项 #AARRGGBB）；`tests/Tests.fs` 另两条用例。

# 迁入或覆盖的旧测试
旧仓库的对比度与配色用例（如 `A11yRegressionTests.代码块内全部着色与弱化文字在代码面和代码头上过 AA`）经旧 Tokens 取色，留在旧仓库；配色值本身由金样覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 编译照抄的 `tools/OldPalette.fs`（只改命名空间），按 `tools/keys.txt` 的顺序逐项输出；输出应与提交的金样逐字节相同。
