# DesignRhythm（组合）

旧 `Design/LayoutPolicy.fs`（布局断点、临时区域上限、自适应水平留白）原样迁入，以及旧 `Design/Metrics.fs` 中的 `ReadingRhythm` 模块（行高、标题字号与前后间距）。协作者：SpacingRamp（包）。`Metrics.fs` 其余部分（ControlMetrics 等）引用 Avalonia 的 Tokens，留在 UI。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **全部公开值与函数结果与旧实现相同。** [根 AXIOM: 5]
2. **断点与节奏落在间距刻度上。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/rhythm.txt` 由旧代码反射列出全部公开值，并对 9 个宽度、3 个字号和 0 到 7 级标题求值。

# 迁入或覆盖的旧测试
旧测试里没有不依赖 Avalonia 的用例。本组合新增：金样、断点边界、最窄桌面窗口已是紧凑布局、留白与标题间距都在刻度上、标题字号随级别递减。
