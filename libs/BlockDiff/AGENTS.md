# BlockDiff（库）

旧 `Controls/MarkdownRenderer.fs` StreamingMarkdown（720-756，决策 292）的增量记账迁入为泛型 `BlockDiff<'B, 'C>`：`Accept(raw)` 判原文是否变了，`Diff(blocks)` 返回 `BlockPlan`（保留的控件数、要恢复外边距的原末块控件、要重新渲染的块），`Mounted(block, controls)` 登记，`BlockCount`。块相等用结构相等（与 `Markdown.commonPrefix` 相同）。解析、渲染、面板增删、末块底边距清零仍归宿主。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **流式增量只重建第一个变了的块之后的控件。** [根 AXIOM: 5] 长回复每批重绘不重新排字。

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/blockdiff.txt`（含宿主替身的面板与边距）；`tests/Tests.fs` 两条行为用例。

# 迁入或覆盖的旧测试
旧仓库没有直接测 StreamingMarkdown 记账的用例；流式渲染的窗口用例留在旧仓库。本库新增金样与行为用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldBlockDiff.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。
