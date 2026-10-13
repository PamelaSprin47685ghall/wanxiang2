# Ledger

泛型单写者事件账本：状态 `'S`、事件 `'E`、存储 `Log` 全部由调用方提供。不依赖任何其他库。AXIOM 见项目根目录 AGENTS.md。

公开接口：`Ledger.openLog / load / repair`，实例 `Submit / ReadAfter / State / Checkpoint / LastId / Committed`；类型 `Commit<'E> Codec<'E> Log LogRecord Stamp Checkpoint<'S> Damage<'S> SubmitResult<'E> Opened<'S,'E>`。

# LEMMA
1. [AXIOM: 1] 用户在乎的是「说过的话不会丢、不会乱、不会重复」：已确认的提交在重启后仍在，顺序不变；同一条命令重发不会产生第二条记录。
    - 推理：这是聊天记录这类数据的全部可靠性诉求，其余（文件格式、轮转、快照）都是手段。
2. [AXIOM: 7, 8; LEMMA: 1] 账本只依赖三样能力：事件编解码 `Codec<'E>`、折叠 `apply`、存储 `Log`。全部以参数传入，不引用其他库。
    - 推理：LEMMA 1 不涉及领域与分帧；以函数记录传入，本库可以用玩具领域与内存日志单独证明，存储换成任何追加介质都不改契约。
3. [AXIOM: 1, 7; LEMMA: 1] 先折叠、后写盘。折叠被拒就不写。
    - 推理：反过来做（先写后折叠，失败再截尾复用 id）会出现「已落盘但无效」的中间态，必须有截尾和复用 id 的额外路径与额外证明。先折叠则这条路径不存在。代价：`apply` 必须是纯函数且与重放时一致，这本来就是事件溯源的前提。
4. [AXIOM: 1, 10; LEMMA: 1] 打开时发现损坏，只报告不修改；修复是调用方显式调用的不可撤销操作。
    - 推理：自动截尾会悄悄丢掉损坏点之后可能仍有效的记录，这是不可撤销的；按 AXIOM 10，只有不可撤销的操作才设防呆，这里的防呆就是「报告 + 显式修复」。
5. [AXIOM: 7; LEMMA: 1] 幂等索引由账本自己持有，随提交与基线一起走。
    - 推理：幂等是 LEMMA 1「不会重复」本身，交给领域状态就要求每个领域都实现一遍。
6. [AXIOM: 7] 不兼容任何旧格式，外壳版本号从 1 起；更高版本拒绝打开。
    - 推理：兼容层要有使用方和解除条件（根 LEMMA 5），当前没有旧数据需要读；拒绝打开新版本，避免旧程序把新数据误判为损坏后修掉。

# SPEC
1. [LEMMA: 1] 提交 id 从 1 起连续；重开后状态与 LastId 不变。
    - 证明：tests/Tests.fs `ids are contiguous from 1 and survive reopen`。
2. [LEMMA: 3] `apply` 返回 Error 时 `Submit` 返回 `Rejected`，日志无新增，下一个 id 不变。
    - 证明：`rejected fold writes nothing`。
3. [LEMMA: 1, 3] `log.append` 抛出时截断回写前偏移，返回 `WriteFailed`，状态与下一个 id 不变。
    - 证明：`write failure rolls back and keeps id`（内存日志注入半写后抛出）。
4. [LEMMA: 5] 同 commandId 同 hash 返回 `Duplicate 原id`；同 commandId 不同 hash 返回 `StampConflict`；重开后仍成立。
    - 证明：`same stamp is duplicate ...`。
5. [LEMMA: 4] 截断的尾记录、无法解码、id 不连续、重放折叠被拒，都返回 `Damaged`，带有效前缀与其后记录数，日志字节不变；`Ledger.repair` 截到有效前缀后可正常打开。
    - 证明：`torn tail is reported ...`、`corrupt middle record ...`（三种损坏）。
6. [LEMMA: 6] 外壳 `v` 大于 1 返回 `Unsupported`。
    - 证明：`future version is unsupported`。
7. [LEMMA: 1, 5] 从 `Checkpoint` 续播与完整重放得到相同基线（含幂等索引）；`ReadAfter` 在基线前后都返回正确区间。
    - 证明：`resume from checkpoint equals full replay ...`。
8. [LEMMA: 1] 并发 `Submit` 得到不重不漏的连续 id；`Committed` 事件按 id 顺序触发。
    - 证明：`concurrent submits get unique contiguous ids in event order`。
9. [TODO] 幂等索引无界增长，需要保留策略（例如按时间窗淘汰）。
10. [TODO] `ReadAfter` 读到基线之前的提交时从头扫描；如需高频调用，要建全量偏移索引。
11. [TODO] `Ledger.repair` 会丢弃损坏点之后的记录；把这些记录导出备份尚未实现（交给调用方或单独的库）。
