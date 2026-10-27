# JsonSeq

合并包：JsonSeq、Ledger、Snapshot。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `JsonSeq`

## JsonSeq

RFC 7464 分帧（纯函数 `Frame`）与单文件追加日志对象 `JsonSeqFile`。`JsonSeqFile` 的私有状态是一个无缓冲的文件流。不依赖任何兄弟库，不理解 JSON，也不认识 Ledger。根规则见项目根目录 AGENTS.md。

公开面：
- `Frame.encode : string -> byte[]`、`Frame.split : int64 -> ReadOnlySpan<byte> -> Record list`；`Record = { payload; fault; startOffset; endOffset }`。
- `JsonSeqFile(path)`：`Append(text) : AppendOutcome`（`Appended endOffset | NotWritten reason | Unknown reason`）、`Read(from, until)`、`ReadFrom(offset)`、`Truncate(offset)`、`Length`、`Dispose()`。

## LEMMA
1. **记录边界只由 RS 决定，记录体以 LF 结尾才算完整。** [根 AXIOM: 1, 2]
    - 推理：崩溃后最常见的损坏是尾部半写。RS 作唯一边界，半写只影响最后一条，前面的记录能无歧义恢复。不用长度前缀：长度字段自己也可能半写，多一处要校验的东西。
2. **本库只搬运文本，不解析 JSON。** [根 AXIOM: 2, 3]
    - 推理：解析与规范化是使用方的事；契约只剩「文本进、文本出、带偏移」。
3. **读不懂的字节从不变成看似有效的数据；每段不可信字节都以 `fault` 交出。** [根 AXIOM: 1, 5; LEMMA: 1]
    - 推理：宽松解码把坏字节变成 U+FFFD，后续解析照样成功，损坏就被吞掉。忽略游离字节同理：一个不是日志的文件会被读成「空日志」，再追加就盖掉用户的东西。
4. **追加的结果只说有证据的话：写成、确知没写、未知。** [根 AXIOM: 5; 根 LEMMA: 3]
    - 推理：写失败后能截回原长度，才确知文件没变；截回也失败，文件里可能有这条的全部、部分或没有。把这种情况报成「没写」会让调用方重试，可能写出重复或半条（根 SPEC 4：未知不能自动转成再执行的许可）。文件流不带缓冲：失败后缓冲区里残留的字节会在下次 Flush 或 Dispose 时写进文件，状态就不再只归本对象管（根 LEMMA 3）。

## SPEC
1. [LEMMA: 1] `encode` 与 `split` 往返保持文本与偏移。
    - 证明：`tests/Tests.fs` `encode then split round-trips text and offsets`，通过。
2. [LEMMA: 1] 在任意字节处截断，切分结果是原记录的完整前缀，加至多一条 `TornTail` 尾记录。
    - 证明：`truncation at every byte yields complete prefix plus at most one incomplete tail`，逐字节穷举，通过。
3. [根 AXIOM: 6; LEMMA: 1] 文本含 RS 时 `encode` 抛出 `ArgumentException`（防呆：边界一旦破坏不可撤销）。
    - 证明：`encode rejects RS inside payload`，通过。
4. [LEMMA: 1] `Append` 返回前 `Flush(true)`；重开后可从任一返回的偏移续读；`Truncate` 回到给定偏移。
    - 证明：`file append survives reopen and truncate rolls back`，通过。「Flush(true) 真的到了磁盘」依赖操作系统，单元测试证明不了。
5. [LEMMA: 3] 非法 UTF-8 给出 `fault = Some InvalidUtf8`，不替换。
    - 证明：`invalid utf8 is reported not replaced`，通过。
6. [LEMMA: 3] 第一个 RS 之前的字节（含完全没有 RS 的文件）给出一条 `StrayBytes` 记录，覆盖这些字节。
    - 证明：`bytes before first RS are stray`，通过。
7. [LEMMA: 1] `Read(from, until)` 只读出区间内的记录。
    - 证明：`Read returns only records within range`，通过。
