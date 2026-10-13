# VerifiedCopy

复制文件并核对 SHA-256（从旧 RecoveryBundle 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`VerifiedCopy.sha256OfFile`、`VerifiedCopy.copyInto targetDir source : sha256 * size`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `复制并核对哈希，不覆盖已有副本` 通过；端到端由 compose/EventStore 的修复测试与 compose/ServerApp 迁入的 DoctorTests钉住。

# 迁入或覆盖的旧测试
- 见 compose/EventStore 的修复测试与 compose/ServerApp 迁入的 DoctorTests。
