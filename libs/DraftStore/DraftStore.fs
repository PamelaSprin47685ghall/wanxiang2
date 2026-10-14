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
type DraftAttachmentRef = {
    fileName: string
    mediaType: string
    size: int64
    sha256: string
}

/// 一份未发送的输入。conversationId = None 表示还没有对话的起步输入。
type StoredDraft = {
    instanceId: string
    conversationId: Guid option
    text: string
    attachments: DraftAttachmentRef list
}

/// 一条未被主机确认保存的发送。保留原 invocationId，重试时由主机幂等核对。
type StoredUnsent = {
    instanceId: string
    conversationId: Guid
    invocationId: Guid
    text: string
    attachments: DraftAttachmentRef list
    savedAt: DateTimeOffset
}

type DraftSnapshot = {
    drafts: StoredDraft list
    unsent: StoredUnsent list
}

module DraftSnapshot =
    let empty = { drafts = []; unsent = [] }
    let isEmpty (s: DraftSnapshot) = List.isEmpty s.drafts && List.isEmpty s.unsent

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
        root.ToJsonString(JsonSerializerOptions(WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping))

    let private str (o: JsonObject) key =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.String then Some(n.GetValue<string>()) else None

    let private guid (o: JsonObject) key =
        str o key |> Option.bind (fun s -> match Guid.TryParse s with | true, g -> Some g | _ -> None)

    let private refs (o: JsonObject) =
        let mutable n: JsonNode = null
        if o.TryGetPropertyValue("attachments", &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Array then
            [ for item in n.AsArray() do
                  if not (isNull item) && item.GetValueKind() = JsonValueKind.Object then
                      let a = item.AsObject()
                      let size =
                          let mutable s: JsonNode = null
                          if a.TryGetPropertyValue("size", &s) && not (isNull s) && s.GetValueKind() = JsonValueKind.Number then s.GetValue<int64>() else 0L
                      yield
                          { fileName = str a "fileName" |> Option.defaultValue ""
                            mediaType = str a "mediaType" |> Option.defaultValue "application/octet-stream"
                            size = size
                            sha256 = str a "sha256" |> Option.defaultValue "" } ]
        else []

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
                    if root.TryGetPropertyValue("version", &v) && not (isNull v) && v.GetValueKind() = JsonValueKind.Number then v.GetValue<int>() else 0
                if version < 1 || version > formatVersion then DraftSnapshot.empty
                else
                    let items key =
                        let mutable n: JsonNode = null
                        if root.TryGetPropertyValue(key, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Array then
                            [ for item in n.AsArray() do
                                  if not (isNull item) && item.GetValueKind() = JsonValueKind.Object then yield item.AsObject() ]
                        else []
                    { drafts =
                        [ for o in items "drafts" do
                              match str o "instanceId" with
                              | Some instanceId ->
                                  yield
                                      { instanceId = instanceId
                                        conversationId = guid o "conversationId"
                                        text = str o "text" |> Option.defaultValue ""
                                        attachments = refs o }
                              | None -> () ]
                      unsent =
                        [ for o in items "unsent" do
                              match str o "instanceId", guid o "conversationId", guid o "invocationId" with
                              | Some instanceId, Some conversationId, Some invocationId ->
                                  yield
                                      { instanceId = instanceId
                                        conversationId = conversationId
                                        invocationId = invocationId
                                        text = str o "text" |> Option.defaultValue ""
                                        attachments = refs o
                                        savedAt =
                                            match str o "savedAt" |> Option.map (fun s -> DateTimeOffset.TryParse s) with
                                            | Some(true, t) -> t
                                            | _ -> DateTimeOffset.UtcNow }
                              | _ -> () ] }
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
            else DraftWrite.Unchanged
        else
            let text = DraftCodec.serialize snapshot
            if lastSaved <> Some text then
                lastSaved <- Some text
                DraftWrite.Write text
            else DraftWrite.Unchanged

    /// 宿主已删除落点内容（设置里的显式清除、关闭保存时）。
    member _.Cleared() = lastSaved <- None

/// 快照之间的纯函数（内存 ⇄ 快照的转换依赖输入框与发件箱，留在使用方）。
module DraftPolicy =

    /// 落盘用的合并：内存是「已恢复主机」的唯一事实；还没连接过的其他主机，其已存草稿原样保留，
    /// 不能因为这次没打开它就被一次保存抹掉。
    let mergeForSave (stored: DraftSnapshot) (restoredInstances: Set<string>) (current: DraftSnapshot) : DraftSnapshot =
        { drafts = (stored.drafts |> List.filter (fun d -> not (restoredInstances.Contains d.instanceId))) @ current.drafts
          unsent = (stored.unsent |> List.filter (fun u -> not (restoredInstances.Contains u.instanceId))) @ current.unsent }

    /// 已被删除的对话：曾经存在（known）而最新列表里没有，且没有在途发送。返回应清理的对话 id。
    let deletedConversations (listed: Set<Guid>) (known: Set<Guid>) (inFlight: Set<Guid>) : Set<Guid> =
        known |> Set.filter (fun id -> not (listed.Contains id) && not (inFlight.Contains id))
