# Ledger

事件账本对象。拥有领域判断：下一条提交的编号、幂等、折叠，以及每条提交在介质上的区间。私有状态：领域状态、id 序列、幂等索引、区间索引、至多一条未落定的提议。普通同步对象：不碰磁盘、不读时钟、不知道谁来持久化，非线程安全。不依赖任何兄弟库。根规则见项目根目录 AGENTS.md。

公开面：
- 构造：`Ledger<'S,'E,'Err>(apply : 'S -> Commit<'E> -> Result<'S,'Err>, initial, ?origin)` 或 `Ledger(apply, checkpoint)`。
- 消息：
  - `Propose(events, at, ?stamp) : Decision`（`Proposed commit | Duplicate(id, span) | StampConflict id | Rejected 'Err`）。
  - `Record(commit, span) : Result<unit, RecordProblem>`：事实「这条提交已持久化在 span」。刚才的提议和重放读出的提交走同一个入口。问题：`IdGap | SpanGap | RepeatedStamp | FoldRejected 'Err | Unsettled id`，各有 `Describe()`。
  - `Abandon()`：使用方确知提议没有写入。
- 查询：`State`、`LastId`、`Stamps`（不可变 Map）、`Tail`（下一条的起点）、`Checkpoint`（数组为副本）、`SpanOf id`、`SpansAfter(cursor, ?limit)`。
- 值与编解码：`Commit`、`Stamp`、`Span`、`Commit.map`、`Checkpoint.map`、`Envelope.write/read`（`Commit<JsonNode>`）、`CheckpointJson.write/read`（`Checkpoint<JsonNode>`）。

合作方式：由组合者 C 决定何时写、写到哪、失败怎么办（见 `compose/LedgerStore`）。本库的返回值只说自己的裁决和事实，不含「请某个对象做什么」。

# LEMMA
1. **用户在乎的是说过的话不丢、不乱、不重复。** [根 AXIOM: 1]
    - 推理：已确认的提交重启后仍在、顺序不变；同一命令重发不产生第二条提交。文件格式、分帧、快照都是手段。
2. **本库拥有裁决和记账，不拥有合作。** [根 LEMMA: 3, 4; LEMMA: 1]
    - 推理：编号、幂等、折叠是账本的领域判断；何时写、写给谁、写失败如何处置是组合者的事。旧版发出 `AppendRequested` 让组合者机械转发给文件，等于把对文件的调用脚本藏在返回值里（根 LEMMA 4 明令禁止），还为此造了邮箱、蹦床、ticket 和 Ask 重入防护，这些都是 Actor 运行时（根 LEMMA 3 说不必建）。现在 `Proposed c` 只是「下一条提交是 c」这一裁决，`Record` 收的是「c 已持久化在 span」这一事实。
3. **状态只在 `Record` 成功时改变。** [根 AXIOM: 1, 2; LEMMA: 2]
    - 推理：于是写失败不需要回滚，没有「已记入但未落盘」的中间态。
4. **同一时刻至多一条未落定的提议；违反时抛出，账本不变。** [根 AXIOM: 6; 根 LEMMA: 3]
    - 推理：两条提议拿到同一个 id，后写的那条必然与日志冲突，这是组合者的程序错误，只能来自错误的接线，立刻抛出才能在开发期发现。记入别的提交而提议未落定，返回 `Unsettled`（这是数据问题，不抛）。并发串行化归组合者（它拥有合作的顺序），本库不加锁。
5. **结果未知时，本库不提供「当作失败」的出口。** [根 SPEC 4; LEMMA: 3]
    - 推理：`Abandon` 只给「确知没写」。不知道写没写时，组合者丢弃本实例，从介质重建；介质里有这条，重放会记入它，没有就没有。
6. **`Record` 逐条校验 id 连续、区间首尾相接、commandId 不重复、折叠成功；不符就返回问题，账本不变。** [根 AXIOM: 1, 5; LEMMA: 1]
    - 推理：第一个问题就是损坏点。截不截断是不可撤销的决定，交给组合者和用户。追加日志里提交天然首尾相接，区间不接续说明介质有缺口或重叠，这是账本自己能断言的事实。
7. **幂等索引与区间索引归账本，随 `Checkpoint` 保存；每条提交只存 8 字节末尾偏移，不存内容。** [根 AXIOM: 2; LEMMA: 1]
    - 推理：幂等就是「不重复」。存内容会让内存随历史无界增长。`Duplicate` 因此只回 id 与区间，原提交由组合者按区间从介质读回。
