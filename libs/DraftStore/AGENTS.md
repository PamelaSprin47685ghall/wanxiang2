# DraftStore

本机草稿与未确认发送的快照、drafts.json 编解码、落盘去重闸门、快照合并，迁自旧 `Model/DraftStore.fs`。旧的 IDraftBackend 与三个后端（文件、内存、浏览器）是端口式注入，这里不迁：`DraftStore.Save` 直接返回 `DraftWrite`（不写 / 写这段文本 / 删除），宿主按返回值去写自己的落点；「是否启用」也由宿主判断，关闭时不调用。内存 ⇄ 快照的转换（`DraftPolicy.snapshotOf`、`restoreInto`）依赖输入框草稿与发件箱，随 UiControllers 处理。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **drafts.json 的字节不变。** [根 AXIOM: 1] 同一快照写出的文本、同一文本读出的快照与旧 DraftCodec 相同；读到更高版本整份忽略。
2. **保存决定与旧 DraftStore 对后端的动作一致。** [根 AXIOM: 1] 同一串快照，旧版对后端的写/删/不动与本库返回的 DraftWrite 一一对应。

# SPEC
1. [LEMMA: 1, 2] `tests/golden/expected.txt` 由旧 DraftCodec、DraftStore(MemoryDraftBackend)、DraftPolicy 跑 `tests/Render.fs` 生成；金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`（跑旧仓库构建出的程序集）；重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- ComposerReliabilityTests：`a recycled page restores drafts from the browser backend`（后端换成宿主保存的文本）、`conversations that vanish from the list count as deleted unless a send is in flight`
- 未迁：`when turned off nothing is written and what was stored is deleted` 测的是启用开关，开关归宿主，留在旧仓库随宿主改写。

## DraftRestore（迁自旧 AppShell）
- 来源：旧 AppShell.RestoreLocalDrafts / UpdateDraftSummary / PruneDeletedConversationDrafts / ClearLocalDrafts 中不碰界面的判断。
- 金标准：tests/golden/restore.txt，由 tools/gen-restore-golden.fsx 跑 tools/OldDraftRestore.fs（旧代码原样摘录；私有成员无法从旧程序集调用）生成。
- 覆盖的旧测试：旧仓库没有直接测这些私有成员的测试；这里的金标准与行为测试是新增覆盖。
