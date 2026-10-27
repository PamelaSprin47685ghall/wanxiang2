# PairingCodeInput（库）

旧 `Views/Dialogs.fs` 连接对话框配对码输入的归一（`normalize`：任意 Unicode 数字转为 ASCII，最多 6 位）、字距、提示文案，以及自动提交只触发一次（`PairingAutoSubmit`）迁入。输入框回写与光标仍归 Dialogs。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串输入，归一与是否自动提交和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`any unicode digits become six ascii digits`、`the same full code submits once until it changes`。
2. 同一串 6 位码只自动提交一次；不足 6 位时清零记录，之后再输入同一串码会再次提交（`the same full code submits once until it changes`）。

# 迁入或覆盖的旧测试
旧配对对话框用例要起窗口，留在旧仓库。本库新增 2 条规则用例。
