namespace JsonSeq

open System
open System.IO

/// 单文件的 RFC 7464 追加日志。只持有一个写句柄；局部可变状态仅限文件流本身。
/// Append 在返回前 flush 到磁盘（Flush(true)），返回新的文件末尾偏移。
type JsonSeqFile(path: string) =
    do
        match Path.GetDirectoryName(Path.GetFullPath path) with
        | null | "" -> ()
        | d -> Directory.CreateDirectory d |> ignore
    let stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)

    member _.Path = path
    member _.Length = stream.Length

    /// 读出 [from, until) 区间内的记录。两端须位于记录边界（通常来自之前返回的偏移）。
    member _.Read(from: int64, until: int64) : Record list =
        if from < 0L || until < from || until > stream.Length then invalidArg (nameof from) "range out of bounds"
        let len = int (until - from)
        let buf = Array.zeroCreate<byte> len
        stream.Position <- from
        stream.ReadExactly(buf, 0, len)
        Frame.split from (ReadOnlySpan buf)

    /// 从 offset 读到文件末尾。
    member this.ReadFrom(offset: int64) : Record list = this.Read(offset, stream.Length)

    /// 追加一帧并落盘。
    member _.Append(text: string) : int64 =
        let bytes = Frame.encode text
        stream.Position <- stream.Length
        stream.Write(bytes, 0, bytes.Length)
        stream.Flush true
        stream.Length

    /// 截断到 offset（用于修复与运行时回滚）。
    member _.Truncate(offset: int64) : unit =
        if offset < 0L || offset > stream.Length then invalidArg (nameof offset) "offset out of range"
        stream.SetLength offset
        stream.Flush true

    interface IDisposable with
        member _.Dispose() = stream.Dispose()
