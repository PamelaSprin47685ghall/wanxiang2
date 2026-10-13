# Ledger

事件账本对象：拥有领域状态、提交 id 序列、幂等索引、提交位置索引。不碰磁盘、不读时钟、不调用任何外部对象。不依赖任何其他库。AXIOM 见项目根目录 AGENTS.md。

公开接口：
- `Ledger<'S,'E,'P>(apply, initial)` / `Ledger(apply, checkpoint)`
- `Decide(events, at, ?stamp) : Decision<'E,'P>` = `Accept commit | Duplicate(id, position) | StampConflict id | Rejected reason`
- `Record(commit, position) : Result<unit,string>`
- `State`、`LastId`、`Checkpoint`、`PositionOf id`、`PositionsAfter(cursor, ?limit)`、事件 `Committed : (Commit<'E> * 'P)`
- `Commit.map`、`Envelope.write : Commit<JsonNode> -> string`、`Envelope.read : string -> Parsed | Invalid | NewerVersion`

消费方的组装（几行，见 tests/Toy.fs `Store`）：`Decide` → 若 `Accept c` 则 `Envelope.write (Commit.map toJson c)` 写入存储得到位置 `p` → `Record(c, p)`。重放：逐条读出 → `Envelope.read` → `Commit.map ofJson` → `Record(c, p)`，第一个 Error 即损坏点。

# LEMMA
1. [AXIOM: 1] 用户在乎的是「说过的话不会丢、不会乱、不会重复」：已确认的提交重启后仍在、顺序不变；同一条命令重发不产生第二条提交。
    - 推理：这是聊天记录这类数据的全部可靠性诉求；文件格式、分帧、快照都是手段。
2. [AXIOM: 4, 7, 8; LEMMA: 1] 库与库之间只交换值，不互相调用；泛型只用在本库真正不关心的地方。
    - 推理：把存储、时钟、编解码做成注入的函数记录（port），表面上解耦，实际是把另一个对象的行为塞进本对象内部调用，本库的语义要靠被注入者才完整，单独测试证明的只是「调用了端口」。诚实的边界是：每个对象拥有自己的状态与行为，只接收值、产出值，组装方在对象之间传递这些值。本库关心的只有「下一条提交能否成立、成立后状态是什么」，所以只对领域状态 `'S`、事件 `'E` 和「提交存在哪里」`'P` 泛型；折叠 `apply` 是事件溯源本身的语义（与 `List.fold` 的 folder 同类），不是端口。
3. [AXIOM: 1, 7; LEMMA: 2] 裁决与记入分开：`Decide` 不改账本；`Record` 只接收已经持久化的事实。
    - 推理：账本只在事实落定后才改变，所以写盘失败时账本无需回滚，不存在「已落盘但无效」或「已记入但未落盘」的中间态。现场提交与重放都是「把事实交给账本」，走同一个 `Record`，一条路径一份证明。
4. [AXIOM: 1, 10; LEMMA: 3] `Record` 自己校验 id 连续、commandId 不重复、折叠成功，不符则返回 Error 且账本不变。
    - 推理：这是防呆：组装方并发调用或重放到坏数据时，账本不会把矛盾的事实吞进去。重放时第一个 Error 就是损坏点，由组装方决定截断还是停下（不可撤销，交给用户）。
5. [AXIOM: 7; LEMMA: 1] 幂等索引与位置索引由账本持有，随 `Checkpoint` 一起保存与恢复。
    - 推理：幂等是 LEMMA 1「不会重复」本身；位置索引让组装方无需另建分页索引，只按位置读存储。
6. [AXIOM: 7] 外壳只承载 JSON 形式的事件，版本从 1 起，不兼容任何旧格式；更高版本单独报告。
    - 推理：领域事件与 JSON 的互转是领域知识，配合 `Commit.map` 由组装方完成，外壳不需要知道领域。旧格式没有使用方，不建兼容层（根 LEMMA 5）。单独报告更高版本，避免旧程序把新数据当损坏截掉。
7. [AXIOM: 1; LEMMA: 3] 一次只能有一个 `Decide → 持久化 → Record` 在进行中（单写者）。
    - 推理：两个并发的 `Decide` 会给出同一个 id，第二个持久化后 `Record` 必然失败，日志里就多一条无主记录。账本不拥有持久化，所以无法替组装方串行化；组装方用一个 actor（如 MailboxProcessor）包住这三步。LEMMA 4 保证违反时账本状态仍然正确。

# SPEC
1. [LEMMA: 1, 3] 提交 id 从 1 起连续；按日志重放得到相同的 `Checkpoint`。
    - 证明：tests/Tests.fs `ids are contiguous and replay reproduces state`。
2. [LEMMA: 3] `Decide` 重复调用结果相同且不改变账本；折叠失败给 `Rejected`。
    - 证明：`decide does not change the ledger and rejection is reported`。
3. [LEMMA: 4] `Record` 拒绝 id 断档、重复 commandId、折叠失败，且 `Checkpoint` 不变。
    - 证明：`record rejects gaps, repeated stamps and failing folds without change`。
4. [LEMMA: 5] 同 commandId 同 hash 给 `Duplicate(原 id, 原位置)`；hash 不同给 `StampConflict`；重放后仍成立。
    - 证明：`same stamp is duplicate with position, different hash conflicts`。
5. [LEMMA: 5] `Ledger(apply, checkpoint)` 与原账本等价；位置数与 throughId 不符时构造抛出。
    - 证明：`checkpoint restores an equivalent ledger and rejects inconsistent positions`。
6. [LEMMA: 5] `PositionsAfter(cursor, limit)` 返回 id 大于 cursor 的至多 limit 个位置；`PositionOf` 越界给 None。
    - 证明：`positions after cursor honour limit`。
7. [LEMMA: 6] 外壳往返保持提交（含 `UInt64.MaxValue` id）；`v` 更高给 `NewerVersion`；其余坏输入给 `Invalid`。
    - 证明：`envelope round-trips, newer version and garbage are distinguished`。
8. [LEMMA: 1] `Committed` 按 id 顺序触发并带位置。
    - 证明：`committed fires in id order with position`。
9. [TODO] 幂等索引无界增长，需要保留策略（例如按时间窗淘汰）。
10. [TODO] 位置索引每条提交占一个 `'P`，`Checkpoint` 随提交数线性增长；超大账本可改为分段索引。
