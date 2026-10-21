namespace Wanxiang.Client

open System
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Wanxiang.Core
open Wanxiang.Protocol

/// 客户端 WebSocket 连接（fire-and-forget 事件协议），公开面与旧实现相同。
/// 组合 TextSocket（按代管理的文本传输）与 WireAgui（AG-UI 承载的编解码）。
/// 断线后由上层决定重连（决策 26/27：重连后重新 observe）。
type WsClient() =

    let socket = TextSocket()
    let onEvent = Event<WireEvent>()
    let onClosed = Event<exn option>()
    let onConnectionEvent = Event<int * WireEvent>()
    let onConnectionClosed = Event<int * exn option>()

    do
        socket.TextReceived.Add(fun (generation, text) ->
            // AG-UI 承载：宽容解码。未知类型（Ok None）与解码失败都忽略——
            // 客户端对协议错误的兜底是重连，不是断言（与主机的严格度不同）。
            match WireAgui.tryDecode text with
            | Ok(Some ev) when socket.IsCurrent generation ->
                onConnectionEvent.Trigger(generation, ev)
                onEvent.Trigger ev
            | _ -> ())

        socket.Closed.Add(fun (generation, error) ->
            onConnectionClosed.Trigger(generation, error)
            onClosed.Trigger error)

    member _.EventReceived = onEvent.Publish
    member _.Closed = onClosed.Publish
    member _.ConnectionEventReceived = onConnectionEvent.Publish
    member _.ConnectionClosed = onConnectionClosed.Publish
    member _.ConnectionGeneration = socket.Generation
    member _.IsConnected = socket.IsConnected

    /// 连接并启动接收循环（认证前连接处于受限状态，决策 55）。
    member _.ConnectWithGenerationAsync(uri: Uri, ct: CancellationToken) : Task<int> = socket.ConnectAsync(uri, ct)

    member _.ConnectAsync(uri: Uri, ct: CancellationToken) : Task =
        task {
            let! _ = socket.ConnectAsync(uri, ct)
            return ()
        }

    member _.Disconnect() = socket.Disconnect()

    /// 返回传输是否完成，而不是把断线当成功。业务保存仍只认 command.committed。
    member _.TrySendAsync(ev: WireEvent) : Task<bool> =
        socket.TrySendAsync(WireAgui.encode ev, None)

    member _.TrySendCommandAsync(cmd: ClientCommand) : Task<bool> =
        socket.TrySendAsync(WireAgui.encodeCommand cmd, None)

    member _.TrySendCommandAtGenerationAsync(generation: int, cmd: ClientCommand) : Task<bool> =
        socket.TrySendAsync(WireAgui.encodeCommand cmd, Some generation)

    member _.TrySendAtGenerationAsync(generation: int, ev: WireEvent) : Task<bool> =
        socket.TrySendAsync(WireAgui.encode ev, Some generation)

    /// 保留原有 API；需要用户反馈的调用方使用 TrySend 系列。
    member this.SendAsync(ev: WireEvent) : Task =
        task {
            let! _ = this.TrySendAsync ev
            return ()
        }

    member this.SendCommandAsync(cmd: ClientCommand) : Task =
        task {
            let! _ = this.TrySendCommandAsync cmd
            return ()
        }

/// 客户端对话状态（内存态；PWA 不持久缓存，决策 3）。
type ConversationView =
    {
        conversationId: Guid
        mutable title: string
        mutable lastCommitId: CommitId
        mutable runtimeState: string
        mutable messages: JsonArray
        /// 快照携带的最早 commitId 与是否还有更早历史（Q127 分页）
        mutable pageEarliest: CommitId
        mutable pageHasMore: bool
        /// 对话级配置快照（P0-4：UI 读写 SessionConfig）
        mutable config: SessionConfig
    }

