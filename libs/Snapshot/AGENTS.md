# Snapshot

原子快照文件：保存一段文本，带版本与完整性校验。不依赖任何其他库。AXIOM 见项目根目录 AGENTS.md。

公开接口：`Snapshot.save path text`、`Snapshot.load path : Loaded`（`Missing | Loaded of string | Corrupt of reason`）。

# LEMMA
1. [AXIOM: 1, 7] 快照是可重建的缓存，不是事实来源。
    - 推理：用户在乎的是启动快、数据不丢（AXIOM 1）。事实在日志里；快照只省时间。于是快照坏了的正确处理是忽略并重建，不需要修复、迁移或兼容路径（AXIOM 7）。
2. [AXIOM: 1; LEMMA: 1] 读者只会看到完整的旧快照或完整的新快照。
    - 推理：半写的快照若被当作有效值读入，会让状态与日志分叉，且没有任何迹象。临时文件加改名（同目录 rename 原子）把半写排除在可见状态之外。
3. [LEMMA: 1, 2] 文件带版本与正文哈希；任何不符都判为 Corrupt，交给调用方重建。
    - 推理：rename 只防半写，防不了磁盘位翻转或人为改动；校验和使「不可信」可被检测，LEMMA 1 使检测到之后的处理平凡。

4. [AXIOM: 7, 8] 本库只收发文本，不接收编解码函数。
    - 推理：「把值变成文本」是值所属领域的事；本库拥有的语义是「这段文本被原子地、可校验地保存了」。接收编解码函数就是把别人的行为塞进来调用，文本才是诚实的边界（见 Ledger LEMMA 2）。

# SPEC
1. [LEMMA: 2] 保存后读出等于原值；再次保存覆盖旧值；不残留 `.tmp`。
    - 证明：tests/Tests.fs `save then load round-trips and overwrites`。
2. [LEMMA: 3] 文件任一字节翻转、任一位置截断，`load` 返回 `Corrupt`。
    - 证明：`any flipped or truncated byte is Corrupt`（逐字节穷举）。
3. [LEMMA: 2] 崩溃遗留的 `.tmp` 不影响读取，下次保存直接覆盖。
    - 证明：`leftover tmp file does not affect existing snapshot`。
4. [TODO] 改名后未对所在目录做 fsync，断电时改名本身可能丢失（此时读到旧快照，按 LEMMA 1 仍安全，只是慢）。
