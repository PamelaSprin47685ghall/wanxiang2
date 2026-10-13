# Ledger

事件账本 actor。私有状态：领域状态、提交 id 序列、幂等索引、位置索引、在途提交、等待队列。只接收 `LedgerIn`、只发出 `LedgerOut`；不碰磁盘、不读时钟、不调用任何外部对象。不依赖任何其他库。AXIOM 与根 LEMMA 见项目根目录 AGENTS.md。

## 消息协议
- 构造：`Ledger<'S,'E,'Err>(apply : 'S -> Commit<'E> -> Result<'S,'Err>, initial, ?origin)` 或 `Ledger(apply, checkpoint)`。
- 收：
  - `Post(Submit(ticket, events, at, stamp))`：请求提交，`at` 由调用方给出。
  - `Ask(events, at, ?stamp)`：投递 Submit 并同步等回复（`Committed` 或 `Refused`）；在 Outbox 处理器里调用直接抛出。
  - `Post(Appended(id, endOffset))` / `Post(AppendFailed(id, reason))`：持久化方的回复，起点就是 `AppendRequested` 给出的 `startOffset`。
  - `Post(Replay(commit, span))`：重放一条读出的提交。
- 发（`Outbox`）：
  - `AppendRequested(ticket, commit, startOffset)`：同一时刻至多一条在途。
  - `Committed(ticket option, commit, span)`：重放的 ticket 为 None。
  - `Refused(ticket, Duplicate(id, span) | StampConflict id | Rejected 'Err | WriteFailed reason)`。
  - `ReplayRejected(commit, span, IdGap | SpanGap | RepeatedStamp | FoldRejected 'Err | Busy)`：损坏点。
- 查询（只读，立即回答）：`State`、`LastId`、`Checkpoint`、`SpanOf id`、`SpansAfter(cursor, ?limit)`。
- 值：`Span = { start; end }`、`Commit.map`、`Envelope.write : Commit<JsonNode> -> string`、`Envelope.read`。

组装方只做路由：`AppendRequested(t, c, start)` → 编码并交给存储对象写入 → 投回 `Appended(c.id, end)` 或 `AppendFailed`。重放：存储对象读出的记录 → `Envelope.read` → `Commit.map` → `Replay(c, span)`。最小示例见 tests/Toy.fs `Store`。

# LEMMA
1. [AXIOM: 1] 用户在乎的是「说过的话不会丢、不会乱、不会重复」：已确认的提交重启后仍在、顺序不变；同一条命令重发不产生第二条提交。
    - 推理：这是聊天记录这类数据的全部可靠性诉求；文件格式、分帧、快照都是手段。
2. [根 LEMMA: 6, 7; LEMMA: 1] 账本只拥有「下一条提交能否成立、成立后状态是什么」，泛型只覆盖 `'S`、`'E` 和折叠的错误类型 `'Err`。
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

9. [AXIOM: 9; LEMMA: 2] 拒绝原因携带折叠自己的错误类型 `'Err`。
    - 推理：拒绝的原因是领域知识；压成字符串会逼组装方在外面另存真正的错误，把账本已知的事实再记一遍。
10. [AXIOM: 1, 7; LEMMA: 7] 位置是「只追加介质上首尾相接的区间」，账本只记每条的末尾偏移，起点由前一条末尾导出。
    - 推理：追加日志里提交天然首尾相接，这是账本自己就能断言的事实，不是存储细节：区间不接续就说明介质有缺口或重叠，重放可以据此发现损坏（`SpanGap`）。于是每条提交只占 8 字节，不再保存调用方定义的任意位置。
11. [AXIOM: 10; LEMMA: 4] 在 Outbox 处理器里同步等待回复（`Ask`）必然永远等不到，直接抛出。
    - 推理：回复要等当前线程处理完手头这条消息才会产生；静默死锁是最难查的手滑，抛出能在开发期立刻暴露。

# SPEC
1. [LEMMA: 1, 3] id 从 1 起连续；重放得到相同 `Checkpoint`。证明：tests/Tests.fs `ids are contiguous and replay reproduces checkpoint`。
2. [LEMMA: 4] 同一时刻只有一条 `AppendRequested`；在途期间的请求等落定后才裁决。证明：`one append in flight ...`。
3. [LEMMA: 3] `AppendFailed` 给出 `Refused WriteFailed`，状态不变，id 复用。证明：`append failure refuses and keeps id`。
4. [LEMMA: 3, 9] 折叠被拒给出 `Refused(Rejected 领域错误)`，不发 `AppendRequested`。证明：`rejection carries the domain error ...`。
5. [LEMMA: 7] 同戳给出 `Duplicate(原 id, 原区间)`，hash 不同给出 `StampConflict`；重放后仍成立。证明：`same stamp is duplicate with span ...`。
6. [LEMMA: 6, 10] 重放拒绝 `IdGap`、`SpanGap`、`RepeatedStamp`、`FoldRejected 领域错误`、`Busy`，且状态不变。证明：`replay rejects every kind of inconsistency without change`。
7. [LEMMA: 5] 回复对不上、或末尾偏移不前进时抛出，之后仍可用。证明：`mismatched reply is a protocol error ...`。
8. [LEMMA: 11] 在 Outbox 处理器里 `Ask` 抛出 `InvalidOperationException`，不会死锁。证明：`Ask from inside an Outbox handler throws ...`。
9. [LEMMA: 7, 10] `Checkpoint` 每条提交只存一个末尾偏移；恢复等价；末尾数不符或不递增时构造抛出。证明：`checkpoint stores only ends ...`。
10. [LEMMA: 7, 10] `SpansAfter(cursor, limit)` 与 `SpanOf` 分页、越界正确，相邻区间首尾相接。证明：`spans after cursor honour limit and are contiguous`。
11. [LEMMA: 8] 外壳往返（含 `UInt64.MaxValue`）；`NewerVersion`、`Invalid` 区分清楚。证明：`envelope round-trips ...`。
12. [LEMMA: 4] 并发 `Ask` 时每个请求恰好提交一次，`Committed` 按 id 顺序发出。证明：`concurrent asks commit every submit exactly once in id order`。
13. [TODO] 幂等索引无界增长，需要保留策略。
14. [TODO] `Checkpoint` 仍随提交数线性增长（每条 8 字节）；若要有界，可只存每 K 条一个锚点，其余由组装方从介质扫描补齐。
15. [TODO] `Outbox` 处理器在排空邮箱的线程上同步执行；处理器若阻塞，会拖住投递方。
