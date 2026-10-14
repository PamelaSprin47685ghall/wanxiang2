# PairingHint（库）

旧 `Views/Dialogs.fs` 的 `ConnectDialogMode.pairingFirst`、`Dialogs.isLoopbackUrl`、`Dialogs.pairingCodeHint`（改名 `codeHint`）原样迁入：连接对话框是否直接进配对码模式、地址是否本机、配对码去哪里拿。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一输入，判断与提示与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/pairinghint.txt` 由旧函数在 10 个地址与 8 种首次连接组合上生成。

# 迁入或覆盖的旧测试
- 旧 DevicesUiTests：`pairing hint differs between this machine and a remote host`
- 旧 JourneyR9Tests：`260 首次连接（无令牌无凭据无错误）才直接走配对码`

本库新增：金样。
