namespace Wanxiang.UI

open System
open System.Text.Json.Nodes
open Wanxiang.Core

// 组合者：发件箱（Wire 的 ClientCommand + ComposerDrafts 的附件草稿）、按命令登记的确认台账（CommandFeedback）、草稿内存 ⇄ 快照（DraftStore）。

type DeliveryState =
    | PreparingConversation
    | SendingMessage
    | QueuedMessage
    | UnconfirmedMessage of string
    | RejectedMessage of string

type PendingMessage =
    {
        instanceId: string
        conversationId: Guid
        command: ClientCommand
        creation: ClientCommand option
        creationConfirmed: bool
        text: string
        attachments: PendingAttachment list
        state: DeliveryState
        lastAttemptAt: DateTimeOffset
    }

module PendingMessage =
    let invocationId item = ClientCommand.invocationId item.command

    let canRetry item =
        match item.state with
        | UnconfirmedMessage _
        | RejectedMessage _ -> true
        | _ -> false

    let status item =
        match item.state with
        | PreparingConversation -> "正在准备对话…"
        | SendingMessage -> "正在发送，等待保存确认…"
        | QueuedMessage -> "已排队，尚未保存"
        | UnconfirmedMessage reason -> "尚未确认保存：" + reason
        | RejectedMessage reason -> "未发送：" + reason

/// 输入框只把内容交给这里；直到 command.committed 才释放。
/// 重试始终使用原命令、原 invocationId 和原附件引用，不猜测主机是否已写入。
type MessageOutbox() =
    let mutable items: Map<Guid, PendingMessage> = Map.empty

    member _.AllInstances() =
        items |> Map.toList |> List.map (fun (_, i) -> i.instanceId) |> List.distinct

    member _.ForInstance(instanceId: string) =
        items
        |> Map.toList
        |> List.map snd
        |> List.filter (fun item -> item.instanceId = instanceId)

    member _.Items(instanceId: string, conversationId: Guid option) =
        items
        |> Map.toList
        |> List.map snd
        |> List.filter (fun item -> item.instanceId = instanceId && Some item.conversationId = conversationId)

    member _.TryFind(id: Guid) = items.TryFind id
    member _.Count = items.Count

    static member private BuildMessage(text: string, attachments: PendingAttachment list) =
        let contents = JsonArray()

        if not (String.IsNullOrWhiteSpace text) then
            let node = JsonObject()
            node["text"] <- text
            contents.Add node

        for attachment in attachments do
            let node = JsonObject()
            node["type"] <- "attachment"
            node["sha256"] <- attachment.sha256
            node["size"] <- attachment.size
            node["mediaType"] <- attachment.mediaType
            node["fileName"] <- attachment.fileName
            contents.Add node

        let message = JsonObject()
        message["role"] <- "user"
        message["contents"] <- contents
        message

    member _.Stage
        (
            instanceId: string,
            conversationId: Guid,
            text: string,
            attachments: PendingAttachment list,
            creation: ClientCommand option
        ) =
        let message = MessageOutbox.BuildMessage(text, attachments)
        let id = Guid.CreateVersion7()

        let item =
            {
                instanceId = instanceId
                conversationId = conversationId
                command =
                    SendUserMessage
                        {|
                            invocationId = id
                            conversationId = conversationId
                            messageJson = message
                        |}
                creation = creation
                creationConfirmed = creation.IsNone
                text = text
                attachments = attachments
                state =
                    (if creation.IsSome then
                         PreparingConversation
                     else
                         SendingMessage)
                lastAttemptAt = DateTimeOffset.UtcNow
            }

        items <- items.Add(id, item)
        item

    /// 重新打开后从本地恢复一条未确认的发送（W5，决策 80）：沿用**原 invocationId** 与原附件引用，
    /// 重试时主机按幂等核对，不会重复保存；状态恒为「未确认」，由用户显式点重试——
    /// 主机从不把排队当事实（原则 2），客户端也不自动重发（原则 5）。
    /// 只恢复对话已确认创建的消息；已在 outbox 的同 id 条目不被覆盖。
    member _.Restore
        (
            instanceId: string,
            conversationId: Guid,
            invocationId: Guid,
            text: string,
            attachments: PendingAttachment list,
            reason: string
        ) =
        if items.ContainsKey invocationId then
            items.[invocationId]
        else
            let message = MessageOutbox.BuildMessage(text, attachments)

            let item =
                {
                    instanceId = instanceId
                    conversationId = conversationId
                    command =
                        SendUserMessage
                            {|
                                invocationId = invocationId
                                conversationId = conversationId
                                messageJson = message
                            |}
                    creation = None
                    creationConfirmed = true
                    text = text
                    attachments = attachments
                    state = UnconfirmedMessage reason
                    lastAttemptAt = DateTimeOffset.UtcNow
                }

            items <- items.Add(invocationId, item)
            item

    /// 对话被删除：它名下的未发送消息已无处可发（本地丢弃，主机从未知道它们）。
    member _.DiscardConversation(instanceId: string, conversationId: Guid) =
        items <-
            items
            |> Map.filter (fun _ item -> not (item.instanceId = instanceId && item.conversationId = conversationId))

    /// 清除本地草稿时一并丢弃本实例所有未确认的发送（仅限「未确认/被拒」，进行中的不动）。
    member _.DiscardUnsent(instanceId: string) =
        items <-
            items
            |> Map.filter (fun _ item -> not (item.instanceId = instanceId && PendingMessage.canRetry item))

    member _.ApplyState(id: Guid, state: DeliveryState) =
        items <-
            items
            |> Map.change
                id
                (Option.map (fun item ->
                    { item with
                        state = state
                        lastAttemptAt =
                            match state with
                            | PreparingConversation
                            | SendingMessage -> DateTimeOffset.UtcNow
                            | _ -> item.lastAttemptAt
                    }))

    member _.Expire(now: DateTimeOffset, timeout: TimeSpan) =
        let mutable changed = false

        items <-
            items
            |> Map.map (fun _ item ->
                match item.state with
                | PreparingConversation
                | SendingMessage when now - item.lastAttemptAt >= timeout ->
                    changed <- true

                    { item with
                        state = UnconfirmedMessage "暂未收到确认；内容已保留，可重试核对。"
                    }
                | _ -> item)

        changed

    member _.ConversationReady(instanceId: string, conversationId: Guid) =
        items <-
            items
            |> Map.map (fun _ item ->
                if item.instanceId = instanceId && item.conversationId = conversationId then
                    { item with creationConfirmed = true }
                else
                    item)

    member this.Accept(id: Guid) =
        if items.ContainsKey id then
            this.ApplyState(id, QueuedMessage)

    member _.Commit(id: Guid) = items <- items.Remove id

    member _.RejectCreation(id: Guid, reason: string) =
        items <-
            items
            |> Map.map (fun _ item ->
                if
                    item.creation
                    |> Option.exists (fun command -> ClientCommand.invocationId command = id)
                then
                    { item with
                        state = RejectedMessage reason
                    }
                else
                    item)

    member _.Disconnect(instanceId: string) =
        items <-
            items
            |> Map.map (fun _ item ->
                if item.instanceId = instanceId then
                    match item.state with
                    | PreparingConversation
                    | SendingMessage
                    | QueuedMessage
                    | UnconfirmedMessage _ ->
                        { item with
                            state = UnconfirmedMessage "连接已断开；重试会核对原命令，不会重复保存。"
                        }
                    | RejectedMessage _ -> item
                else
                    item)

