# PairingCodeInput（库）

旧 `Views/Dialogs.fs` 连接对话框配对码输入的归一（`normalize`：任意 Unicode 数字转为 ASCII，最多 6 位）、字距、提示文案，以及自动提交只触发一次（`PairingAutoSubmit`）迁入。输入框回写与光标仍归 Dialogs。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串输入，归一与是否自动提交和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/pairingcodeinput.txt`。
2. 同一串 6 位码只自动提交一次；不足 6 位时清零记录，之后再输入同一串码会再次提交。

# 迁入或覆盖的旧测试
旧配对对话框用例要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
