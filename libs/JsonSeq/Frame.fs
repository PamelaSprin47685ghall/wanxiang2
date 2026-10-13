namespace JsonSeq

open System
open System.Text

/// 一条读出的记录。
/// payload：RS 与 LF 之间的 UTF-8 文本；
/// complete：是否以 LF 正常结尾（否 = 尾部被截断）；
/// startOffset / endOffset：记录在字节流中的起止位置（endOffset 不含）。
type Record = { payload: string; complete: bool; startOffset: int64; endOffset: int64 }

/// RFC 7464 分帧：RS(0x1E) + 文本 + LF(0x0A)。
[<RequireQualifiedAccess>]
module Frame =
    [<Literal>]
    let RS = 0x1Euy
    [<Literal>]
    let LF = 0x0Auy

    /// 包成一帧。文本内含 RS 会破坏边界，直接拒绝（防呆）。
    let encode (text: string) : byte[] =
        if text.IndexOf '\u001e' >= 0 then invalidArg (nameof text) "payload must not contain RS (0x1E)"
        let body = Encoding.UTF8.GetBytes text
        let buf = Array.zeroCreate<byte> (body.Length + 2)
        buf[0] <- RS
        Array.blit body 0 buf 1 body.Length
        buf[body.Length + 1] <- LF
        buf

    /// 按 RS 切分。base 是 data[0] 在整个流中的偏移。
    /// 相邻 RS 之间的空段忽略；RS 之前的游离字节忽略。
    let split (baseOffset: int64) (data: ReadOnlySpan<byte>) : Record list =
        let result = ResizeArray<Record>()
        let mutable i = 0
        while i < data.Length do
            if data[i] = RS then
                let mutable j = i + 1
                while j < data.Length && data[j] <> RS do j <- j + 1
                let len = j - i - 1
                if len > 0 then
                    let seg = data.Slice(i + 1, len)
                    let complete = seg[seg.Length - 1] = LF
                    let body = if complete then seg.Slice(0, seg.Length - 1) else seg
                    result.Add { payload = Encoding.UTF8.GetString body
                                 complete = complete
                                 startOffset = baseOffset + int64 i
                                 endOffset = baseOffset + int64 j }
                i <- j
            else i <- i + 1
        List.ofSeq result
