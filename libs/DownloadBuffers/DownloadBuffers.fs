namespace Wanxiang.UI

open System
open System.Collections.Generic
open System.IO

/// 附件下载的在途接收缓冲，键为小写 sha256。
///
/// 主机按 chunk 推流，只有完成事件会消费缓冲。断连、重试或完成事件丢失时
/// 缓冲必须能整体丢弃，否则每个半途而废的下载都会在内存里留下一个永不释放的
/// MemoryStream（Phase 0 风险 R1）。单个下载另有硬上限，超限即丢弃缓冲并让
/// 调用方给出一次性提示，内存不随异常流无限增长。不持久化任何内容；
/// 断连由 AppShell 调用 Clear 清零。
[<RequireQualifiedAccess>]
type DownloadChunkResult =
    | Accepted
    /// 本次写入触顶：缓冲已整体丢弃，调用方应提示一次
    | Capped
    /// 没有在途缓冲（尚未 Begin、已被 Capped 清掉或已完成），附原因便于诊断。
    | Unknown of reason: string

type DownloadBuffers(?capBytes: int64) =
    let capBytes = defaultArg capBytes (128L * 1024L * 1024L)
    let buffers = Dictionary<string, MemoryStream>()
    let names = Dictionary<string, string>()

    member _.PendingCount = buffers.Count

    /// 开始接收。同键重复 Begin（重试）会重置缓冲并更新文件名，不叠加第二个流。
    member _.Begin(sha256: string, fileName: string) =
        let key = sha256.ToLowerInvariant()
        match buffers.TryGetValue key with
        | true, existing ->
            existing.Dispose()
            buffers[key] <- new MemoryStream()
        | _ -> buffers[key] <- new MemoryStream()
        names[key] <- fileName

    member _.Append(sha256: string, bytes: byte[]) : DownloadChunkResult =
        let key = sha256.ToLowerInvariant()
        match buffers.TryGetValue key with
        | true, buffer ->
            if int64 buffer.Length + int64 bytes.Length > capBytes then
                buffer.Dispose()
                buffers.Remove key |> ignore
                names.Remove key |> ignore
                DownloadChunkResult.Capped
            else
                buffer.Write(bytes, 0, bytes.Length)
                DownloadChunkResult.Accepted
        | _ -> DownloadChunkResult.Unknown "没有在途缓冲：尚未开始、已被丢弃或已完成。"

    /// 完成接收：取走文件名与字节并移除缓冲。文件名缺失时回落到 sha256。
    member _.Take(sha256: string) : (string * byte[]) option =
        let key = sha256.ToLowerInvariant()
        match buffers.TryGetValue key with
        | true, buffer ->
            buffers.Remove key |> ignore
            let fileName =
                match names.TryGetValue key with
                | true, name ->
                    names.Remove key |> ignore
                    name
                | _ -> sha256
            let bytes = buffer.ToArray()
            buffer.Dispose()
            Some(fileName, bytes)
        | _ -> None

    /// 断连/重连：底层流已死，不会再有完成事件，丢弃所有在途缓冲。
    member _.Clear() =
        for entry in buffers do
            entry.Value.Dispose()
        buffers.Clear()
        names.Clear()
