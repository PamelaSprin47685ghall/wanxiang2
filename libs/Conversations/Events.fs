namespace Wanxiang.Core

open System
open System.Text.Json.Nodes

/// 领域事件数据。每个事件拥有稳定 type + version（v1 语义永久固定）。
type ConversationCreatedData<'C> =
    {
        conversationId: Guid
        title: string
        config: 'C
    }

type ConversationForkedData =
    {
        conversationId: Guid
        parentConversationId: Guid
        /// 父对话中被继承的最后一条消息的全局提交 id（编辑第一条消息时为 None）
        forkAfterId: uint64 option
    }

type ConversationRenamedData = { conversationId: Guid; title: string }

type ConversationConfigUpdatedData<'C> = { conversationId: Guid; config: 'C }

type ConversationDeletedData = { conversationId: Guid }

/// 会话标记（置顶 / 归档）。与会话配置一样是**整体快照**而非 patch：
/// 重放时不需要考虑事件先后组合，读一条就知道当前状态。
type ConversationFlagsChangedData =
    {
        conversationId: Guid
        pinned: bool
        archived: bool
    }

/// Agent Framework 完整消息：payload 原样透明保存（MAF schema 的 ChatMessage JSON）。
type AgentMessageRecordedData =
    {
        conversationId: Guid
        payloadJson: JsonNode
    }

type MessageDeletedData =
    {
        conversationId: Guid
        /// 被删除消息的全局提交 id（消息标识）
        messageCommitId: uint64
    }

/// 'C 是会话配置。领域只保存、复制它，从不解读它，所以不依赖它的具体类型。
type EventData<'C> =
    | ConversationCreated of ConversationCreatedData<'C>
    | ConversationForked of ConversationForkedData
    | ConversationRenamed of ConversationRenamedData
    | ConversationConfigUpdated of ConversationConfigUpdatedData<'C>
    | ConversationDeleted of ConversationDeletedData
    | ConversationFlagsChanged of ConversationFlagsChangedData
    | AgentMessageRecorded of AgentMessageRecordedData
    | MessageDeleted of MessageDeletedData
