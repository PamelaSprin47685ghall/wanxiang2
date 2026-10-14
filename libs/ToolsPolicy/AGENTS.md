# ToolsPolicy（库）

旧 `Views/SettingsTools.fs` 的 `policyLabel` 与 `toolsPayload` 迁入，原样照抄（原本就是纯函数）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **文案与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/toolspolicy.txt`。

# 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
