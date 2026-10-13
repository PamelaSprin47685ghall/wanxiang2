# SnapshotCheck

诊断检查 snapshot 的文字（从旧 Doctor 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`SnapshotState`（RequireQualifiedAccess）、`SnapshotCheck.run state : ok * detail`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **这是独立的规则，不需要知道调用方的类型与协作。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `snapshot 检查文字与旧实现一致` 通过；端到端由 compose/Doctor 金样与 compose/ServerApp 迁入的 DoctorTests钉住。

# 迁入或覆盖的旧测试
以下旧测试已原文迁入 wanxiang2，端到端经过本库：
- `健康数据目录：全部检查通过`（compose/ServerApp/tests/DoctorTests.fs）
- `doctor reports and recovery bundle equal the old implementation`（compose/Doctor/tests/GoldenTests.fs，金样由旧代码生成）

本库自己的行为测试（边界与失败路径）见 tests/Tests.fs。
