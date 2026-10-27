# AttachmentBlobs

合并包：AttachmentBlobs、MediaSniff。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `AttachmentBlobs`

## AttachmentBlobs

内容寻址的分块上传：SHA-256 就是永久标识；完成前只存在于临时文件；完成时校验长度和哈希；相同内容只存一份。不懂媒体类型和文件名。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`BlobError`、`AttachmentBlobs(root, maxBytes, maxChunkBytes)`，成员 PathOf、Begin、Append、Complete、Abort、AbortByOwner、OpenRead、Exists、Dispose。

## LEMMA
1. **分块按序号严格递增到达。** [根 AXIOM: 4]
    - 推理：重复分块会让临时文件写放大，乱序要等到完成时校验哈希才会发现（旧决策 71）。
2. **校验失败时什么都不留下。** [根 AXIOM: 4]
3. **错误文字与旧实现相同。** [根 AXIOM: 5]
    - 推理：这些文字会出现在线上的错误事件里。

## SPEC
1. [LEMMA: 1, 3] `bad input is refused with the old texts`，通过。
2. [LEMMA: 2] `wrong content or wrong declared hash fails and leaves nothing`、`abort by owner removes only that owner's uploads`，通过。
3. 内容寻址、只存一份：`chunks in order complete to a content-addressed file stored once`，通过。

## 变更记录
- `BlobError` 加 `[<RequireQualifiedAccess>]`：`Failed`、`Invalid` 等通用名不再泄漏进同时打开 Wanxiang.Server 与 Wanxiang.Agent 的组合者。

## 成员 `MediaSniff`

## MediaSniff

文件头嗅探、媒体类型取舍和文件名清理。不碰文件系统。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`MediaSniff.sniff`、`resolve`、`sanitizeFileName`。

## LEMMA
1. **只看前 512 字节的魔数和字符种类。** [根 AXIOM: 3]
    - 推理：沿用旧决策 Q176，够用就行，不引入完整的类型库。
2. **声明值优先；只有声明为空或 octet-stream 时才采用嗅探结果。** [根 AXIOM: 5]
3. **文件名只用于显示，绝不用作路径。** [根 AXIOM: 4]

## SPEC
1. [LEMMA: 1, 2, 3] `tests/MediaSniff.Tests.fs` 的 `magic numbers win; printable bytes are text; control bytes are unknown`、`declared type is kept unless empty or octet-stream`、`file names lose control characters, are capped and trimmed` ，通过。
