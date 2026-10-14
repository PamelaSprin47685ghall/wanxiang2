# RevealFlash（库）

旧 `Views/ChatView.fs` RevealAndHighlight 与 EndRevealHighlight 的高亮记账迁入：`Revealed`、`Begin`（返回要先复原的上一条）、`End`、`LastFlashed`、`Highlighted`，停留时长 `DwellMs` 为 1600。计时器、找高亮承载面、背景刷仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串操作，记账与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/revealflash.txt`。

# 迁入或覆盖的旧测试
旧搜索命中高亮用例（第 211 节）要起窗口，留在旧仓库。本库新增：金样与 1 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