8. [LEMMA: 4] 写之前就失败给出 `NotWritten`；写失败但截回成功也给出 `NotWritten`；写失败且截不回时给出 `Unknown`。
    - 证明：`failure before writing is NotWritten`；`write failure without rollback evidence is Unknown`（真实设备 `/dev/full`：写 ENOSPC、截断 EINVAL；仅 Linux）；`write failure with rollback evidence is NotWritten`（真实故障：测试临时收紧本进程 `RLIMIT_FSIZE` 上限并忽略 SIGXFSZ，超过上限的写按 EFBIG 失败，而缩小文件的截回仍被允许；已写进上限内的部分字节由截回清掉；仅 Linux）。都通过。
    - 剩余边界：`RLIMIT_FSIZE` 在测试进程内是全局的，测试在 `finally` 里恢复原上限与原信号处置；期间只运行本用例。
9. [LEMMA: 1, 3] 没有正文的 RS 不被忽略：在末尾算 `TornTail`，在中间算 `EmptyRecord`；记录的偏移首尾相接，覆盖全部字节。
    - 证明：`bare RS is a faulted record, never skipped`，通过。缺陷由 compose 的逐字节崩溃测试发现。
10. [LEMMA: 1, 3] `StreamSplit.readAll` 以 `RS`（`0x1E`）哨兵分块流式读取，不一次把整段区间读入单个缓冲区；含内嵌换行符的跨块记录、前导 `StrayBytes`、中间 `EmptyRecord` 与末尾 `TornTail` 在任意切点下与 `Frame.split` 一次性读入逐字节等价。
    - 证明：`streaming keeps records whole across chunk boundaries`、`streaming read equals one-shot read for every cut`，通过。
    - 剩余边界：`Read` 仍把切分出的 `Record list` 组装返回，且单条跨块记录在遇到下一个 `RS` 前暂存在 `pending` 中。

11. [根 SPEC 3] 实例隔离；同一路径上的第二个实例读同一个文件（写者唯一归 `DirLock`，本库不假装独占）；非法参数与错误之后状态合法；`Read` 交出的记录不是内部状态的活视图。
    - 证明：`instances are isolated; errors and illegal arguments leave state legal`（其中同一路径第二个实例一段仅 Linux），通过。
    - 剩余边界：输入只有 `string` 与 `int64`，返回值是 `string`、`int64` 与不可变记录，没有可共享的可变别名；同路径多实例的写交错本库不裁决。

## 成员 `Ledger`

## Ledger

事件账本对象。拥有领域判断：下一条提交的编号、幂等、折叠，以及每条提交在介质上的区间。私有状态：领域状态、id 序列、幂等索引、区间索引、至多一条未落定的提议。普通同步对象：不碰磁盘、不读时钟、不知道谁来持久化，非线程安全。不依赖任何兄弟库。根规则见项目根目录 AGENTS.md。

公开面：
- 构造：`Ledger<'S,'E,'Err>(apply : 'S -> Commit<'E> -> Result<'S,'Err>, initial, ?origin, ?maxStamps)` 或 `Ledger(apply, checkpoint)`。
- 消息：
  - `Propose(events, at, ?stamp) : Decision`（`Proposed commit | Duplicate(id, span) | StampConflict id | Rejected 'Err`）。
  - `Record(commit, span) : Result<unit, RecordProblem>`：事实「这条提交已持久化在 span」。刚才的提议和重放读出的提交走同一个入口。问题：`IdGap | SpanGap | RepeatedStamp | FoldRejected 'Err | Unsettled id`，各有 `Describe()`。
  - `Abandon()`：使用方确知提议没有写入。
- 查询：`State`、`LastId`、`Stamps`（不可变 Map）、`MaxStamps`、`Tail`（下一条的起点）、`Checkpoint`（数组为副本）、`SpanOf id`、`SpansAfter(cursor, ?limit)`。
- 值与编解码：`Commit`、`Stamp`、`Span`、`Commit.map`、`Checkpoint.map`、`Envelope.write/read`（`Commit<JsonNode>`）、`CheckpointJson.write/read`（`Checkpoint<JsonNode>`）。

