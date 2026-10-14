# SidebarSelection（库）

旧 `Views/Sidebar.fs` 的多选状态迁入，成为一个对象：`Enter`、`Toggle`、`Exit`、`ToggleAllVisible`、`SelectedIds`、`IsSelected`，以及 `Chrome`（计数、全选与置顶文案、启用、条可见）。可见行和「是否全部置顶」由调用方传入。行视觉、按钮控件仍归 Sidebar。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串操作，选择与呈现和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/sidebarselection.txt`（19 步脚本）。
2. 清空最后一项就退出模式；全选会清掉不可见的已选项；置顶文案和批量键只看可见的已选项；计数文案用原始计数。

# 迁入或覆盖的旧测试
旧侧栏多选用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
