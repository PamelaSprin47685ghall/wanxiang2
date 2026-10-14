# ConversationSurface（库）

旧 `AppShell.fs` 刷新时「已打开对话」分支（空对话卡、末尾错误卡来源、生成中文案、输入框可用、能否停止）与 UpdateHostStatusLine 的文案迁入。末尾错误来源 `TailError` 加了 RequireQualifiedAccess；ReplyTail.isUnanswered 由调用方算好传入。控件调用仍归 AppShell。没打开对话的分支在 IdleSurface。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组状态，决定与旧分支相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/conversationsurface.txt`（256 种组合）。
2. 运行错误优先于未回复；未回复只在不生成、不流式、没有半截回复时才显示。

# 迁入或覆盖的旧测试
旧对话区用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