/// 由命令得出 invocationId 与「能否在这里超时」：消息投递类命令由发件箱自己的 30 秒确认超时负责，这里不重复判定。
module CommandExpiry =
    let expirable (cmd: ClientCommand) =
        match cmd with
        | SendUserMessage _
        | CreateConversation _
        | ForkConversation _ -> false
        | _ -> true

/// 旧调用方按命令登记：转成 CommandFeedback 库的按 id 登记。
[<AutoOpen>]
module CommandFeedbackCommands =
    type CommandFeedbackTracker with
        member this.Track(cmd: ClientCommand, successMessage: string option, onCommitted: unit -> unit) =
            this.TrackId(ClientCommand.invocationId cmd, CommandExpiry.expirable cmd, successMessage, onCommitted)

        member this.TrackWithCompletion
            (cmd: ClientCommand, successMessage: string option, onCommitted: unit -> unit, onCompleted: bool -> unit)
            =
            this.TrackIdWithCompletion(
                ClientCommand.invocationId cmd,
                CommandExpiry.expirable cmd,
                successMessage,
                onCommitted,
                onCompleted
            )

/// 草稿内存状态 ⇄ 可落盘快照的转换。快照之间的合并在 DraftStore 库的同名模块。
module DraftPolicy =

    let private refOf (a: PendingAttachment) : DraftAttachmentRef =
        {
            fileName = a.fileName
            mediaType = a.mediaType
            size = a.size
            sha256 = a.sha256
        }

    /// 恢复出来的引用：失败残留的形状（占 chip、阻塞发送、可移除），并登记为「需重新选择」。
    let private pendingOf (r: DraftAttachmentRef) : PendingAttachment =
        let id = Guid.NewGuid()
        RestoredAttachments.mark id

        {
            attachmentId = id
            sha256 = r.sha256
            size = r.size
            mediaType = r.mediaType
            fileName = r.fileName
            ready = false
            failed = true
        }

    /// 当前内存 → 快照。
    /// - 草稿：文字或附件非空才存；附件只存「已就绪」与「已是恢复引用」的（上传中/失败的字节本机没有意义）。
    /// - 未发送：outbox 中「未确认/被拒/排队/发送中」且对话已确认创建的，存为未确认；
    ///   对话尚未确认创建的，其文字折回该主机的起步草稿（对话在主机并不存在，没有可重试的目标）。
    let snapshotOf
        (drafts: ComposerDrafts)
        (outbox: MessageOutbox)
        (savedAtOf: Guid -> DateTimeOffset)
        : DraftSnapshot =
        let draftRows =
            [
                for (instanceId, conversationId), draft in drafts.Entries do
                    if not (String.IsNullOrEmpty instanceId) then
                        let refs =
                            [
                                for a in draft.attachments.Items do
                                    if
                                        (a.ready || RestoredAttachments.isRestored a.attachmentId)
                                        && not (String.IsNullOrEmpty a.sha256)
                                    then
                                        yield refOf a
                            ]

                        if not (String.IsNullOrWhiteSpace draft.text) || not (List.isEmpty refs) then
                            yield
                                {
                                    instanceId = instanceId
                                    conversationId = conversationId
                                    text = draft.text
                                    attachments = refs
                                }
            ]

        let items =
            [
                for instanceId in (outbox.AllInstances()) do
                    yield! outbox.ForInstance instanceId
            ]
            |> List.filter (fun item -> item.state <> PreparingConversation || item.creationConfirmed)

        let confirmed = items |> List.filter _.creationConfirmed
        let orphaned = items |> List.filter (fun item -> not item.creationConfirmed)

        let unsent =
            [
                for item in confirmed do
                    yield
                        {
                            instanceId = item.instanceId
                            conversationId = item.conversationId
                            invocationId = PendingMessage.invocationId item
                            text = item.text
                            attachments = item.attachments |> List.map refOf
                            savedAt = savedAtOf (PendingMessage.invocationId item)
                        }
            ]
        // 未确认创建的对话：折回起步草稿
        let foldedBack =
            orphaned
            |> List.groupBy _.instanceId
            |> List.map (fun (instanceId, group) ->
                let text =
                    group
                    |> List.map _.text
                    |> List.filter (fun t -> not (String.IsNullOrWhiteSpace t))
                    |> String.concat "\n\n"

                instanceId, text, group |> List.collect (fun i -> i.attachments |> List.map refOf))

        let merged =
            foldedBack
            |> List.fold
                (fun (rows: StoredDraft list) (instanceId, text, refs) ->
                    match
                        rows
                        |> List.tryFind (fun r -> r.instanceId = instanceId && r.conversationId.IsNone)
                    with
                    | Some existing ->
                        let joined =
                            [ existing.text; text ]
                            |> List.filter (fun t -> not (String.IsNullOrWhiteSpace t))
                            |> String.concat "\n\n"

                        rows
                        |> List.map (fun r ->
                            if obj.ReferenceEquals(r, existing) then
                                { r with
                                    text = joined
                                    attachments = r.attachments @ refs
                                }
                            else
                                r)
                    | None ->
                        if String.IsNullOrWhiteSpace text && List.isEmpty refs then
                            rows
                        else
                            rows
                            @ [
                                {
                                    instanceId = instanceId
                                    conversationId = None
                                    text = text
                                    attachments = refs
                                }
                            ])
                draftRows

        { drafts = merged; unsent = unsent }

    /// 快照 → 内存。只恢复指定主机的内容；已有内容（用户这次已经输入的）不被覆盖。
    /// 返回恢复的未确认发送条数（用于提示）。
    let restoreInto
        (snapshot: DraftSnapshot)
        (instanceId: string)
        (drafts: ComposerDrafts)
        (outbox: MessageOutbox)
        : int =
        for d in snapshot.drafts do
            if d.instanceId = instanceId then
                let live = drafts.Get(instanceId, d.conversationId)

                if String.IsNullOrWhiteSpace live.text then
                    live.text <- d.text

                live.attachments.RestoreReferences(d.attachments |> List.map pendingOf)
                |> ignore

        let mutable restored = 0

        for u in snapshot.unsent do
            if u.instanceId = instanceId && (outbox.TryFind u.invocationId).IsNone then
                let attachments =
                    u.attachments
                    |> List.map (fun r ->
                        {
                            attachmentId = Guid.NewGuid()
                            sha256 = r.sha256
                            size = r.size
                            mediaType = r.mediaType
                            fileName = r.fileName
                            ready = true
                            failed = false
                        })

                outbox.Restore(
                    instanceId,
                    u.conversationId,
                    u.invocationId,
                    u.text,
                    attachments,
                    "上次关闭前未确认保存；可重试，不会重复保存。"
                )
                |> ignore

                restored <- restored + 1

        restored
