namespace Wanxiang.UI

open System
open System.Collections.Generic

/// Composer 中的一条附件草稿。用 attachmentId 而不是 sha256 作为身份：
/// 同一文件被选择两次时，两条草稿仍能独立上传、删除和完成。
type PendingAttachment =
    {
        attachmentId: Guid
        sha256: string
        size: int64
        mediaType: string
        fileName: string
        /// 上传中时为 false
        ready: bool
        /// 上传失败后保留的可见残留：仍占一条 chip，可移除、可重新添加重试；发送前必须先清理。
        failed: bool
    }

/// 重新打开后从本地草稿恢复出来的附件引用（W5，决策 81）：客户端无法确认文件仍在，
/// 只保留名称与大小，必须由用户重新选择；同样占一条 chip、阻塞发送、可移除。
/// 用独立登记而不是给 PendingAttachment 加字段：记录形状是既有契约，加字段会让所有构造点同时破坏。
module RestoredAttachments =
    let private ids = System.Collections.Concurrent.ConcurrentDictionary<Guid, unit>()
    let mark (attachmentId: Guid) = ids[attachmentId] <- ()
    let isRestored (attachmentId: Guid) = ids.ContainsKey attachmentId

/// 附件传输所需元数据。协议层完成/失败事件通过 attachmentId 回来。
type AttachmentUpload =
    {
        attachmentId: Guid
        fileName: string
        mediaType: string
        size: int64
        sha256: string
    }

/// 附件草稿状态机。UI 只消费 Items，不再自己同时维护 pending list + upload dictionary。
type AttachmentDraftController() =
    let uploads = Dictionary<Guid, AttachmentUpload>()
    let mutable items: PendingAttachment list = []
    /// 所属对话键，由 ComposerDrafts 在创建/搬移草稿时维护；Complete 透出它，
    /// AppShell 只在它等于当前可见对话时才 toast 上传完成。
    let mutable ownerKey: string * Guid option = "", None

    member _.Items = items
    member _.HasReady = items |> List.exists _.ready

    member _.HasUploading =
        items |> List.exists (fun item -> not item.ready && not item.failed)

    member _.HasFailed = items |> List.exists _.failed

    member _.OwnerKey
        with get () = ownerKey
        and set value = ownerKey <- value

    member _.Begin(upload: AttachmentUpload) =
        uploads[upload.attachmentId] <- upload

        items <-
            items
            @ [
                {
                    attachmentId = upload.attachmentId
                    sha256 = upload.sha256
                    size = upload.size
                    mediaType = upload.mediaType
                    fileName = upload.fileName
                    ready = false
                    failed = false
                }
            ]

        items

    /// 用户从草稿中移除后，底层上传可以自然完成，但不会再重新出现在 Composer。
    member _.Remove(attachmentId: Guid) =
        items <- items |> List.filter (fun item -> item.attachmentId <> attachmentId)
        items

    /// 恢复本地草稿里的附件引用：以「需重新选择」的失败残留呈现，不假装文件还在（W5）。
    member _.RestoreReferences(refs: PendingAttachment list) =
        let known = items |> List.map _.attachmentId |> Set.ofList
        items <- items @ (refs |> List.filter (fun r -> not (known.Contains r.attachmentId)))
        items

    /// 返回上传元数据，以及完成时这条附件是否仍在用户草稿里。
    /// 一并透出所属对话键：调用方只在它等于当前可见对话时才 toast，避免后台对话的完成误报。
    member _.Complete
        (attachmentId: Guid, committedSize: int64)
        : (AttachmentUpload * bool * (string * Guid option)) option =
        match uploads.TryGetValue attachmentId with
        | true, upload ->
            uploads.Remove attachmentId |> ignore
            let mutable stillDrafted = false

            items <-
                items
                |> List.map (fun item ->
                    if item.attachmentId = attachmentId then
                        stillDrafted <- true

                        { item with
                            ready = true
                            failed = false
                            size = committedSize
                        }
                    else
                        item)

            Some(upload, stillDrafted, ownerKey)
        | _ -> None

    /// 失败时保留一条 failed 残留（同槽位 chip 呈现，可移除、可重新添加重试）；相同 sha256 的其它附件不受影响。
    /// 返回上传元数据（fileName 即 toast 用名）、是否仍在草稿里、所属对话键；同一 attachmentId 只报一次。
    member _.Abort(attachmentId: Guid) : (AttachmentUpload * bool * (string * Guid option)) option =
        match uploads.TryGetValue attachmentId with
        | true, upload ->
            uploads.Remove attachmentId |> ignore

            let stillDrafted =
                items |> List.exists (fun item -> item.attachmentId = attachmentId)

            items <-
                items
                |> List.map (fun item ->
                    if item.attachmentId = attachmentId then
                        { item with
                            ready = false
                            failed = true
                        }
                    else
                        item)

            Some(upload, stillDrafted, ownerKey)
        | _ -> None


    /// 断连时把所有 uploading 翻成 failed 残留并返回它们；语义上发送仍被阻塞，
    /// 但用户移除残留后即可恢复，不会因“永远上传中”楔死发送。
    member _.MarkAllUploadingAsFailed() : PendingAttachment list =
        let failedNow = ResizeArray<PendingAttachment>()

        items <-
            items
            |> List.map (fun item ->
                if not item.ready && not item.failed then
                    uploads.Remove item.attachmentId |> ignore

                    let failed =
                        { item with
                            ready = false
                            failed = true
                        }

                    failedNow.Add failed
                    failed
                else
                    item)

        List.ofSeq failedNow

    /// 发送是草稿的原子消费点。只要仍有 uploading，就拒绝消费。
    member _.TryConsumeReady() : PendingAttachment list option =
        if items |> List.exists (fun item -> not item.ready) then
            None
        else
            let consumed = items
            items <- []
            Some consumed

type ComposerDraft =
    {
        mutable text: string
        attachments: AttachmentDraftController
    }

/// 草稿仅在客户端内存中保存，按实例和对话隔离；不构成第二份业务数据库。
type ComposerDrafts() =
    let mutable items: Map<string * Guid option, ComposerDraft> = Map.empty

    member _.Get(instanceId: string, conversationId: Guid option) =
        let key = instanceId, conversationId

        match items.TryFind key with
        | Some draft -> draft
        | None ->
            let draft =
                {
                    text = ""
                    attachments = AttachmentDraftController()
                }

            draft.attachments.OwnerKey <- key
            items <- items.Add(key, draft)
            draft

    member _.Move(instanceId: string, source: Guid option, target: Guid option) =
        match items.TryFind(instanceId, source) with
        | Some draft ->
            draft.attachments.OwnerKey <- (instanceId, target)
            items <- items.Remove((instanceId, source)).Add((instanceId, target), draft)
        | None -> ()

    member _.All = items |> Map.toSeq |> Seq.map snd

    /// 带键枚举，供本地草稿落盘（W5）。
    member _.Entries = items |> Map.toSeq

    /// 删除一份草稿（对话被删除、或用户清除本地草稿时）。
    member _.Remove(instanceId: string, conversationId: Guid option) =
        items <- items.Remove((instanceId, conversationId))
