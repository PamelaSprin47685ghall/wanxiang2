# JsonSeq

RFC 7464 分帧（纯函数 `Frame`）与单文件追加日志对象 `JsonSeqFile`。`JsonSeqFile` 的私有状态是一个无缓冲的文件流。不依赖任何兄弟库，不理解 JSON，也不认识 Ledger。根规则见项目根目录 AGENTS.md。

公开面：
- `Frame.encode : string -> byte[]`、`Frame.split : int64 -> ReadOnlySpan<byte> -> Record list`；`Record = { payload; fault; startOffset; endOffset }`。
- `JsonSeqFile(path)`：`Append(text) : AppendOutcome`（`Appended endOffset | NotWritten reason | Unknown reason`）、`Read(from, until)`、`ReadFrom(offset)`、`Truncate(offset)`、`Length`、`Dispose()`。

# LEMMA
1. **记录边界只由 RS 决定，记录体以 LF 结尾才算完整。** [根 AXIOM: 1, 2]
    - 推理：崩溃后最常见的损坏是尾部半写。RS 作唯一边界，半写只影响最后一条，前面的记录能无歧义恢复。不用长度前缀：长度字段自己也可能半写，多一处要校验的东西。
2. **本库只搬运文本，不解析 JSON。** [根 AXIOM: 2, 3]
    - 推理：解析与规范化是使用方的事；契约只剩「文本进、文本出、带偏移」。
3. **读不懂的字节从不变成看似有效的数据；每段不可信字节都以 `fault` 交出。** [根 AXIOM: 1, 5; LEMMA: 1]
    - 推理：宽松解码把坏字节变成 U+FFFD，后续解析照样成功，损坏就被吞掉。忽略游离字节同理：一个不是日志的文件会被读成「空日志」，再追加就盖掉用户的东西。
4. **追加的结果只说有证据的话：写成、确知没写、未知。** [根 AXIOM: 5; 根 LEMMA: 3]
    - 推理：写失败后能截回原长度，才确知文件没变；截回也失败，文件里可能有这条的全部、部分或没有。把这种情况报成「没写」会让调用方重试，可能写出重复或半条（根 SPEC 4：未知不能自动转成再执行的许可）。文件流不带缓冲：失败后缓冲区里残留的字节会在下次 Flush 或 Dispose 时写进文件，状态就不再只归本对象管（根 LEMMA 3）。

# SPEC
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
8. [LEMMA: 4] 写之前就失败给出 `NotWritten`；真实的写失败（ENOSPC）且截不回时给出 `Unknown`。
    - 证明：`failure before writing is NotWritten`；`write failure without rollback evidence is Unknown`（真实设备 `/dev/full`，仅 Linux）。都通过。
    - 剩余边界：[TODO]「写失败但截回成功 → NotWritten」这条分支没有真实故障能触发它的测试。
9. [LEMMA: 1, 3] 没有正文的 RS 不被忽略：在末尾算 `TornTail`，在中间算 `EmptyRecord`；记录的偏移首尾相接，覆盖全部字节。
    - 证明：`bare RS is a faulted record, never skipped`，通过。缺陷由 compose 的逐字节崩溃测试发现。
10. [TODO] `ReadFrom` 一次读入到末尾的全部字节；超大日志需要流式读取。
