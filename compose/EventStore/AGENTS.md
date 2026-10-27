# EventStore（组合）

把 LedgerStore 和 ChatDomain 组合成旧 `Wanxiang.Store`（DataPaths、Store、RecoveryBundle），源码原样迁移，命名空间与磁盘格式不变。根规则见项目根目录 AGENTS.md。

## 接口
- `EventStore(dataDir, ?onCommitted, ?policy, ?maxStamps)`：日志存储的旧领域薄壳（只做类型映射）。成员：`DataDir`、`WireOnCommitted(f)`（接上提交成功后的广播回调；单槽回调注入，命名避开 `Set*`）、`Health`、`Startup`、`LastSnapshotError`、`IsReadOnly`、`Projection`、`LastCommitId`、`Stamps`、`MaxStamps`、`Checkpoint`、`Submit` / `SubmitEvents`、`CommitsAfter`、`SaveSnapshot`、`Repair`、`Shutdown`。
- `Inspect.run` / `Inspect.runOrPeek` / `Inspect.peek` / `Inspect.ofStore`、`Repair.repair` / `Repair.repairWith`。
- 命名规约：组合者零 `Set*` 业务写入成员（`grep "member .*\.Set"` 归零）；回调注入一律 `Wire*`。

# LEMMA
1. **源码与磁盘字节与旧实现一致。** [根 AXIOM: 5]
    - 推理：已有数据目录必须继续可读；原样迁移最容易对照。

# SPEC
1. [LEMMA: 1] 旧仓库的 EventLogTests（`three intact commits round trip through CommitCodec` 等）、StoreConsistencyTests（`单写者内部幂等：同 commandId 再次 Submit 返回原提交且不追加` 等）、ReplayTests（`doctor 只读模式不修改损坏日志` 等）原样迁入，测试通过。
2. [LEMMA: 1; Ledger LEMMA 6, 7] `EventStore` 透传可选 `maxStamps` 有界幂等保留上限，窗口内重发返回 `IdempotentReplay`、超出上限淘汰最旧幂等戳、淘汰后重发作为新提交落盘，保存快照后 `Inspect.run` 与 `Inspect.peek` 均保持 `Healthy`：`tests/ReplayTests.fs` 的 `有界 maxStamps 淘汰旧幂等戳后重发与 Inspect 均保持健康` 通过。

# 迁入或覆盖的旧测试
- EventLogTests.fs、StoreConsistencyTests.fs、ReplayTests.fs 整个文件（原样迁入 tests/）。

# 拆分与复核（待拆项完成）
- RecoveryBundle 的「复制 + SHA-256 核对」拆成库 VerifiedCopy；清单与诊断 JSON 的形状、写入顺序留在组合者，字节不变。
- Store 的 Inspect/Repair ↔ LedgerStore 对应关系复核：对应是一层薄翻译，唯一的问题是靠错误文字前缀
  （`why.StartsWith "written by a newer version"`）认出「更新版本写的日志」。已改为 LedgerStore 的类型化状态 `Health.NewerFormat`，
  Inspect 直接映射为 UnsupportedFormat（文字不变）。其余对应（Damaged → RepairRequired，OutcomeUnknown → OutcomeUnknown，
  Repair 只截 Damaged）保持不变。
