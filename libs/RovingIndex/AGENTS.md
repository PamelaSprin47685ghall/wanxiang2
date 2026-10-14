# RovingIndex（库）

旧 `Controls/Menu.fs` wireDirectionalNavigation 的落点计算（`menu`，按键用 `RovingKey`，加了 RequireQualifiedAccess）与 `Views/SettingsView.fs` NavigateSection 的循环取模（`wrap`）迁入。挑选可聚焦项、聚焦与滚动仍归控件。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，落点与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/rovingindex.txt`。

# 迁入或覆盖的旧测试
旧菜单键盘用例要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
