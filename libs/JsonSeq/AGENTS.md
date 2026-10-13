# JsonSeq

RFC 7464 分帧与单文件追加日志。不依赖任何其他库。AXIOM 见项目根目录 AGENTS.md。

# LEMMA
1. [AXIOM: 1, 7] 记录边界只由 RS 决定，记录体以 LF 结尾才算完整。
    - 推理：持久化日志在崩溃后最常见的损坏是尾部半写；RS 作为唯一边界使半写只影响最后一条，前面记录可以无歧义恢复，这是用户在乎的「数据不丢」。不用长度前缀，因为长度字段本身也可能半写，多一处要校验的东西。
2. [AXIOM: 7; LEMMA: 1] 本库不理解 JSON，只搬运文本。
    - 推理：解析与规范化是消费方的事；不解析，本库就没有 JSON 依赖，契约只剩「文本进、文本出、带偏移」。

3. [AXIOM: 1; LEMMA: 1] 本库从不把读不懂的字节悄悄变成看似有效的数据；每段不可信字节都以 `fault` 交给调用方。
    - 推理：宽松解码会把损坏字节变成 U+FFFD，之后的解析可能照样成功，损坏就此被吞掉，用户的数据变了却没人知道。忽略游离字节同理：整份不是日志的文件会被读成「健康的空日志」，再追加就覆盖了用户的东西。

# SPEC
1. [LEMMA: 1] `Frame.encode` 与 `Frame.split` 往返保持文本与偏移。
    - 证明：tests/Tests.fs `encode then split round-trips text and offsets`。
2. [LEMMA: 1] 字节流在任意位置截断，切分结果为原记录的完整前缀，加至多一条 `fault = Some TornTail` 的尾记录。
    - 证明：`truncation at every byte ...`，逐字节截断穷举。
3. [AXIOM: 10; LEMMA: 1] 文本含 RS 时 `encode` 抛出 `ArgumentException`（防呆：否则边界被破坏且不可撤销）。
    - 证明：`encode rejects RS inside payload`。
4. [LEMMA: 1] `JsonSeqFile.Append` 返回前 `Flush(true)`；重开后可从任一返回的偏移续读；`Truncate` 回滚到给定偏移。
    - 证明：`file append survives reopen and truncate rolls back`。「Flush(true) 真正抵达磁盘」依赖操作系统，无法在单元测试中证明。
5. [LEMMA: 3] 非法 UTF-8 的记录给出 `fault = Some InvalidUtf8`，不替换为 U+FFFD。
    - 证明：`invalid utf8 is reported not replaced`。
6. [LEMMA: 3] 第一个 RS 之前的字节（包括完全没有 RS 的文件）给出一条 `fault = Some StrayBytes` 记录，覆盖这些字节的区间。
    - 证明：`bytes before first RS are stray`。
7. [LEMMA: 1] `Read(from, until)` 只读出区间内的记录，供调用方按已知偏移随机读取单条。
    - 证明：`Read returns only records within range`。
8. [TODO] `ReadFrom` 一次读入到末尾的全部字节；超大日志需要流式读取。
