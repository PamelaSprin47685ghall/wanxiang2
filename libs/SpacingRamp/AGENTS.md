# SpacingRamp（库）

旧 `Wanxiang.UI/Design/Spacing.fs` 原样迁入：Fluent 2 间距刻度、槽位、圆角、描边、字距、图标、命中区，以及判定函数。模块名仍是 `Spacing`。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **全部公开值与判定结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/spacing.txt` 由旧 Spacing.fs 反射列出全部公开值（含嵌套模块），并对 13 个数值求各判定函数。

# 迁入或覆盖的旧测试
旧测试里没有不依赖 Avalonia 的 Spacing 用例（DesignConvergenceTests 等扫描界面源码，留在旧仓库）。本库新增：金样、刻度递增、刻度外数值、geometry 覆盖范围。
