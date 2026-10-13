# AttachmentBlobs

内容寻址的分块上传：SHA-256 就是永久标识；完成前只存在于临时文件；完成时校验长度和哈希；相同内容只存一份。不懂媒体类型和文件名。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`BlobError`、`AttachmentBlobs(root, maxBytes, maxChunkBytes)`，成员 PathOf、Begin、Append、Complete、Abort、AbortByOwner、OpenRead、Exists、Dispose。

# LEMMA
1. **分块按序号严格递增到达。** [根 AXIOM: 4]
    - 推理：重复分块会让临时文件写放大，乱序要等到完成时校验哈希才会发现（旧决策 71）。
2. **校验失败时什么都不留下。** [根 AXIOM: 4]
3. **错误文字与旧实现相同。** [根 AXIOM: 5]
    - 推理：这些文字会出现在线上的错误事件里。

# SPEC
1. [LEMMA: 1, 3] `bad input is refused with the old texts`，通过。
2. [LEMMA: 2] `wrong content or wrong declared hash...`、`abort by owner...`，通过。
3. 内容寻址、只存一份：`chunks in order complete...`，通过。
