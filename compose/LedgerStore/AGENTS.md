# compose/LedgerStore

组合者 C：用 `JsonSeqFile`、`Ledger`、`SnapshotFile`、`DirLock` 组成一个目录上的持久事件存储。只经 `./x pack` 产出的交付包使用这四个库，不引用它们的源码（根 SPEC 2）；本目录自己也打成包 `Wanxiang2.LedgerStore`，供旧仓库消费。根规则见项目根目录 AGENTS.md。

公开面：
- 构造：`new LedgerStore<'S,'E,'Err>(dir, apply : 'S -> Commit<'E> -> Result<'S,'Err>, initial, codec, ?policy)`。
  - `Codec<'S,'E> = { encodeEvent; decodeEvent; encodeState; decodeState }`：事件编解码用于每条提交，状态编解码只用于快照。
  - `SnapshotPolicy = { everyCommits : int option; onClose : bool }`，有 `Manual` 与 `Default`（每 1000 条加关闭时）。
- 写：`Submit(events, at, ?stamp) : Submitted<'E,'Err>`，结果是 `Committed c | Duplicate 原提交 | Conflict id | Rejected 'Err | NotWritten | Unknown | ReadOnly` 之一。
- 读：`CommitsAfter(cursor, limit) : Result<Commit<'E> list, string>`，`State : 'S`，`LastId`，`Stamps : Map<commandId, hash * id>`，`Checkpoint : Checkpoint<'S>`。
- 诊断：`Startup = { snapshot = Used throughId | Absent | Discarded reason; replayedCommits }`，`Health = Healthy | Damaged(offset, reason) | OutcomeUnknown reason`，`LastSnapshotError`。
- 维护：`SaveSnapshot() : Result<unit,string>`，`Repair() : Result<int64,string>`，`Dispose()`。

# LEMMA
1. **C 拥有这段合作：顺序、关联、在途状态。** [根 LEMMA: 4]
    - 推理：顺序是「提议 → 追加 → 记入 → 按策略存快照」；关联是用 `ledger.Tail` 与 `Appended end` 拼出区间交回账本；在途状态只有 `Health`、上次快照位置和并发锁。编号、幂等、折叠的判断都在 Ledger，C 不复制。
2. **三种写结果三种处置。** [根 SPEC 4; Ledger LEMMA 5; JsonSeq LEMMA 4]
    - 推理：`Appended` → `Record`；`NotWritten` → `Abandon`，告诉用户可以重试；`Unknown` → 既不记入也不放弃，存储转只读，用户看到「结果未知」，重新打开时从日志查明。未知不能变成重试许可。
3. **打开时不改任何字节；发现损坏只转只读，截断要用户明确调用 `Repair`。** [根 AXIOM: 5, 6]
    - 推理：截断不可撤销，是用户的决定；只读能保住已有数据，并让后果看得见。
4. **快照只当缓存：对不上就丢，存失败不影响提交；何时存由策略定。** [Snapshot LEMMA 1]
    - 推理：快照末条的区间超出日志，或该处读出的不是那条 id，就从头重放，`Startup` 如实说明。策略只影响启动快慢，所以失败只记在 `LastSnapshotError` 里。
5. **类型化的事件、状态、错误；不复制。** [根 AXIOM: 2; 根 LEMMA: 3, 8]
    - 推理：旧系统需要领域类型。每条提交只编一次事件，不把整个状态往返 JSON。状态归属靠不可变值：`'S`、`'E` 应是不可变的 F# 值，`State` 和返回的提交直接交出。这是对使用方的约束，C 无法替它检查。
6. **游标分页只读已记入的提交。** [Ledger LEMMA 7]
    - 推理：区间来自账本，C 一次读出这一段字节，逐条核对 id。损坏点之后的内容不会被当成提交读出来。