合作方式：由组合者 C 决定何时写、写到哪、失败怎么办（见 `compose/LedgerStore`）。本库的返回值只说自己的裁决和事实，不含「请某个对象做什么」。

## LEMMA
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

## SPEC
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
14. [TODO] 幂等索引有界保留已在 `Ledger` 与 `CheckpointJson` 提供可选 `maxStamps`（`stamps index evicts the oldest entries beyond the retention bound`、`stamp retention bound survives checkpoint round-trip`、`checkpoint json round-trips with caller-encoded state` 通过）；但单纯淘汰旧戳会使淘汰后重发的同 `commandId` 再次写入日志，与无界重放/`Inspect` 的 `RepeatedStamp` 损坏判据冲突，故尚未接入 `compose/LedgerStore`。
15. [TODO] `Checkpoint` 随提交数线性增长（每条 8 字节）。

## 成员 `Snapshot`

## Snapshot

原子快照文件对象：保存一段文本，带版本与完整性校验。私有状态是它管的那个路径。不依赖任何兄弟库，不认识 Ledger。根规则见项目根目录 AGENTS.md。

公开面：`SnapshotFile(path)`：`Save(text)`、`Load() : Loaded`（`Missing | Loaded of string | Corrupt of reason`）。

## LEMMA
1. **快照是可重建的缓存，不是事实来源。** [根 AXIOM: 1, 2]
    - 推理：事实在日志里，快照只省时间。所以快照坏了就忽略并重建，不需要修复、迁移或兼容路径。
2. **读者只会看到完整的旧快照或完整的新快照。** [根 AXIOM: 1; LEMMA: 1]
    - 推理：半写的快照被当真，状态就与日志分叉且无迹可寻。临时文件加同目录改名把半写排除在可见状态之外。
3. **文件带版本与正文哈希；任何不符都判为 `Corrupt`。** [LEMMA: 1, 2]
    - 推理：改名只防半写，防不了位翻转或人为改动；有了校验，「不可信」能被发现，LEMMA 1 让发现之后的处理很简单。
4. **只收发文本。** [根 AXIOM: 3; 根 LEMMA: 4]
    - 推理：本库拥有的是「这段文本被原子地、可校验地保存了」。值怎么变成文本是值的主人的事；在组合里由组合者把 `CheckpointJson.write` 的结果交给本库。

## SPEC
1. [LEMMA: 2] 保存后读出等于原值；再次保存覆盖旧值；不留 `.tmp`。
    - 证明：`tests/Tests.fs` `save then load round-trips and overwrites`，通过。
2. [LEMMA: 3] 文件任一字节翻转、任一位置截断，`Load` 返回 `Corrupt`。
    - 证明：`any flipped or truncated byte is Corrupt`，逐字节穷举，通过。
3. [LEMMA: 2] 崩溃遗留的 `.tmp` 不影响读取，下次保存覆盖它。
    - 证明：`leftover tmp file does not affect existing snapshot`，通过。
4. [LEMMA: 3] 头与正文都严格按 UTF-8 解码；头里有非法字节判 `Corrupt`，原因指明是头。
    - 证明：`invalid utf8 in header is Corrupt`，通过。
5. [LEMMA: 1] 真实组合中，删掉快照或快照与日志对不上时，组合者从日志重建，结果相同。
    - 证明：`compose/LedgerStore/tests/Tests.fs` 中的 `commits survive reopen with and without snapshot, startup says which`、`stale snapshot from a truncated log is discarded`；测试名称与当前源码核对一致，本次未重新运行。
6. [TODO] 目录 fsync 试验实现尚未验证平台标志、系统调用结果及失败语义；不能宣称断电持久性已解决。Windows 行为也未验证。

7. [根 SPEC 3] 实例隔离；文件被外部弄坏后 `Load` 如实报 `Corrupt`，下一次 `Save` 覆盖它；`Load` 交出的文本是副本，不是内部状态的活视图。
    - 证明：`instances are isolated and a corrupt file is replaced by the next save`，通过。
    - 剩余边界：私有状态就是它管的那个路径；两个实例指向同一路径时共用那个文件，这是调用方的选择，不是别名。
