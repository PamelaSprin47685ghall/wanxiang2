namespace JsonSeq

open System
open System.IO

/// 一次追加的结果。三种情况如实区分：没有证据时不说「没写」。
type AppendOutcome =
    /// 已写入并 Flush(true)；新的文件末尾偏移。
    | Appended of endOffset: int64
    /// 写失败，且已截回写前长度：确知文件未变。
    | NotWritten of reason: string
    /// 写失败，截回也失败：文件里可能有这条的全部、部分或没有。只能重新读文件才能知道。
    | Unknown of reason: string

/// 单文件的 RFC 7464 追加日志。只持有一个写句柄；局部可变状态仅限文件流本身。
/// Append 在返回前 flush 到磁盘（Flush(true)），返回新的文件末尾偏移。
type JsonSeqFile(path: string) =
    do
        match Path.GetDirectoryName(Path.GetFullPath path) with
        | null
        | "" -> ()
        | d -> Directory.CreateDirectory d |> ignore
    // 不用缓冲：写失败后缓冲区里不能残留字节，否则下次 Flush 或 Dispose 会把它们写进文件。
    let stream =
        new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 0)

    member _.Path = path
    member _.Length = stream.Length

    /// 读出 [from, until) 区间内的记录。两端须位于记录边界（通常来自之前返回的偏移）。
    member _.Read(from: int64, until: int64) : Record list =
        if from < 0L || until < from || until > stream.Length then
            invalidArg (nameof from) "range out of bounds"

        let len = int (until - from)
        let buf = Array.zeroCreate<byte> len
        stream.Position <- from
        stream.ReadExactly(buf, 0, len)
        Frame.split from (ReadOnlySpan buf)

    /// 从 offset 读到文件末尾。
    member this.ReadFrom(offset: int64) : Record list = this.Read(offset, stream.Length)

    /// 追加一帧并落盘。文本含 RS 属于程序错误，抛出（见 Frame.encode）。
    member _.Append(text: string) : AppendOutcome =
        let bytes = Frame.encode text
        let mutable before = -1L

        try
            before <- stream.Length
            stream.Position <- before
            stream.Write(bytes, 0, bytes.Length)
            stream.Flush true
            Appended stream.Length
        with e ->
            if before < 0L then
                NotWritten e.Message
            else
                try
                    stream.SetLength before
                    stream.Flush true
                    NotWritten e.Message
                with e2 ->
                    Unknown(sprintf "%s; rollback failed: %s" e.Message e2.Message)

    /// 截断到 offset（用于修复与运行时回滚）。
    member _.Truncate(offset: int64) : unit =
        if offset < 0L || offset > stream.Length then
            invalidArg (nameof offset) "offset out of range"

        stream.SetLength offset
        stream.Flush true

    interface IDisposable with
        member _.Dispose() = stream.Dispose()
