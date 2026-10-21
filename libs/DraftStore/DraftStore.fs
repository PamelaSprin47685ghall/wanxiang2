namespace Wanxiang.UI

open System
open System.Text.Json
open System.Text.Json.Nodes

/// 本地草稿与未发送消息（W5，决策 80/81）。
///
/// 这是**客户端本机**的便利存储，不是业务数据：
/// - 不入事件日志、不上传、不同步（决策 195：本机客户端偏好的同一类）；
/// - 按「主机 instanceId + 对话 id」索引，切换主机互不泄漏；
/// - 只存文字与附件引用（名称/类型/大小/sha256），**不存附件字节、不存令牌或密钥**；
/// - 桌面写 `$WANXIANG_HOME/drafts.json`（与 client.toml、ui.json 同目录，仅当前用户可读写）；
///   PWA 写 IndexedDB；
/// - 用户可以随时关闭（立即删除已存内容）或一键清除。
///
/// 本库只管快照 ⇄ 文本与「该不该写」；落点（文件 / IndexedDB）由宿主按返回值去写。
type DraftAttachmentRef =
    {
        fileName: string
        mediaType: string
        size: int64
        sha256: string
    }

/// 一份未发送的输入。conversationId = None 表示还没有对话的起步输入。
type StoredDraft =
    {
        instanceId: string
        conversationId: Guid option
        text: string
        attachments: DraftAttachmentRef list
    }

/// 一条未被主机确认保存的发送。保留原 invocationId，重试时由主机幂等核对。
type StoredUnsent =
    {
        instanceId: string
        conversationId: Guid
        invocationId: Guid
        text: string
        attachments: DraftAttachmentRef list
        savedAt: DateTimeOffset
    }

type DraftSnapshot =
    {
        drafts: StoredDraft list
        unsent: StoredUnsent list
    }

module DraftSnapshot =
    let empty = { drafts = []; unsent = [] }

    let isEmpty (s: DraftSnapshot) =
        List.isEmpty s.drafts && List.isEmpty s.unsent

