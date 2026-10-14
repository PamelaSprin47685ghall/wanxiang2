# StartLayout（库）

旧 `Views/MainLayout.fs` 的两处计算迁入：起始页是否居中（`centered`）与侧栏宽度钳制（`sidebarWidth`）。门槛由调用方传入：LayoutPolicy.startWorkspaceMinHeight，Tokens.sidebarMinWidth 与 MaxWidth（232 与 420）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/startlayout.txt`。

# 迁入或覆盖的旧测试
旧布局用例要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
