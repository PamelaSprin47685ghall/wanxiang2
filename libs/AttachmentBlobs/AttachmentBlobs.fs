namespace Wanxiang.Server

open System
open System.Collections.Concurrent
open System.IO
open System.Security.Cryptography

/// 上传失败的原因。文字与旧实现相同，组合者直接转成自己的错误类型。
[<RequireQualifiedAccess>]
type BlobError =
    | Invalid of string
    | TooLarge of limitBytes: int64 * actualBytes: int64
    | HashMismatch of expected: string * actual: string
    | NotActive of uploadId: Guid
    | Failed of string

type internal Upload =
    {
        owner: int
        tempPath: string
        expectedSha256: string
        expectedBytes: int64
        mutable receivedBytes: int64
        mutable chunkCount: int
        stream: FileStream
        sha: IncrementalHash
    }

/// 内容寻址的分块上传：SHA-256 就是永久标识，路径按哈希分两级（ab/cd/<hash>）。
/// 完成前只存在于 .tmp 下的临时文件；完成时校验长度和哈希，相同内容只存一份。
/// 分块必须按序号严格递增到达。不懂媒体类型和文件名。线程安全。
type AttachmentBlobs(root: string, maxBytes: int64, maxChunkBytes: int) =
    let tempDir = Path.Combine(root, ".tmp")

    do
        Directory.CreateDirectory root |> ignore
        Directory.CreateDirectory tempDir |> ignore

    let uploads = ConcurrentDictionary<Guid, Upload>()

    static member IsSha256(value: string) =
        not (String.IsNullOrWhiteSpace value)
        && value.Length = 64
        && value |> Seq.forall Char.IsAsciiHexDigit

    /// 某个哈希的存储路径；不是合法哈希时 None。
    member _.PathOf(sha256: string) : string option =
        if not (AttachmentBlobs.IsSha256 sha256) then
            None
        else
            let h = sha256.ToLowerInvariant()
            Some(Path.Combine(root, h.Substring(0, 2), h.Substring(2, 2), h))

    member _.Begin(owner: int, uploadId: Guid, totalBytes: int64, sha256: string) : Result<unit, BlobError> =
        if totalBytes < 0L then
            Error(BlobError.Invalid "attachment size must not be negative")
        elif not (AttachmentBlobs.IsSha256 sha256) then
            Error(BlobError.Invalid "attachment sha256 must be 64 hexadecimal characters")
        elif totalBytes > maxBytes then
            Error(BlobError.TooLarge(maxBytes, totalBytes))
        elif uploads.ContainsKey uploadId then
            Error(BlobError.Invalid "attachment id is already active")
        else
            let tempPath = Path.Combine(tempDir, uploadId.ToString("N"))

            try
                let stream =
                    new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)

                try
                    File.SetUnixFileMode(tempPath, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)
                with _ ->
                    ()

                let u =
                    {
                        owner = owner
                        tempPath = tempPath
                        expectedSha256 = sha256
                        expectedBytes = totalBytes
                        receivedBytes = 0L
                        chunkCount = 0
                        stream = stream
                        sha = IncrementalHash.CreateHash HashAlgorithmName.SHA256
                    }

                if uploads.TryAdd(uploadId, u) then
                    Ok()
                else
                    stream.Dispose()
                    File.Delete tempPath
                    Error(BlobError.Invalid "attachment id is already active")
            with e ->
                Error(BlobError.Failed(sprintf "attachment begin failed: %s" e.Message))

    member _.Append(uploadId: Guid, index: int, dataBase64: string) : Result<unit, BlobError> =
        match uploads.TryGetValue uploadId with
        | false, _ -> Error(BlobError.NotActive uploadId)
        | true, s ->
            try
                if index <> s.chunkCount then
                    Error(
                        BlobError.Invalid(
                            sprintf "attachment chunk index %d out of order; expected %d" index s.chunkCount
                        )
                    )
                else
                    let bytes = Convert.FromBase64String dataBase64

                    if bytes.Length > maxChunkBytes then
                        Error(BlobError.Invalid(sprintf "attachment chunk exceeds %d bytes limit" maxChunkBytes))
                    elif s.receivedBytes + int64 bytes.Length > s.expectedBytes then
                        Error(BlobError.Invalid "attachment chunk exceeds declared size")
                    else
                        s.stream.Write(bytes, 0, bytes.Length)
                        s.sha.AppendData(bytes, 0, bytes.Length)
                        s.receivedBytes <- s.receivedBytes + int64 bytes.Length
                        s.chunkCount <- s.chunkCount + 1
                        Ok()
            with e ->
                Error(BlobError.Invalid(sprintf "attachment chunk decode failed: %s" e.Message))

    /// 校验并落位。成功返回 (小写哈希, 字节数, 存储路径)。失败时临时文件被删除，上传结束。
    member _.Complete(uploadId: Guid, declaredSha256: string) : Result<string * int64 * string, BlobError> =
        match uploads.TryGetValue uploadId with
        | false, _ -> Error(BlobError.NotActive uploadId)
        | true, s ->
            try
                s.stream.Flush()
                s.stream.Dispose()
                let actual = Convert.ToHexString(s.sha.GetHashAndReset()).ToLowerInvariant()

                let fail err =
                    File.Delete s.tempPath
                    uploads.TryRemove uploadId |> ignore
                    Error err

                if s.receivedBytes <> s.expectedBytes then
                    fail (
                        BlobError.Invalid(
                            sprintf "attachment size mismatch: expected %d got %d" s.expectedBytes s.receivedBytes
                        )
                    )
                elif not (String.Equals(actual, s.expectedSha256, StringComparison.OrdinalIgnoreCase)) then
                    fail (BlobError.HashMismatch(s.expectedSha256, actual))
                elif not (String.Equals(declaredSha256, s.expectedSha256, StringComparison.OrdinalIgnoreCase)) then
                    // 内容无损，是 complete 传错了声明值：把这个值点名放进错误
                    fail (BlobError.HashMismatch(s.expectedSha256, declaredSha256))
                else
                    let dir = Path.Combine(root, actual.Substring(0, 2), actual.Substring(2, 2))
                    Directory.CreateDirectory dir |> ignore
                    let finalPath = Path.Combine(dir, actual)

                    if not (File.Exists finalPath) then
                        File.Move(s.tempPath, finalPath)
                    else
                        File.Delete s.tempPath

                    uploads.TryRemove uploadId |> ignore
                    Ok(actual, s.receivedBytes, finalPath)
            with e ->
                Error(BlobError.Failed(sprintf "attachment complete failed: %s" e.Message))

    member _.Abort(uploadId: Guid) : unit =
        match uploads.TryGetValue uploadId with
        | false, _ -> ()
        | true, s ->
            try
                s.stream.Dispose()
                File.Delete s.tempPath
            with _ ->
                ()

            uploads.TryRemove uploadId |> ignore

    /// 某个发起者（如一条连接）的全部未完成上传作废。
    member this.AbortByOwner(owner: int) : unit =
        for kv in uploads do
            if kv.Value.owner = owner then
                this.Abort kv.Key

    member this.OpenRead(sha256: string) : (Stream * int64) option =
        match this.PathOf sha256 with
        | Some path when File.Exists path ->
            let fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            Some(fs :> Stream, fs.Length)
        | _ -> None

    member this.Exists(sha256: string) =
        this.PathOf sha256 |> Option.exists File.Exists

    member _.Dispose() =
        for kv in uploads do
            try
                kv.Value.stream.Dispose()
            with _ ->
                ()
