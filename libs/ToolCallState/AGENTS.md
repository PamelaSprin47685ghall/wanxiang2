# ToolCallState（库）

旧 `Views/MessageCard.fs` 的 `resolveToolCallStatusWith` 迁入：`ToolCallState.resolve`。状态联合类型用例名是泛名，加了 RequireQualifiedAccess。从工具结果里认出错误由调用方用 ToolErrorSummary 做，结果以 `string option option` 传入。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，状态与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/toolcallstate.txt`（36 种组合）。

# 迁入或覆盖的旧测试
旧 MessageCard 的 `resolveStatus` 用例要构造 MessageContext，留在旧仓库。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
