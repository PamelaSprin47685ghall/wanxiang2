# EventStore（组合）

把 LedgerStore 和 ChatDomain 组合成旧 `Wanxiang.Store`（DataPaths、Store、RecoveryBundle），源码原样迁移，命名空间与磁盘格式不变。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **源码与磁盘字节与旧实现一致。** [根 AXIOM: 5]
    - 推理：已有数据目录必须继续可读；原样迁移最容易对照。

# SPEC
1. [LEMMA: 1] 旧仓库的 EventLogTests、StoreConsistencyTests、ReplayTests 原样迁入，42 个测试通过。

# 迁入或覆盖的旧测试
- EventLogTests.fs、StoreConsistencyTests.fs、ReplayTests.fs 整个文件（原样迁入 tests/）。

# 拆分与复核（待拆项完成）
- RecoveryBundle 的「复制 + SHA-256 核对」拆成库 VerifiedCopy；清单与诊断 JSON 的形状、写入顺序留在组合者，字节不变。
- Store 的 Inspect/Repair ↔ LedgerStore 对应关系复核：对应是一层薄翻译，唯一的问题是靠错误文字前缀
  （`why.StartsWith "written by a newer version"`）认出「更新版本写的日志」。已改为 LedgerStore 的类型化状态 `Health.NewerFormat`，
  Inspect 直接映射为 UnsupportedFormat（文字不变）。其余对应（Damaged → RepairRequired，OutcomeUnknown → OutcomeUnknown，
  Repair 只截 Damaged）保持不变。
