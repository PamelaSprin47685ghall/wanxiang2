# McpForm（库）

旧 `Views/SettingsTools.fs` 里 MCP 编辑器 saveCore 的校验与 `mcpPayload` 迁入：`validate` 返回值和按字段的错误（`McpField` 加了 RequireQualifiedAccess），`payload` 组写入载荷，`parseArgs` 切参数行。挂错误、提交与回复处理仍归 SettingsTools。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，错误次序、文案与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/mcpform.txt`（8400 种组合）。
2. 本地命令与远程端点必须且只能填一个，出错时两栏都标；超时为 1 到 3600 秒；已占用的标识只挡新增。

# 迁入或覆盖的旧测试
旧 SettingsTools 的用例要起窗口，留在旧仓库。本库新增：金样与 4 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
