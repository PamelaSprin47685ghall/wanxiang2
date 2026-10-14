# SidebarRowState（库）

旧 `Views/Sidebar.fs` 中 ApplyRowState 兜底分支的高亮与读屏状态，以及 ApplyRowActionVisibility 中「更多操作」键的判定迁入。多选模式下「更多操作」键整批下线。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组状态，结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/sidebarrowstate.txt`。

# 迁入或覆盖的旧测试
旧侧栏行用例要起窗口，留在旧仓库。本库新增：金样与 1 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
