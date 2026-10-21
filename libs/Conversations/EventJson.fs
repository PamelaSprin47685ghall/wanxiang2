namespace Wanxiang.Core

open System
open System.Globalization
open System.Text.Json
open System.Text.Json.Nodes

/// 领域事件的 JSON 形状。它写在用户磁盘上的日志里，也随提交发给客户端：类型名、字段名、版本号都不能改。
module EventJson =

    /// 每个事件类型的 data 结构版本。v1 永久固定；要改结构只能加新版本并保留旧版本的读取。
    [<Literal>]
    let Version = 1

    let eventType (ev: EventData<'C>) : string =
        match ev with
        | ConversationCreated _ -> "conversation.created"
        | ConversationForked _ -> "conversation.forked"
        | ConversationRenamed _ -> "conversation.renamed"
        | ConversationConfigUpdated _ -> "conversation.config-updated"
        | ConversationDeleted _ -> "conversation.deleted"
        | ConversationFlagsChanged _ -> "conversation.flags-changed"
        | AgentMessageRecorded _ -> "agent-message-recorded"
        | MessageDeleted _ -> "message.deleted"

    let private tryGet (o: JsonObject) (key: string) : JsonNode option =
        let mutable n: JsonNode = null

        if o.TryGetPropertyValue(key, &n) then
            if isNull n then None else Some n
        else
            None

    let private tryGetInt (v: JsonNode) : int option =
        match v with
        | :? JsonValue as jv ->
            match jv.TryGetValue<int>() with
            | true, i -> Some i
            | _ -> None
        | _ -> None

    let private tryGetUInt64 (v: JsonNode) : uint64 option =
        match v with
        | :? JsonValue as jv ->
            match jv.TryGetValue<uint64>() with
            | true, i -> Some i
            | _ -> None
        | _ -> None

    let toJson (config: 'C -> JsonObject) (ev: EventData<'C>) : JsonObject =
        let o = JsonObject()
        o["type"] <- eventType ev
        o["version"] <- Version
        let data = JsonObject()

        match ev with
        | ConversationCreated d ->
            data["conversationId"] <- d.conversationId.ToString("D")
            data["title"] <- d.title
            data["config"] <- config d.config
        | ConversationForked d ->
            data["conversationId"] <- d.conversationId.ToString("D")
            data["parentConversationId"] <- d.parentConversationId.ToString("D")

            match d.forkAfterId with
            | Some id -> data["forkAfterId"] <- id
            | None -> ()
        | ConversationRenamed d ->
            data["conversationId"] <- d.conversationId.ToString("D")
            data["title"] <- d.title
        | ConversationConfigUpdated d ->
            data["conversationId"] <- d.conversationId.ToString("D")
            data["config"] <- config d.config
        | ConversationDeleted d -> data["conversationId"] <- d.conversationId.ToString("D")
        | ConversationFlagsChanged d ->
            data["conversationId"] <- d.conversationId.ToString("D")
            data["pinned"] <- d.pinned
            data["archived"] <- d.archived
        | AgentMessageRecorded d ->
            data["conversationId"] <- d.conversationId.ToString("D")
            data["payload"] <- d.payloadJson.DeepClone()
        | MessageDeleted d ->
            data["conversationId"] <- d.conversationId.ToString("D")
            data["messageCommitId"] <- d.messageCommitId

        o["data"] <- data
        o

    let private parseGuid (v: JsonNode) : Guid option =
        try
            match v.GetValueKind() with
            | JsonValueKind.String -> Some(Guid.Parse(v.GetValue<string>()))
            | _ -> None
        with _ ->
            None

    let private parseId (v: JsonNode) : uint64 option =
        try
            match v.GetValueKind() with
            | JsonValueKind.Number -> tryGetUInt64 v
            | _ -> None
        with _ ->
            None

    let private configFromNode (config: JsonObject -> 'C) (v: JsonNode) : 'C option =
        match v with
        | :? JsonObject as o -> Some(config o)
        | _ -> None

    let private tryString (o: JsonObject) (k: string) : string option =
        match tryGet o k with
        | Some v when v.GetValueKind() = JsonValueKind.String -> Some(v.GetValue<string>())
        | _ -> None

    /// 按事件外壳解析为领域事件。结构非法或版本未知返回 None（触发损坏处理）。
    let tryFromJson (config: JsonObject -> 'C) (o: JsonObject) : EventData<'C> option =
        match tryGet o "type", tryGet o "version", tryGet o "data" with
        | Some typeNode, Some versionNode, Some dataNode when
            typeNode.GetValueKind() = JsonValueKind.String
            && versionNode.GetValueKind() = JsonValueKind.Number
            && dataNode.GetValueKind() = JsonValueKind.Object
            ->
            match tryGetInt versionNode with
            | Some v when v = Version ->
                let data = dataNode.AsObject()

                let guid k =
                    match tryGet data k with
                    | Some v -> parseGuid v
                    | None -> None

                let commitId k =
                    match tryGet data k with
                    | Some v -> parseId v
                    | None -> None

                let cfg () =
                    match tryGet data "config" with
                    | Some v -> configFromNode config v
                    | None -> None

                match tryString o "type" with
                | Some "conversation.created" ->
                    match guid "conversationId", cfg () with
                    | Some cid, Some c ->
                        let title = tryString data "title" |> Option.defaultValue ""

                        Some(
                            ConversationCreated
                                {
                                    conversationId = cid
                                    title = title
                                    config = c
                                }
                        )
                    | _ -> None
                | Some "conversation.forked" ->
                    match guid "conversationId", guid "parentConversationId" with
                    | Some cid, Some pid ->
                        let after =
                            match tryGet data "forkAfterId" with
                            | Some v -> parseId v
                            | None -> None

                        Some(
                            ConversationForked
                                {
                                    conversationId = cid
                                    parentConversationId = pid
                                    forkAfterId = after
                                }
                        )
                    | _ -> None
                | Some "conversation.renamed" ->
                    match guid "conversationId" with
                    | Some cid ->
                        let title = tryString data "title" |> Option.defaultValue ""
                        Some(ConversationRenamed { conversationId = cid; title = title })
                    | _ -> None
                | Some "conversation.config-updated" ->
                    match guid "conversationId", cfg () with
                    | Some cid, Some c -> Some(ConversationConfigUpdated { conversationId = cid; config = c })
                    | _ -> None
                | Some "conversation.deleted" ->
                    match guid "conversationId" with
                    | Some cid -> Some(ConversationDeleted { conversationId = cid })
                    | _ -> None
                | Some "conversation.flags-changed" ->
                    match guid "conversationId" with
                    | Some cid ->
                        let flag k =
                            match tryGet data k with
                            | Some v -> v.GetValueKind() = JsonValueKind.True
                            | None -> false

                        Some(
                            ConversationFlagsChanged
                                {
                                    conversationId = cid
                                    pinned = flag "pinned"
                                    archived = flag "archived"
                                }
                        )
                    | _ -> None
                | Some "agent-message-recorded" ->
                    match guid "conversationId" with
                    | Some cid ->
                        match tryGet data "payload" with
                        | Some p ->
                            Some(
                                AgentMessageRecorded
                                    {
                                        conversationId = cid
                                        payloadJson = p.DeepClone()
                                    }
                            )
                        | None -> None
                    | _ -> None
                | Some "message.deleted" ->
                    match guid "conversationId", commitId "messageCommitId" with
                    | Some cid, Some mid ->
                        Some(
                            MessageDeleted
                                {
                                    conversationId = cid
                                    messageCommitId = mid
                                }
                        )
                    | _ -> None
                | _ -> None
            | _ -> None
        | _ -> None
