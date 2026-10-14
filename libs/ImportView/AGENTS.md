# ImportView（库）

旧 `Views/ImportDialog.fs` 的按键门控（`ImportView.chrome`）、状态文案，以及「只认最新一次提交的回执」（`ImportAttempt`，旧 busy 加 currentAttempt；尝试号从 Guid 改成计数）迁入。预览解析、文件读取仍归 ImportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串操作，门控与回执取舍和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/importview.txt`。

# 迁入或覆盖的旧测试
旧导入用例要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
