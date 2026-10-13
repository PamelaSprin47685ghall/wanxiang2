# SecretText

文本脱敏规则：已知凭据值精确替换、键值行兜底打码（从旧 SupportBundle 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`SecretText.replaceKnown secrets text`、`SecretText.maskAssignments prefixes text`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `凭据替换与键值兜底打码` 通过；端到端由 compose/Doctor 金样与 compose/ServerApp 迁入的 SupportBundleTests钉住。

# 迁入或覆盖的旧测试
- 见 compose/Doctor 金样与 compose/ServerApp 迁入的 SupportBundleTests。
