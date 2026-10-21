namespace Wanxiang.Server

open System
open System.IO
open System.Text.Json.Nodes
open Wanxiang.Core

/// 已提交附件引用。
type AttachmentCommittedRef =
    {
        sha256: string
        size: int64
        mediaType: string
        fileName: string
    }

/// 附件内容寻址存储（决策 71/72）：
/// - SHA-256 作为永久文件标识，路径按哈希分片（attachments/ab/cd/<hash>）；
/// - 上传完成前只存在于临时文件，complete 后校验长度与哈希；
/// - 断线删除未完成上传；首版不做 GC（未来压缩时处理）。
/// 元数据（声明 mediaType/fileName + 嗅探结果）随 blob 落盘 <hash>.meta，供下载回传（Q176/P2-7）。
type AttachmentStore(dataDir: string, maxBytes: int64, ?chunkSizeBytes: int) =

    let maxChunkBytes = chunkSizeBytes |> Option.defaultValue (256 * 1024)
    /// 与旧 DataPaths.attachmentsDir 相同：<data>/attachments
    let attachmentsDir = Path.Combine(dataDir, "attachments")
    let blobs = AttachmentBlobs(attachmentsDir, maxBytes, maxChunkBytes)

    /// 声明的媒体类型和文件名，Complete 时落进元数据
    let declared =
        System.Collections.Concurrent.ConcurrentDictionary<Guid, string * string>()

    let toError (e: BlobError) : WanxiangError =
        match e with
        | BlobError.Invalid m -> ValidationError m
        | BlobError.TooLarge(limit, actual) -> AttachmentTooLarge(limit, actual)
        | BlobError.HashMismatch(expected, actual) -> AttachmentHashMismatch(expected = expected, actual = actual)
        | BlobError.NotActive id -> AttachmentIncomplete id
        | BlobError.Failed m -> Poisoned m

    let metaPath (sha256: string) =
        blobs.PathOf sha256 |> Option.map (fun p -> p + ".meta")

    let writeMeta
        (sha256: string)
        (declaredType: string)
        (sniffed: string option)
        (resolved: string)
        (fileName: string)
        (size: int64)
        =
        match metaPath sha256 with
        | None -> ()
        | Some path ->
            try
                let o = JsonObject()
                o["mediaType"] <- resolved
                o["declaredMediaType"] <- declaredType

                match sniffed with
                | Some s -> o["sniffedMediaType"] <- s
                | None -> ()

                o["fileName"] <- fileName
                o["size"] <- size
                File.WriteAllText(path, o.ToJsonString())
            with e ->
                // O-001/R6：元数据写失败是 DEGRADED——附件本体已落盘，仅下载回退默认类型；必须留证据。
                Stderr.write "attachment-meta-write-failed" [ "sha256", sha256; "message", e.Message; "exception", e ]

    /// 连接断线：作废该连接发起的全部未完成上传（决策 71）。
    member this.AbortByConnection(connectionId: int) : unit = blobs.AbortByOwner connectionId

    member _.Begin
        (connectionId: int, attachmentId: Guid, totalBytes: int64, sha256: string, mediaType: string, fileName: string)
        : Result<unit, WanxiangError> =
        let cleanName = MediaSniff.sanitizeFileName fileName

        match blobs.Begin(connectionId, attachmentId, totalBytes, sha256) with
        | Ok() ->
            declared[attachmentId] <- (mediaType, cleanName)
            Ok()
        | Error e -> Error(toError e)

    member _.AppendChunk(attachmentId: Guid, index: int, dataBase64: string) : Result<unit, WanxiangError> =
        blobs.Append(attachmentId, index, dataBase64) |> Result.mapError toError

    member _.Complete(attachmentId: Guid, declaredSha256: string) : Result<AttachmentCommittedRef, WanxiangError> =
        let mediaType, fileName =
            match declared.TryGetValue attachmentId with
            | true, v -> v
            | _ -> ("", "")

        match blobs.Complete(attachmentId, declaredSha256) with
        | Error e ->
            (match e with
             | BlobError.Invalid _
             | BlobError.HashMismatch _ -> declared.TryRemove attachmentId |> ignore
             | _ -> ())

            Error(toError e)
        | Ok(sha, size, finalPath) ->
            declared.TryRemove attachmentId |> ignore

            try
                // Q176：前 512 字节嗅探；声明值为空或 octet-stream 时采用嗅探结果
                let sniffed =
                    try
                        use fs = new FileStream(finalPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                        let headLen = min 512 (int fs.Length)
                        let head = Array.zeroCreate<byte> headLen

                        if fs.Read(head, 0, headLen) > 0 then
                            MediaSniff.sniff head
                        else
                            None
                    with _ ->
                        None

                let resolved = MediaSniff.resolve mediaType sniffed
                writeMeta sha mediaType sniffed resolved fileName size

                Ok
                    {
                        sha256 = sha
                        size = size
                        mediaType = resolved
                        fileName = fileName
                    }
            with e ->
                Error(Poisoned(sprintf "attachment complete failed: %s" e.Message))

    member _.Abort(attachmentId: Guid, _reason: string) : unit =
        blobs.Abort attachmentId
        declared.TryRemove attachmentId |> ignore

    /// 下载：返回流（不存在返回 None）。
    member _.OpenRead(sha256: string) : (Stream * int64) option = blobs.OpenRead sha256

    member _.Exists(sha256: string) : bool = blobs.Exists sha256

    /// 引用元数据。
    member _.MakeRef(sha256: string, size: int64, mediaType: string, fileName: string) : JsonNode =
        let o = JsonObject()
        o["sha256"] <- sha256
        o["size"] <- size
        o["mediaType"] <- mediaType
        o["fileName"] <- fileName
        o

    /// 按 sha256 读取已落盘元数据（下载回传声明值，Q176/P2-7）。
    member _.Metadata(sha256: string) : (string * string * int64) option =
        match metaPath sha256 with
        | None -> None
        | Some path when File.Exists path ->
            try
                let o = JsonNode.Parse(File.ReadAllText path).AsObject()

                let getStr k =
                    let mutable n: JsonNode = null

                    if
                        o.TryGetPropertyValue(k, &n)
                        && n <> null
                        && n.GetValueKind() = System.Text.Json.JsonValueKind.String
                    then
                        n.GetValue<string>()
                    else
                        ""

                let getInt k =
                    let mutable n: JsonNode = null

                    if
                        o.TryGetPropertyValue(k, &n)
                        && n <> null
                        && n.GetValueKind() = System.Text.Json.JsonValueKind.Number
                    then
                        match n with
                        | :? JsonValue as v ->
                            match v.TryGetValue<int64>() with
                            | true, i -> i
                            | _ -> 0L
                        | _ -> 0L
                    else
                        0L

                Some(getStr "mediaType", getStr "fileName", getInt "size")
            with _ ->
                None
        | Some _ -> None

    member _.Dispose() = blobs.Dispose()
