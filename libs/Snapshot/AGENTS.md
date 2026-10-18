# Snapshot

原子快照文件对象：保存一段文本，带版本与完整性校验。私有状态是它管的那个路径。不依赖任何兄弟库，不认识 Ledger。根规则见项目根目录 AGENTS.md。

公开面：`SnapshotFile(path)`：`Save(text)`、`Load() : Loaded`（`Missing | Loaded of string | Corrupt of reason`）。

# LEMMA
1. **快照是可重建的缓存，不是事实来源。** [根 AXIOM: 1, 2]
    - 推理：事实在日志里，快照只省时间。所以快照坏了就忽略并重建，不需要修复、迁移或兼容路径。
2. **读者只会看到完整的旧快照或完整的新快照。** [根 AXIOM: 1; LEMMA: 1]
    - 推理：半写的快照被当真，状态就与日志分叉且无迹可寻。临时文件加同目录改名把半写排除在可见状态之外。
3. **文件带版本与正文哈希；任何不符都判为 `Corrupt`。** [LEMMA: 1, 2]
    - 推理：改名只防半写，防不了位翻转或人为改动；有了校验，「不可信」能被发现，LEMMA 1 让发现之后的处理很简单。
4. **只收发文本。** [根 AXIOM: 3; 根 LEMMA: 4]
    - 推理：本库拥有的是「这段文本被原子地、可校验地保存了」。值怎么变成文本是值的主人的事；在组合里由组合者把 `CheckpointJson.write` 的结果交给本库。

# SPEC
1. [LEMMA: 2] 保存后读出等于原值；再次保存覆盖旧值；不留 `.tmp`。
    - 证明：`tests/Tests.fs` `save then load round-trips and overwrites`，通过。
2. [LEMMA: 3] 文件任一字节翻转、任一位置截断，`Load` 返回 `Corrupt`。
    - 证明：`any flipped or truncated byte is Corrupt`，逐字节穷举，通过。
3. [LEMMA: 2] 崩溃遗留的 `.tmp` 不影响读取，下次保存覆盖它。
    - 证明：`leftover tmp file does not affect existing snapshot`，通过。
4. [LEMMA: 3] 头与正文都严格按 UTF-8 解码；头里有非法字节判 `Corrupt`，原因指明是头。
    - 证明：`invalid utf8 in header is Corrupt`，通过。
5. [LEMMA: 1] 真实组合中，删掉快照或快照与日志对不上时，组合者从日志重建，结果相同。
    - 证明：`compose/LedgerStore/tests/Tests.fs` `commits survive reopen with and without snapshot`、`stale snapshot from a truncated log is ignored`。
6. [TODO] 改名后没有对所在目录 fsync，断电时改名本身可能丢失（读到旧快照，按 LEMMA 1 仍安全，只是慢）。

7. [根 SPEC 3] 实例隔离；文件被外部弄坏后 `Load` 如实报 `Corrupt`，下一次 `Save` 覆盖它；`Load` 交出的文本是副本，不是内部状态的活视图。
    - 证明：`instances are isolated and a corrupt file is replaced by the next save`，通过。
    - 剩余边界：私有状态就是它管的那个路径；两个实例指向同一路径时共用那个文件，这是调用方的选择，不是别名。
