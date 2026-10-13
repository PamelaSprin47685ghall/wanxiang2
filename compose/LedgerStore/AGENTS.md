# compose/LedgerStore

组合者 C：用 `JsonSeqFile`、`Ledger`、`SnapshotFile`、`DirLock` 组成一个目录上的持久事件存储。只经 `./x pack` 产出的交付包使用这四个库，不引用它们的源码（根 SPEC 2）。根规则见项目根目录 AGENTS.md。

它是真实组合的证据，也是旧仓库 `Store.fs` 要对齐的样例；与旧仓库 `Store.fs` 的双重实现登记在根《例外清单》。

公开面：`LedgerStore(dir, apply, initial)`；`Submit(events, at, ?stamp) : Submitted`（`Committed | Duplicate 原提交 | Conflict | Rejected | NotWritten | Unknown | ReadOnly`）；`Health`（`Healthy | Damaged(offset, reason) | OutcomeUnknown`）；`State`（副本）、`LastId`、`SaveSnapshot()`、`Repair()`、`Dispose()`。事件与状态都是 `JsonNode`，领域的类型化留给使用方。

# LEMMA
1. **C 拥有这段合作：顺序、关联、在途状态。** [根 LEMMA: 4]
    - 推理：顺序是「提议 → 追加 → 记入」；关联是用 `ledger.Tail` 与 `Appended end` 拼出区间交回账本；在途状态只有 `Health` 与并发锁。编号、幂等、折叠的判断全在 Ledger，C 不复制。
2. **三种写结果三种处置。** [根 SPEC 4; Ledger LEMMA 5; JsonSeq LEMMA 4]
    - 推理：`Appended` → `Record`；`NotWritten` → `Abandon`，告诉用户可重试；`Unknown` → 不记入、不放弃，存储转只读，用户看到「结果未知」，重新打开时从日志查明。未知不变成重试许可。
3. **打开时不改任何字节；发现损坏只转只读，截断要用户明确调用 `Repair`。** [根 AXIOM: 5, 6]
    - 推理：截断不可撤销，是用户的决定；只读能保住已有数据并让后果可见。
4. **快照只当缓存，对不上就丢。** [Snapshot LEMMA 1]
    - 推理：快照末条的区间超出日志，或该处读出的不是那条 id，就从头重放。
5. **边界上复制 `JsonNode`。** [根 LEMMA: 3]
    - 推理：`JsonNode` 可变。进来的事件、出去的状态都复制，调用方改了也碰不到内部。

# SPEC
（全部证明在 `tests/Tests.fs`，用真实文件、真实四个库，`./x compose` 在干净包缓存下运行，Linux 通过。）
1. [LEMMA: 1, 4] 提交重开后仍在，有无快照结果相同：`commits survive reopen with and without snapshot`。
2. [LEMMA: 1] 领域拒绝时日志一个字节也不写：`domain rejection writes nothing`。
3. [LEMMA: 1] 重复命令返回从日志读回的原提交，重开后仍然如此；hash 不同返回 `Conflict`：`duplicate returns the original commit read back from the log, also after reopen`。
4. [LEMMA: 3] 崩溃留下的半写尾：打开后 `Damaged`、只读、字节不变；`Repair` 截到损坏点后可继续提交：`torn tail after a crash makes the store read-only until the user repairs`。
5. [LEMMA: 4] 日志被截短后旧快照被忽略：`stale snapshot from a truncated log is ignored`。
6. [LEMMA: 2] 真实写失败且截不回（`/dev/full`）→ `Unknown`，随后只读，不重试：`unknown write outcome stays unknown and is never turned into a retry`。
7. [DirLock LEMMA 1] 同目录第二个存储被拒：`second store on the same directory is refused`。
8. [LEMMA: 5] 改输入、改返回的状态都不影响内部：`inputs and returned state are not aliased`。
9. [LEMMA: 1] 100 个并发提交各恰好一次：`concurrent submits each commit exactly once`。
10. [TODO] `NotWritten` 分支（写失败但截回成功）没有真实故障触发的测试。
11. [TODO] 从 `OutcomeUnknown` 重开后查明结果，只在 `/dev/full` 上测过只读；没有「部分写入后重开」的真实故障测试（半写尾由 SPEC 4 覆盖）。
