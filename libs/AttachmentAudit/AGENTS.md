# AttachmentAudit

诊断检查 missing-attachments / orphan-attachments（从旧 Doctor 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`AttachmentAudit.run refs blobNames : (ok * detail) * (ok * detail)`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `附件缺失与孤儿判定与旧实现一致` 通过；端到端由 compose/Doctor 金样与 compose/ServerApp 迁入的 DoctorTests、AttachmentTests钉住。

# 迁入或覆盖的旧测试
- 见 compose/Doctor 金样与 compose/ServerApp 迁入的 DoctorTests、AttachmentTests。
