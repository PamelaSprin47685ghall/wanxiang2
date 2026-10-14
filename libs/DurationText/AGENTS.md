# DurationText（库）

旧 `Views/MessageCard.fs` 的 `formatDuration` 原样迁入（改名 `format`）：思考与工具耗时角标。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一毫秒数，文案与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/durationtext.txt` 由旧函数生成。

# 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本库新增：金样、零不显示、秒保留一位小数。