module DraftCodec =

    /// 文件外壳版本：新增字段只加不改，旧版读到未知字段忽略；读到更高版本整份忽略（不覆盖、不报错）。
    let formatVersion = 1

    let private refToJson (r: DraftAttachmentRef) =
        let o = JsonObject()
        o["fileName"] <- r.fileName
        o["mediaType"] <- r.mediaType
        o["size"] <- r.size
        o["sha256"] <- r.sha256
        o

    let serialize (snapshot: DraftSnapshot) : string =
        let root = JsonObject()
        root["version"] <- formatVersion
        let drafts = JsonArray()

        for d in snapshot.drafts do
            let o = JsonObject()
            o["instanceId"] <- d.instanceId

            (match d.conversationId with
             | Some id -> o["conversationId"] <- id.ToString "D"
             | None -> ())

            o["text"] <- d.text
            o["attachments"] <- JsonArray([| for r in d.attachments -> refToJson r :> JsonNode |])
            drafts.Add o

        root["drafts"] <- drafts
        let unsent = JsonArray()

        for u in snapshot.unsent do
            let o = JsonObject()
            o["instanceId"] <- u.instanceId
            o["conversationId"] <- u.conversationId.ToString "D"
            o["invocationId"] <- u.invocationId.ToString "D"
            o["text"] <- u.text
            o["attachments"] <- JsonArray([| for r in u.attachments -> refToJson r :> JsonNode |])
            o["savedAt"] <- u.savedAt.ToString "O"
            unsent.Add o

        root["unsent"] <- unsent

        root.ToJsonString(
            JsonSerializerOptions(
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            )
        )

    let private str (o: JsonObject) key =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue(key, &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.String
        then
            Some(n.GetValue<string>())
        else
            None

    let private guid (o: JsonObject) key =
        str o key
        |> Option.bind (fun s ->
            match Guid.TryParse s with
            | true, g -> Some g
            | _ -> None)

    let private refs (o: JsonObject) =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue("attachments", &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.Array
        then
            [
                for item in n.AsArray() do
                    if not (isNull item) && item.GetValueKind() = JsonValueKind.Object then
                        let a = item.AsObject()

                        let size =
                            let mutable s: JsonNode = null

                            if
                                a.TryGetPropertyValue("size", &s)
                                && not (isNull s)
                                && s.GetValueKind() = JsonValueKind.Number
                            then
                                s.GetValue<int64>()
                            else
                                0L

                        yield
                            {
                                fileName = str a "fileName" |> Option.defaultValue ""
                                mediaType = str a "mediaType" |> Option.defaultValue "application/octet-stream"
                                size = size
                                sha256 = str a "sha256" |> Option.defaultValue ""
                            }
            ]
        else
            []

    /// 宽容读取：任何损坏、未知版本、字段缺失都退化为「少恢复一些」，绝不抛出、绝不阻塞启动。
    let tryParse (text: string) : DraftSnapshot =
        try
            match JsonNode.Parse text with
            | null -> DraftSnapshot.empty
            | node when node.GetValueKind() <> JsonValueKind.Object -> DraftSnapshot.empty
            | node ->
                let root = node.AsObject()

                let version =
                    let mutable v: JsonNode = null

                    if
                        root.TryGetPropertyValue("version", &v)
                        && not (isNull v)
                        && v.GetValueKind() = JsonValueKind.Number
                    then
                        v.GetValue<int>()
                    else
                        0

                if version < 1 || version > formatVersion then
                    DraftSnapshot.empty
                else
                    let items key =
                        let mutable n: JsonNode = null

                        if
                            root.TryGetPropertyValue(key, &n)
                            && not (isNull n)
                            && n.GetValueKind() = JsonValueKind.Array
                        then
                            [
                                for item in n.AsArray() do
                                    if not (isNull item) && item.GetValueKind() = JsonValueKind.Object then
                                        yield item.AsObject()
                            ]
                        else
                            []

                    {
                        drafts =
                            [
                                for o in items "drafts" do
                                    match str o "instanceId" with
                                    | Some instanceId ->
                                        yield
                                            {
                                                instanceId = instanceId
                                                conversationId = guid o "conversationId"
                                                text = str o "text" |> Option.defaultValue ""
                                                attachments = refs o
                                            }
                                    | None -> ()
                            ]
                        unsent =
                            [
                                for o in items "unsent" do
                                    match str o "instanceId", guid o "conversationId", guid o "invocationId" with
                                    | Some instanceId, Some conversationId, Some invocationId ->
                                        yield
                                            {
                                                instanceId = instanceId
                                                conversationId = conversationId
                                                invocationId = invocationId
                                                text = str o "text" |> Option.defaultValue ""
                                                attachments = refs o
                                                savedAt =
                                                    match
                                                        str o "savedAt"
                                                        |> Option.map (fun s -> DateTimeOffset.TryParse s)
                                                    with
                                                    | Some(true, t) -> t
                                                    | _ -> DateTimeOffset.UtcNow
                                            }
                                    | _ -> ()
                            ]
                    }
        with _ ->
            DraftSnapshot.empty

/// 一次保存该对落点做什么。
[<RequireQualifiedAccess>]
type DraftWrite =
    /// 与上次落盘内容相同，不写
    | Unchanged
    /// 写入这段文本
    | Write of text: string
    /// 快照为空：删除已存内容，落点不留空壳
    | Delete

/// 草稿落盘的去重闸门：记住上次落盘的文本，内容没变就不写。是否启用由宿主判断，关闭时不调用。
type DraftStore() =
    let mutable lastSaved: string option = None

    /// 宿主读到的已存文本 → 快照；同时记下它，作为「上次落盘」。
    member _.Loaded(text: string option) : DraftSnapshot =
        match text with
        | Some t ->
            lastSaved <- Some t
            DraftCodec.tryParse t
        | None -> DraftSnapshot.empty

    /// 内容没变就不写；空快照等于删除（之前没写过就什么都不做）。
    member _.Save(snapshot: DraftSnapshot) : DraftWrite =
        if DraftSnapshot.isEmpty snapshot then
            if lastSaved.IsSome then
                lastSaved <- None
                DraftWrite.Delete
            else
                DraftWrite.Unchanged
        else
            let text = DraftCodec.serialize snapshot

            if lastSaved <> Some text then
                lastSaved <- Some text
                DraftWrite.Write text
            else
                DraftWrite.Unchanged

    /// 宿主已删除落点内容（设置里的显式清除、关闭保存时）。
    member _.Cleared() = lastSaved <- None

/// 快照之间的纯函数（内存 ⇄ 快照的转换依赖输入框与发件箱，留在使用方）。
module DraftPolicy =

    /// 落盘用的合并：内存是「已恢复主机」的唯一事实；还没连接过的其他主机，其已存草稿原样保留，
    /// 不能因为这次没打开它就被一次保存抹掉。
    let mergeForSave (stored: DraftSnapshot) (restoredInstances: Set<string>) (current: DraftSnapshot) : DraftSnapshot =
        {
            drafts =
                (stored.drafts
                 |> List.filter (fun d -> not (restoredInstances.Contains d.instanceId)))
                @ current.drafts
            unsent =
                (stored.unsent
                 |> List.filter (fun u -> not (restoredInstances.Contains u.instanceId)))
                @ current.unsent
        }

    /// 已被删除的对话：曾经存在（known）而最新列表里没有，且没有在途发送。返回应清理的对话 id。
    let deletedConversations (listed: Set<Guid>) (known: Set<Guid>) (inFlight: Set<Guid>) : Set<Guid> =
        known
        |> Set.filter (fun id -> not (listed.Contains id) && not (inFlight.Contains id))

/// 连上主机后把本机保存的草稿放回、列表变化后清理已删对话、设置页摘要，迁自旧 AppShell 的
/// RestoreLocalDrafts / PruneDeletedConversationDrafts / UpdateDraftSummary / ClearLocalDrafts 里不碰界面的部分。
module DraftRestore =

    let private hasContent (d: StoredDraft) =
        not (String.IsNullOrWhiteSpace d.text) || not (List.isEmpty d.attachments)

    /// 已存内容里这台主机出现过的对话（用来判断以后哪些对话被删了）。
    let knownConversations (stored: DraftSnapshot) (instanceId: string) : Set<Guid> =
        set
            [
                for d in stored.drafts do
                    if d.instanceId = instanceId then
                        match d.conversationId with
                        | Some id -> yield id
                        | None -> ()
                for u in stored.unsent do
                    if u.instanceId = instanceId then
                        yield u.conversationId
            ]

    /// 放回后给用户的一句话；什么都没放回时为 None。unsentRestored 是这次放回发件箱的未确认条数。
    let restoreNotice (stored: DraftSnapshot) (instanceId: string) (unsentRestored: int) : string option =
        let hasDrafts =
            stored.drafts
            |> List.exists (fun d -> d.instanceId = instanceId && hasContent d)

        if hasDrafts || unsentRestored > 0 then
            let needsReselect =
                stored.drafts
                |> List.exists (fun d -> d.instanceId = instanceId && not (List.isEmpty d.attachments))

            let message =
                if unsentRestored > 0 then
                    "已恢复上次未发送的内容；未确认的消息可重试，不会重复保存。"
                else
                    "已恢复上次未发送的草稿。"

            Some(if needsReselect then message + "附件需要重新选择。" else message)
        else
            None

    /// 输入框文字为空、而这次放回的草稿有字时才填进输入框，不覆盖用户这次已经输入的文字。
    let fillComposer (composerText: string) (restoredText: string) =
        String.IsNullOrWhiteSpace composerText
        && not (String.IsNullOrWhiteSpace restoredText)

    /// 设置页摘要用的两个数：有内容的草稿条数、未确认消息条数。
    let summaryCounts (snapshot: DraftSnapshot) : int * int =
        (snapshot.drafts |> List.filter hasContent |> List.length), List.length snapshot.unsent

    /// 已删对话（DraftPolicy.deletedConversations 的结果）从已存内容里去掉。
    let pruneStored (stored: DraftSnapshot) (instanceId: string) (gone: Set<Guid>) : DraftSnapshot =
        {
            drafts =
                stored.drafts
                |> List.filter (fun d ->
                    not (d.instanceId = instanceId && d.conversationId |> Option.exists gone.Contains))
            unsent =
                stored.unsent
                |> List.filter (fun u -> not (u.instanceId = instanceId && gone.Contains u.conversationId))
        }

    /// 清理后仍认得的对话：去掉已删的，加上这次列表里的。
    let knownAfterPrune (known: Set<Guid>) (gone: Set<Guid>) (listed: Set<Guid>) =
        Set.union (Set.difference known gone) listed

    let clearedText = "本地草稿已清除。"
