# MessageMenu（库）

旧 `Views/MessageCard.fs` messageMenuEntries 的门控迁入：`entries` 给出菜单项（`MessageMenuItem` 加了 RequireQualifiedAccess），`label`、`hint`、`isDanger` 给文案。执行体、图标、ContextMenu 仍归 MessageCard。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组状态，菜单项、次序、文案与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/messagemenu.txt`（96 种组合）。
2. 生成中整个菜单为空；复制要有非空文本；编辑并分叉只给用户消息；重新生成只给最新回复；复制链接要有提交号和链接出口；删除只要有提交号。

# 迁入或覆盖的旧测试
旧右键菜单用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