7. **故障证据用真实故障，不建注入框架。** [根 SPEC 4; 根 LEMMA 7]
    - 推理：旧系统的 FaultInjector 是全局可变开关，要在生产代码里埋故障点。新结构里，崩溃对磁盘的影响只有一种：追加在某个字节处中断。所以测试直接写出每一种截断后的文件；写失败用 `/dev/full`；快照失败用「目标是目录」。

# SPEC
（证明都在 `tests/Tests.fs`，用真实文件和真实的四个库；`./x compose` 在干净包缓存下运行，Linux 通过。）
1. [LEMMA: 1, 4] 提交重开后仍在；用快照、坏快照、无快照三种启动结果相同，`Startup` 如实报告：`commits survive reopen with and without snapshot, startup says which`。
2. [LEMMA: 5] 领域拒绝带类型化错误，日志一个字节也不写：`domain rejection carries the typed error and writes nothing`。
3. [LEMMA: 1] 重复命令返回从日志读回的原提交，重开后仍然如此；hash 不同返回 `Conflict`；`Stamps`、`Checkpoint` 可读：`duplicate returns the original commit read back from the log; stamps are visible`。
4. [LEMMA: 6] 150 条按每页 64 条分页读完，内容与提交时一致：`commits after cursor page through the log`。
5. [LEMMA: 3, 7] 在新记录的每一个字节处崩溃：历史字节不变；id 连续不重复；截断为 0 时健康，完整时新提交在，其余情况 `Damaged`（带偏移和原因），修复后从旧位置继续：`crash at every byte of an append leaves the longest valid prefix`。这个测试曾抓到一个真 bug：末尾单个 RS 被忽略，偏移对不上（已在 JsonSeq 修复）。
6. [LEMMA: 3] 半写尾：只读，打开不改字节：`torn tail makes the store read-only and opening changes no bytes`。
7. [LEMMA: 3] 事件解码失败算损坏，不抛异常：`undecodable event is damage, not a crash`。
8. [LEMMA: 4] 日志被截短后旧快照被丢弃：`stale snapshot from a truncated log is discarded`。
9. [LEMMA: 4] 每 k 条保存、关闭时保存：`snapshot policy saves every k commits and on close`。
10. [LEMMA: 4, 7] 快照保存真实失败，不影响提交：`snapshot save failure does not affect commits`。
11. [LEMMA: 2, 7] 真实写失败且截不回（`/dev/full`）→ `Unknown`，随后只读：`unknown write outcome stays unknown and is never turned into a retry`。
12. [DirLock LEMMA 1] 同目录第二个存储被拒：`second store on the same directory is refused`。
13. [LEMMA: 1] 100 个并发提交各恰好一次：`concurrent submits each commit exactly once`。
14. [根 SPEC 2] 交付包可以在仓库外消费：在 /tmp 下新建项目，只引用 `Wanxiang2.LedgerStore`，提交后重开得到 `Used 1UL`。2026-10-13 手动跑过；[TODO] 没有自动化。
15. [TODO] `NotWritten` 分支（写失败但截回成功）没有真实故障触发。
16. [TODO] 进程被真正杀掉（而不是由测试写出截断文件）的测试没有；按 LEMMA 7，二者对磁盘的效果相同。

## 旧仓库测试的去留（供接入时参考）
旧 `CrashMatrixTests` 的故障点对应如下：
- `BeforeOpen`、`BeforeAppend`、`DuringAppend`、`AfterAppendBeforeFlush`：由 SPEC 5（截断长度为 0 到部分写）和 SPEC 11 覆盖。
- `AfterFlushBeforeProjection`：新结构先折叠再写，这个状态不存在（Ledger LEMMA 3）。
- `AfterProjectionBeforePublish`：由 SPEC 5 中截断长度等于完整记录的情形覆盖。
- `DuringTruncate`：运行时不再截断。
- `NoFault`、`FailOnce`：测的是注入器本身。

所以这些测试都变得多余，可以连同 `FaultInjector` 一起删掉。
