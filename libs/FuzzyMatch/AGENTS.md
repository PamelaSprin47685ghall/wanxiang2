# FuzzyMatch

子序列模糊匹配打分（从旧 `CommandPalette.fuzzyScore` 拆出，改名 `FuzzyMatch.score`）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **分值与旧实现一致。** [根 AXIOM: 5] 面板排序依赖分值；测试钉住了旧实现的具体数值。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- 无单独旧测试；经 compose/CommandPalette 迁入的 R20 用例间接覆盖。
