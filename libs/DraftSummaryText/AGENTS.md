# DraftSummaryText（库）

旧 `AppShell.fs` 的 `UpdateDraftSummary` 里的文案分支迁入：`DraftSummaryText.text`。数草稿与未确认消息仍归 AppShell（用 DraftStore 快照）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组计数，文案与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/draftsummarytext.txt` 由照抄的旧分支生成。

# 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本库新增：金样、关闭保存优先。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

# 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
