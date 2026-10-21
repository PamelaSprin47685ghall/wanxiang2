# DraftStore

本机草稿与未确认发送的快照、drafts.json 编解码、落盘去重闸门、快照合并，迁自旧 `Model/DraftStore.fs`。旧的 IDraftBackend 与三个后端（文件、内存、浏览器）是端口式注入，这里不迁：`DraftStore.Save` 直接返回 `DraftWrite`（不写 / 写这段文本 / 删除），宿主按返回值去写自己的落点；「是否启用」也由宿主判断，关闭时不调用。内存 ⇄ 快照的转换（`DraftPolicy.snapshotOf`、`restoreInto`）依赖输入框草稿与发件箱，随 UiControllers 处理。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## DraftBackend（宿主桥，2026-10-17 增补）

入口外包后宿主就是本仓库，旧 `Model/DraftStore.fs` 的落点后端迁入本库（`DraftBackend.fs`）：`IDraftBackend`（签名与旧版逐字一致，PWA 侧按同一契约实现 IndexedDB 后端）、`FileDraftBackend`（原子写 tmp+rename、0600、`drafts.json`）、`MemoryDraftBackend`（测试替身与无持久存储退路）。与旧版的偏差：`FileDraftBackend.DefaultPath` 由无参改为接受配置目录参数——本库不依赖 LocalState 包（库间无依赖边），路径拼接结果相同，由入口传入 `Path.GetDirectoryName configPath`。

# LEMMA
1. **drafts.json 的字节不变。** [根 AXIOM: 1] 同一快照写出的文本、同一文本读出的快照与旧 DraftCodec 相同；读到更高版本整份忽略。
2. **保存决定与旧 DraftStore 对后端的动作一致。** [根 AXIOM: 1] 同一串快照，旧版对后端的写/删/不动与本库返回的 DraftWrite 一一对应。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过：`a recycled page restores drafts from the browser backend`、`after the host clears storage the same snapshot is written again`、`conversations that vanish from the list count as deleted unless a send is in flight`、`restore notice: unsent wins, attachments ask for reselect, nothing restored is silent`、`file draft backend round-trips, clears, and tolerates missing files`、`memory draft backend keeps per-instance state`。

# 迁入或覆盖的旧测试
- ComposerReliabilityTests：`a recycled page restores drafts from the browser backend`（后端换成宿主保存的文本）、`conversations that vanish from the list count as deleted unless a send is in flight`
- 未迁：`when turned off nothing is written and what was stored is deleted` 测的是启用开关，开关归宿主，留在旧仓库随宿主改写。

## DraftRestore（迁自旧 AppShell）
- 来源：旧 AppShell.RestoreLocalDrafts / UpdateDraftSummary / PruneDeletedConversationDrafts / ClearLocalDrafts 中不碰界面的判断。
- 覆盖的旧测试：旧仓库没有直接测这些私有成员的测试；这里的行为测试是新增覆盖，见 SPEC 1 的用例名。
