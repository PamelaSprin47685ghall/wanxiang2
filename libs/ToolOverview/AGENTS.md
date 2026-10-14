# ToolOverview（库）

旧 `Views/MessageCard.fs` toolActivityBlock 的概览文案（`text`）、颜色档（`tone`，`OverviewTone`）与图标（`icon`，`OverviewIcon`）迁入，两个联合类型都加了 RequireQualifiedAccess。输入是各状态的计数，由调用方从 ToolCallState 统计。控件仍归 MessageCard。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组计数，结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/tooloverview.txt`（48 种组合）。
2. 等待批准优先于执行中；有待定的就绝不显示完成。

# 迁入或覆盖的旧测试
旧工具概览用例（第 206 节）要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
