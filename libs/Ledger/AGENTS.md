# Ledger

事件账本 actor。私有状态：领域状态、提交 id 序列、幂等索引、位置索引、在途提交、等待队列。只接收 `LedgerIn`、只发出 `LedgerOut`；不碰磁盘、不读时钟、不调用任何外部对象。不依赖任何其他库。AXIOM 与根 LEMMA 见项目根目录 AGENTS.md。

## 消息协议
- 构造：`Ledger<'S,'E,'P>(apply, initial)` 或 `Ledger(apply, checkpoint)`。
- 收（`Post`）：
  - `Submit(ticket, events, at, stamp)`：请求提交；`at` 由调用方给出。
  - `Appended(id, position)` / `AppendFailed(id, reason)`：持久化方对 `AppendRequested` 的回复。
  - `Replay(commit, position)`：重放一条读出的提交。
- 发（`Outbox`）：
  - `AppendRequested(ticket, commit)`：请写入；同一时刻至多一条在途。
  - `Committed(ticket option, commit, position)`：提交成立（重放的 ticket 为 None）。
  - `Refused(ticket, Duplicate(id, position) | StampConflict id | Rejected reason | WriteFailed reason)`。
  - `ReplayRejected(commit, position, reason)`：损坏点。
- 查询（只读，立即回答）：`State`、`LastId`、`Checkpoint`、`PositionOf id`、`PositionsAfter(cursor, ?limit)`。
- 值工具：`Commit.map`、`Envelope.write : Commit<JsonNode> -> string`、`Envelope.read : string -> Parsed | Invalid | NewerVersion`。

组装方只做路由：`AppendRequested` → 编码并交给存储对象写入 → 把结果作为 `Appended` / `AppendFailed` 投回。重放：存储对象读出的记录 → `Envelope.read` → `Commit.map` → `Replay`。最小示例见 tests/Toy.fs `Store`。

# LEMMA
1. [AXIOM: 1] 用户在乎的是「说过的话不会丢、不会乱、不会重复」：已确认的提交重启后仍在、顺序不变；同一条命令重发不产生第二条提交。
    - 推理：这是聊天记录这类数据的全部可靠性诉求；文件格式、分帧、快照都是手段。
2. [根 LEMMA: 6, 7; LEMMA: 1] 账本只拥有「下一条提交能否成立、成立后状态是什么」，泛型只覆盖 `'S`、`'E`、`'P`。
    - 推理：持久化、时钟、编码属于别的对象；账本对它们发消息（`AppendRequested`）、收回复（`Appended`），而不是调用注入进来的函数。`apply` 是事件溯源本身的语义（与 `List.fold` 的 folder 同类），不是对等方。
3. [AXIOM: 1, 7; LEMMA: 2] 状态只在收到 `Appended` 后改变。
    - 推理：于是写失败无需回滚，不存在「已落盘但无效」或「已记入但未落盘」的中间态。重放与现场提交落到同一个记入步骤。
4. [AXIOM: 1, 10; LEMMA: 3] 同一时刻至多一条提交在途；在途期间到达的请求排队，等在途提交落定后再按落定状态裁决。
    - 推理：串行化是单写者语义本身，应由账本这个 actor 保证，而不是要求组装方记住加锁（防呆）。裁决推迟到落定之后，幂等和折叠判断用的都是已持久化的事实。
5. [AXIOM: 10; LEMMA: 4] 回复与在途提交对不上是组装方的程序错误：抛出 `InvalidOperationException`，账本状态不变、仍可继续使用。
    - 推理：这类错误不会来自用户数据，只来自错误的路由；立刻抛出才能在开发期发现。
6. [AXIOM: 1; LEMMA: 3] 重放逐条校验 id 连续、commandId 不重复、折叠成功；不符就发 `ReplayRejected`，账本不变。账本有在途或等待请求时拒绝重放。
    - 推理：第一个 `ReplayRejected` 就是损坏点；截不截断是不可撤销的决定，交给组装方和用户。
7. [AXIOM: 7; LEMMA: 1] 幂等索引与位置索引由账本持有，随 `Checkpoint` 保存与恢复。
    - 推理：幂等就是 LEMMA 1 的「不会重复」；有了位置索引，组装方按位置向存储对象要记录，无需另建分页索引。
8. [AXIOM: 7] 外壳只承载 `Commit<JsonNode>`，版本从 1 起，不兼容任何旧格式；更高版本单独报告。
    - 推理：领域事件与 JSON 的互转是领域知识，由组装方配合 `Commit.map` 完成。旧格式没有使用方，不建兼容层（根 LEMMA 5）。

# SPEC
1. [LEMMA: 1, 3] id 从 1 起连续；重放得到相同 `Checkpoint`。证明：tests/Tests.fs `ids are contiguous and replay reproduces checkpoint`。
2. [LEMMA: 4] 同一时刻只有一条 `AppendRequested`；在途期间的请求等到落定后才裁决。证明：`one append in flight, later submits wait ...`。
3. [LEMMA: 3] `AppendFailed` 给出 `Refused WriteFailed`，状态不变，id 复用。证明：`append failure refuses and keeps id`。
4. [LEMMA: 3] 折叠被拒给出 `Refused Rejected`，不发 `AppendRequested`。证明：`rejected fold never requests an append`。
5. [LEMMA: 7] 同戳给出 `Duplicate(原 id, 原位置)`，hash 不同给出 `StampConflict`；重放后仍成立。证明：`same stamp is duplicate ...`。
6. [LEMMA: 6] 重放拒绝断档、重复 commandId、折叠失败、账本忙碌，且状态不变。证明：`replay rejects gaps ...`。
7. [LEMMA: 5] 回复对不上时抛出，之后仍可正常提交。证明：`mismatched reply is a protocol error ...`。
8. [LEMMA: 7] `Checkpoint` 恢复等价；位置数不符时构造抛出。证明：`checkpoint restores ...`。
9. [LEMMA: 7] `PositionsAfter(cursor, limit)` 与 `PositionOf` 正确分页与越界。证明：`positions after cursor honour limit`。
10. [LEMMA: 8] 外壳往返（含 `UInt64.MaxValue`）；`NewerVersion`、`Invalid` 区分清楚。证明：`envelope round-trips ...`。
11. [LEMMA: 4] 并发投递时每个请求恰好提交一次，`Committed` 按 id 顺序发出。证明：`concurrent posts commit every submit exactly once in id order`。
12. [TODO] 幂等索引无界增长，需要保留策略。
13. [TODO] `Checkpoint` 的位置列表随提交数线性增长；超大账本可改为分段索引。
14. [TODO] `Outbox` 处理器在投递线程上同步执行；处理器若阻塞，会拖住投递方。需要时可改为异步邮箱。
