# DesignRhythm（组合）

旧 `Design/LayoutPolicy.fs`（布局断点、临时区域上限、自适应水平留白）原样迁入，以及旧 `Design/Metrics.fs` 中的 `ReadingRhythm` 模块（行高、标题字号与前后间距）。协作者：SpacingRamp（包）。`Metrics.fs` 其余部分（ControlMetrics 等）引用 Avalonia 的 Tokens，留在 UI。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **全部公开值与函数结果与旧实现相同。** [根 AXIOM: 5]
2. **断点与节奏落在间距刻度上。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`inset steps exactly at the breakpoints`、`narrowest desktop window is already compact`、`inset and rhythm values stay on the spacing ramp`、`heading sizes shrink with level and floor at plus one`、`断点从小到大排开，最窄窗口落在 compact 档`、`水平留白随窗口变宽单调不减，只在断点处跳档`、`四档留白取自间距刻度的固定档位`、`非法宽度落回最窄档，无穷宽度落回最宽档`、`正文行高高于次级与技术行高，且三者随字号同比缩放`、`标题字号在四级之前逐级收紧，之后不再下降`、`标题前后间距随级别收紧并落在刻度上`。

# 迁入或覆盖的旧测试
旧测试里没有不依赖 Avalonia 的用例。本组合新增：断点边界、最窄桌面窗口已是紧凑布局、留白与标题间距都在刻度上、标题字号随级别递减。
