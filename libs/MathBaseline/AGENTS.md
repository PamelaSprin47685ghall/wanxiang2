# MathBaseline（库）

旧 `Rich/MathVisual.fs` 的 `Metrics`（盒宽、盒高、盒内基线）与 `Baseline`、`Render` 里重复出现的基线选择迁入：`metrics`、`drawBaseline`。盒子以 `(width, above, below)` 传入（MathTypeset 的 MathBox 字段），宿主基线仍由控件沿可视树向上找 TextBlock 求得。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一盒子与宿主基线，尺寸与绘制基线与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`inline formula aligns to the host text baseline`、`box height spans above and below and the baseline sits at above`、`display mode ignores the host baseline`、`inline baseline lands on the host line baseline in host coordinates`。

# 迁入或覆盖的旧测试
旧 MathBaselineTests 要真实排版，留在旧仓库。本库新增：行内对齐宿主基线、行内基线落在宿主行基线、盒高跨基线上下。

# 真实行为测试
`tests/Tests.fs` 按上面写明的规则直接断言新实现，规则改了要同时改这里。
