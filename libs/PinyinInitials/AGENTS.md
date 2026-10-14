# PinyinInitials

汉字转拼音首字母（GB2312 一二级字表，从旧 UI `Model/CommandPalette.fs` 原样拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5] 用户凭首字母找对话，结果不能随迁移改变。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- R20PaletteTests：`373 拼音首字母覆盖常用字`