/// 客户端状态机：维护观察视图 + 游标。
type ClientState() =

    let mutable conversationList: JsonArray = JsonArray()
    let mutable conversations: Map<Guid, ConversationView> = Map.empty
    let mutable cursor: CommitId = 0UL
    let mutable latestCommitId: CommitId = 0UL

    let listChanged = Event<JsonArray>()
    let convChanged = Event<Guid>()
    let cursorChanged = Event<CommitId>()

    member _.ConversationList = conversationList
    member _.Conversations = conversations
    member _.Cursor = cursor
    member _.LatestCommitId = latestCommitId

    /// 实例身份改变时清空旧投影，绝不把另一台主机的游标带过来。
    member _.Reset() =
        conversationList <- JsonArray()
        conversations <- Map.empty
        cursor <- 0UL
        latestCommitId <- 0UL

    member _.ListChanged = listChanged.Publish
    member _.ConversationChanged = convChanged.Publish
    member _.CursorChanged = cursorChanged.Publish

    /// 处理主机事件，更新本地状态。
    member this.Handle(ev: WireEvent) : unit =
        match ev with
        | ConversationListSnapshot d ->
            conversationList <- d.items
            latestCommitId <- max latestCommitId d.lastCommitId
            listChanged.Trigger conversationList
        | ConversationSnapshot d ->
            let view =
                match conversations.TryFind d.conversationId with
                | Some v ->
                    v.title <- d.title
                    v.lastCommitId <- d.lastCommitId
                    v.runtimeState <- d.runtimeState
                    v.messages <- d.messages
                    v.pageEarliest <- d.snapshotEarliestCommitId
                    v.pageHasMore <- d.snapshotHasMore
                    v.config <- d.config
                    v
                | None ->
                    {
                        conversationId = d.conversationId
                        title = d.title
                        lastCommitId = d.lastCommitId
                        runtimeState = d.runtimeState
                        messages = d.messages
                        pageEarliest = d.snapshotEarliestCommitId
                        pageHasMore = d.snapshotHasMore
                        config = d.config
                    }

            conversations <- conversations.Add(d.conversationId, view)
            latestCommitId <- max latestCommitId d.lastCommitId
            convChanged.Trigger d.conversationId
        | HistoryPage d ->
            // Q127 分页：把更早历史按 commitId 升序前置拼接（去重）
            match conversations.TryFind d.conversationId with
            | Some v ->
                let combined, earliest = CommitTimeline.prependOlder d.items v.messages
                v.messages <- combined
                earliest |> Option.iter (fun c -> v.pageEarliest <- c)
                v.pageHasMore <- d.hasMore
                convChanged.Trigger d.conversationId
            | None -> ()
        | MessageCommitted d ->
            match conversations.TryFind d.conversationId with
            | Some v ->
                // commitId 是消息的唯一标识：重复推送、快照与 catch-up 交叠时
                // 必须按它去重，否则同一条回复会在界面上出现两次。
                if not (CommitTimeline.contains d.commitId v.messages) then
                    let o = System.Text.Json.Nodes.JsonObject()
                    o["commitId"] <- d.commitId

                    o["committedAt"] <-
                        d.committedAt.UtcDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture)

                    o["payload"] <- d.payload.DeepClone()
                    v.messages.Add o

                v.lastCommitId <- max v.lastCommitId d.commitId
                latestCommitId <- max latestCommitId d.commitId
                convChanged.Trigger d.conversationId
            | None -> ()
        | ConversationUpdated d ->
            latestCommitId <- max latestCommitId d.commitId

            match conversations.TryFind d.conversationId with
            | Some v ->
                if not (isNull d.change) then
                    let mutable delNode: System.Text.Json.Nodes.JsonNode = null

                    if d.change.TryGetPropertyValue("deletedMessage", &delNode) && not (isNull delNode) then
                        v.messages <- CommitTimeline.without (delNode.GetValue<uint64>()) v.messages

                    let mutable titleNode: System.Text.Json.Nodes.JsonNode = null

                    if d.change.TryGetPropertyValue("title", &titleNode) && not (isNull titleNode) then
                        v.title <- titleNode.GetValue<string>()
                // 水位只能单调上升：generation 状态变化推来的 conversation.updated
                // 携带 commitId = 0，直接赋值会把水位清零，
                // 于是 catch-up 把整段历史当成未应用重放一遍，界面出现重复消息。
                v.lastCommitId <- max v.lastCommitId d.commitId
                convChanged.Trigger d.conversationId
            | None -> ()
        | AuthorityCatchUp d ->
            // 慢客户端追赶（决策 32-34）：应用批次中的权威提交，然后推进游标
            let mutable maxApplied = d.fromCursor

            for item in d.items do
                match item with
                | :? System.Text.Json.Nodes.JsonObject as obj ->
                    let evType =
                        if obj.ContainsKey "type" && obj["type"] <> null then
                            obj["type"].ToString().Trim('"')
                        else
                            ""

                    if evType = "message.committed" || evType = "chat.message.committed" then
                        match obj["payload"] with
                        | :? System.Text.Json.Nodes.JsonObject as p ->
                            let cidStr =
                                if p.ContainsKey "conversationId" && p["conversationId"] <> null then
                                    p["conversationId"].ToString().Trim('"')
                                else
                                    ""

                            match Guid.TryParse cidStr with
                            | true, cid when conversations.ContainsKey cid ->
                                let v = conversations[cid]

                                let msgNode =
                                    if p.ContainsKey "message" && p["message"] <> null then
                                        p["message"].DeepClone()
                                    else
                                        obj.DeepClone()

                                v.messages.Add msgNode
                                convChanged.Trigger cid
                            | _ -> ()
                        | _ -> ()
                    else
                        match WireCodec.tryDecode (obj.ToJsonString()) with
                        | Ok ev -> this.Handle ev
                        | _ -> ()
                | _ ->
                    let commitOpt =
                        match item with
                        | :? System.Text.Json.Nodes.JsonValue as jv ->
                            match jv.TryGetValue<string>() with
                            | true, s -> CommitCodec.tryCommitFromJsonLine s
                            | _ -> None
                        | :? System.Text.Json.Nodes.JsonObject as obj ->
                            CommitCodec.tryCommitFromJsonLine (obj.ToJsonString())
                        | _ -> None

                    match commitOpt with
                    | Some commit ->
                        for ev in commit.events do
                            match ev with
                            | AgentMessageRecorded m when conversations.ContainsKey m.conversationId ->
                                match conversations.TryFind m.conversationId with
                                | Some v ->
                                    // 按 commitId 精确去重：水位比较不足以防重放
                                    if not (CommitTimeline.contains commit.id v.messages) then
                                        let o = System.Text.Json.Nodes.JsonObject()
                                        o["commitId"] <- commit.id

                                        o["committedAt"] <-
                                            commit.committedAtUtc.UtcDateTime.ToString(
                                                "o",
                                                System.Globalization.CultureInfo.InvariantCulture
                                            )

                                        o["payload"] <- m.payloadJson.DeepClone()
                                        v.messages.Add o
                                        v.lastCommitId <- max v.lastCommitId commit.id
                                        convChanged.Trigger m.conversationId
                                | _ -> ()
                            | MessageDeleted m when conversations.ContainsKey m.conversationId ->
                                match conversations.TryFind m.conversationId with
                                | Some v ->
                                    v.messages <- CommitTimeline.without m.messageCommitId v.messages
                                    v.lastCommitId <- max v.lastCommitId commit.id
                                    convChanged.Trigger m.conversationId
                                | _ -> ()
                            | _ -> ()

                        maxApplied <- max maxApplied commit.id
                    | None -> ()

            latestCommitId <- max latestCommitId d.toCommitId
            this.AdvanceCursorTo maxApplied
        | GenerationStarted d ->
            match conversations.TryFind d.conversationId with
            | Some v ->
                v.runtimeState <- "generating"
                convChanged.Trigger d.conversationId
            | None -> ()
        | GenerationFinished d ->
            match conversations.TryFind d.conversationId with
            | Some v ->
                v.runtimeState <- "idle"
                convChanged.Trigger d.conversationId
            | None -> ()
        | CursorAdvanced d ->
            cursor <- max cursor d.id
            cursorChanged.Trigger cursor
        | _ -> ()

    /// 客户端应用了一批事件后推进游标（决策 33）。
    member this.AdvanceCursor() : unit =
        cursor <- latestCommitId
        cursorChanged.Trigger cursor

    /// catch-up 批次应用完成后推进游标：只确认实际应用的 commit（不含被主机过滤的跳号区间）。
    member this.AdvanceCursorTo(applied: CommitId) : unit =
        cursor <- max cursor applied
        cursorChanged.Trigger cursor

    /// 生成要发送给主机的游标确认事件。
    member this.CursorAdvancedEvent() : WireEvent = CursorAdvanced {| id = cursor |}
