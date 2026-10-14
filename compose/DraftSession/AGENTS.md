# DraftSession（组合者）

本地草稿会话：已存快照、读完标志、已恢复主机、认得的对话、未确认消息的保存时刻，与 DraftStore 的落盘闸门。迁自旧 `AppShell.fs` 76-87 行的字段与 446-525 行、2530-2545 行的 `CurrentDraftSnapshot`、`FlushDrafts`、`RestoreLocalDrafts`、`ClearLocalDrafts`、`PruneDeletedConversationDrafts`、`LoadLocalDrafts` 里不碰界面的部分。协作者：DraftStore（快照类型、编解码、闸门、DraftPolicy/DraftRestore 纯函数）。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

宿主仍负责：读写落点、内存草稿与发件箱（ComposerDrafts、Outbox 的 `DraftPolicy.snapshotOf`/`restoreInto`）、输入框、提示、计时器（800ms 自动保存、关窗、页面隐藏）。

# LEMMA
1. **读完之前不写盘**（旧 FlushDrafts 的 `draftsLoaded` 守卫）；保存关闭时也不写；内容没变不写。
2. **每台主机只放回一次**；保存时只有已恢复的主机以内存为准，其他主机的已存内容原样保留。
3. **只清理确认已删的对话**：曾认得、列表里没有、没有在途发送。
4. **清除不复位读完标志、已恢复主机与已知对话**（与旧实现相同）。

# SPEC
1. [LEMMA: 1–4] `tests/Tests.fs` 经 DraftStore 交付包编译运行通过。

# 迁入或覆盖的旧测试（ComposerReliabilityTests，会话一侧；输入框、发件箱、文件权限部分留旧仓库）
- `closing and reopening restores the draft text and marks attachments as needing reselection`
- `deleting a conversation removes its draft from memory and from disk`
- `switching the setting off deletes the file and clear drafts empties everything`
- `saving while connected to one host keeps another host's stored drafts`（Outbox 已迁内存一侧，本库覆盖合并一侧）

# 时序注意（FlushDrafts）
旧 `FlushDrafts` 先 `SyncActiveDraftText` 再取快照；宿主调用 `Flush` 前同样要先把输入框文字同步进内存草稿再算 `current`。旧实现在未读完时连快照都不算（不调用 `savedAtOf`），新实现由宿主在 `Flush` 前计算快照、会提前记下保存时刻：宿主若想完全保持旧时序，可先看 `IsLoaded` 再算快照。

# 与旧实现的差异
- 旧落点闸门在 `LocalDraftStore`（宿主）里；这里用 DraftStore 库的 `DraftStore` 对象，`Flush` 返回 `DraftWrite`，宿主按它写或删。
