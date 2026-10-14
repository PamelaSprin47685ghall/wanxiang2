# BreadcrumbAction（库）

旧 `Views/ChatView.fs` 的 SetBreadcrumb、BreadcrumbText 与 ActivateBreadcrumb 状态迁入，成为泛型对象 `BreadcrumbAction<'Id>`。`Set` 接收 (文字, 说明, 父对话可用, 父对话) 或 None，返回呈现；`Activate` 返回要回到的父对话。文字由 ForkLineage.breadcrumb 给（组合时传入）；控件与自动化属性仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串操作，呈现与跳转目标和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/breadcrumbaction.txt`。

# 迁入或覆盖的旧测试
旧面包屑用例要起窗口，留在旧仓库。本库新增：金样与 1 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
