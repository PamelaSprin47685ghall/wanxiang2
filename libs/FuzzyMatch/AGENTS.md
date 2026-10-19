# FuzzyMatch

合并包：FuzzyMatch、PinyinInitials。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `FuzzyMatch`

## FuzzyMatch

子序列模糊匹配打分（从旧 `CommandPalette.fuzzyScore` 拆出，改名 `FuzzyMatch.score`）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **分值与旧实现一致。** [根 AXIOM: 5] 面板排序依赖分值；测试钉住了旧实现的具体数值。

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- 无单独旧测试；经 compose/CommandPalette 迁入的 R20 用例间接覆盖。

## 成员 `PinyinInitials`

## PinyinInitials

汉字转拼音首字母（GB2312 一二级字表，从旧 UI `Model/CommandPalette.fs` 原样拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5] 用户凭首字母找对话，结果不能随迁移改变。

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- R20PaletteTests：`373 拼音首字母覆盖常用字`
