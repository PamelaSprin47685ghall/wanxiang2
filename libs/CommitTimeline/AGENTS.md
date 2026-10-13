# CommitTimeline

客户端消息列表（JsonArray）按 commitId 的操作：查找、删除、把更早一页前置并去重（从旧 ClientState 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`CommitTimeline.commitIdOf`、`contains`、`without`、`prependOlder`。

# LEMMA
1. **commitId 是消息的唯一标识，重复推送、快照与 catch-up 交叠时都按它去重。** [根 AXIOM: 5]
2. **非对象项、没有 commitId 的项原样保留、不参与匹配。** [根 AXIOM: 5]
3. **唯一改动：列表里的 JSON null 项会原样保留，旧实现在这里抛 NullReferenceException。** [根 AXIOM: 2]

# SPEC
1. [LEMMA: 1, 2, 3] 本库测试通过（含非整数 commitId 抛出，与旧实现一致）。

# 迁入或覆盖的旧测试
- 经 compose/WsClient 覆盖：`ClientState HistoryPage prepends older messages and deduplicates`、`ClientState MessageCommitted appends message, updates watermark, deduplicates and raises event`、`ClientState ConversationUpdated updates title and removes deleted messages`、`ClientState HistoryPage handles non-JsonObject items and updates pagination`、`ClientState MessageCommitted handles messages collection with non-JsonObject items`、`ClientState ConversationUpdated deletedMessage preserves non-JsonObject items`、`ClientState AuthorityCatchUp decodes string commit lines and handles AgentMessageRecorded and MessageDeleted`。