8. **拒绝原因带折叠自己的错误类型 `'Err`。** [根 AXIOM: 5]
    - 推理：压成字符串，组合者就得在外面另存真正的错误，把账本已知的事实再记一遍。
9. **泛型只覆盖领域：`'S`、`'E`、`'Err`；`apply` 是账本语义的一部分，不是注入的合作者。** [根 LEMMA: 4, 8]
    - 推理：「把一条提交并入状态」是事件溯源本身，与 `List.fold` 的 folder 同类；账本不通过它调度任何别的对象。泛型的当下收益是把领域隔在账本之外：本库测试用一个玩具计数器领域就能完整验证。
10. **外壳与 Checkpoint 的 JSON 形状归本库；领域部分由调用方编成 `JsonNode` 放进来。** [根 AXIOM: 2; 根 LEMMA: 8]
    - 推理：公共部分的形状是账本的事实，每个使用方各写一遍就是同一事实多处手写；领域形状是领域知识。传值，不传编码函数。版本从 1 起，不兼容旧格式（旧格式没有使用方）。

# SPEC
1. [LEMMA: 1, 3] id 从 1 起连续；把同样的事实按序记入新实例，得到相同 `Checkpoint`。
    - 证明：`tests/Tests.fs` `ids are contiguous and recording the same facts reproduces checkpoint`，通过。
2. [LEMMA: 3, 4] 提议在 `Record` 前不改状态；未落定时再 `Propose` 抛出且账本不变。
    - 证明：`a proposal changes nothing until recorded and blocks a second proposal`，通过。
3. [LEMMA: 3, 5] `Abandon` 后状态不变，id 复用。
    - 证明：`abandon keeps state and reuses the id`，通过。
4. [LEMMA: 8] 折叠被拒给出 `Rejected 领域错误`，不留提议。
    - 证明：`rejection carries the domain error and leaves no proposal`，通过。
5. [LEMMA: 7] 同戳给出 `Duplicate(原 id, 原区间)`，hash 不同给出 `StampConflict`；记入重放事实后仍成立，`Stamps` 与之一致。
    - 证明：`same stamp is duplicate with span, different hash conflicts, survives replay`，通过。
6. [LEMMA: 4, 6] `Record` 拒绝 `IdGap`、`SpanGap`（含空区间）、`RepeatedStamp`、`FoldRejected`、`Unsettled`，账本不变；被拒的提议之后仍能正确记入。
    - 证明：`record rejects every kind of inconsistency without change`，通过。
7. [根 SPEC 3] 构造输入与 `Checkpoint` 返回的数组不与内部共享；错误之后仍可用；实例之间隔离。
    - 证明：`checkpoint arrays are copies in and out; errors leave the ledger usable`、`instances are isolated`，通过。
    - 剩余边界：`State` 原样返回 `'S`；`'S` 若可变，别名由使用方负责（组合者在边界上复制，见 compose SPEC）。
8. [LEMMA: 7] `Checkpoint` 只存末尾偏移；恢复等价；末尾数不符或不递增时构造抛出。
    - 证明：`checkpoint stores only ends, restores equivalently, rejects inconsistency`，通过。
9. [LEMMA: 7] `SpansAfter(cursor, limit)` 与 `SpanOf` 分页、越界正确，起点为 origin，相邻区间首尾相接。
    - 证明：`spans after cursor honour limit and are contiguous`，通过。
10. [LEMMA: 10] 外壳往返（含 `UInt64.MaxValue`）；`NewerVersion` 与 `Invalid` 区分。
    - 证明：`envelope round-trips, newer version and garbage are distinguished`，通过。
11. [LEMMA: 6] 每种 `RecordProblem` 有不同的可读描述。
    - 证明：`record problems describe themselves`，通过。
12. [LEMMA: 10] `CheckpointJson` 往返保持 `Checkpoint`，恢复的账本等价；版本不符、偏移越界、坏输入报错。
    - 证明：`checkpoint json round-trips with caller-encoded state`，通过。
13. [LEMMA: 2, 5] 与真实文件组合时成功、重复、拒绝、半写尾、未知结果都按上面的规则落地。
    - 证明：`compose/LedgerStore/tests/Tests.fs`（见该目录 AGENTS.md）。
14. [TODO] 幂等索引无界增长，需要保留策略。
15. [TODO] `Checkpoint` 随提交数线性增长（每条 8 字节）。
