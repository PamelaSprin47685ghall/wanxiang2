# SearchRequery（库）

旧 `Views/Sidebar.fs` 搜索防抖到点时的判定迁入：词没变就不重发查询，否则迟到的 Tick 会清空刚到的命中（旧 LibraryTests 偶发失败的根因）。计时器仍归 Sidebar。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，判定与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/searchrequery.txt`。

# 迁入或覆盖的旧测试
旧 LibraryTests 搜索用例要起窗口，留在旧仓库。本库新增：金样与 1 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
