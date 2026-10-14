# StartReady（库）

旧 `AppShell.fs` 刷新界面时的 `startReady` 判断迁入：`StartReady.isReady`。当前对话状态用 `ActiveConversation`（泛名，加了 RequireQualifiedAccess）：没打开、加载中、已加载（附消息条数）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组状态，结论与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/startready.txt` 由照抄的旧判断在 80 种组合上生成。

# 迁入或覆盖的旧测试
旧 ConversationWorkspaceTests 的用例要起窗口，留在旧仓库。本库新增：金样、新对话或空对话才就绪。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

# 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
