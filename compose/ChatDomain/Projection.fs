namespace Wanxiang.Core

open System

type Conversation = Conversation<SessionConfig>
type Projection = Projection<SessionConfig>

/// 旧仓库的投影入口：转调 Conversations 库，只把配置类型定为 SessionConfig、把提交外壳拆开。
module Projection =

    let empty: Projection = Conversations.empty
    let tryConversation (proj: Projection) (id: Guid) : Conversation option = Conversations.tryConversation proj id

    let effectiveMessagesAtMap
        (conversations: Map<Guid, Conversation>)
        (convId: Guid)
        (untilCommitId: CommitId)
        : MessageRecord list =
        Conversations.effectiveMessagesAtMap conversations convId untilCommitId

    let effectiveMessagesAt (proj: Projection) (convId: Guid) (untilCommitId: CommitId) : MessageRecord list =
        Conversations.effectiveMessagesAt proj convId untilCommitId

    let effectiveMessages (proj: Projection) (conv: Conversation) : MessageRecord list =
        Conversations.effectiveMessages proj conv

    let conversationList (proj: Projection) : Conversation list = Conversations.conversationList proj

    /// 应用一条提交。失败原因归入 ValidationError，由存储决定截尾还是只读。
    let applyCommit (proj: Projection) (commit: Events.Commit) : Result<Projection, WanxiangError> =
        Conversations.apply proj commit.id commit.committedAtUtc commit.events
        |> Result.mapError ValidationError

/// 快照正文的 JSON 形状，权威实现在 Conversations 库。
module ProjectionCodec =
    open System.Text.Json.Nodes

    let toJson (p: Projection) : JsonObject =
        ProjectionJson.toJson SessionConfig.toJson p

    let tryFromJson (o: JsonObject) : Projection option =
        ProjectionJson.tryFromJson SessionConfig.fromJson o
