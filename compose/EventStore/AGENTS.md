# EventStore（组合）

把 LedgerStore 和 ChatDomain 组合成旧 `Wanxiang.Store`（DataPaths、Store、RecoveryBundle），源码原样迁移，命名空间与磁盘格式不变。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **源码与磁盘字节与旧实现一致。** [根 AXIOM: 5]
    - 推理：已有数据目录必须继续可读；原样迁移最容易对照。

# SPEC
1. [LEMMA: 1] 旧仓库的 EventLogTests、StoreConsistencyTests、ReplayTests 原样迁入，42 个测试通过。

# 迁入或覆盖的旧测试
- EventLogTests.fs、StoreConsistencyTests.fs、ReplayTests.fs 整个文件（原样迁入 tests/）。
