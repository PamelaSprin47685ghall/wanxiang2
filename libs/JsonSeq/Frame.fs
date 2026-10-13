namespace JsonSeq

open System
open System.Text

/// 一条读出的记录。
/// payload：RS 与 LF 之间的 UTF-8 文本（有 fault 时可能为空）；
/// fault：None = 完整有效；Some 原因 = 这段字节不可信（截断、非法 UTF-8、游离字节）；
/// startOffset / endOffset：在字节流中的起止位置（endOffset 不含）。
type Record = { payload: string; fault: string option; startOffset: int64; endOffset: int64 }

/// RFC 7464 分帧：RS(0x1E) + 文本 + LF(0x0A)。
[<RequireQualifiedAccess>]
module Frame =
    [<Literal>]
    let RS = 0x1Euy
    [<Literal>]
    let LF = 0x0Auy

    [<Literal>]
    let TornTail = "torn record (no trailing LF)"
    [<Literal>]
    let InvalidUtf8 = "invalid UTF-8"
    [<Literal>]
    let StrayBytes = "bytes outside any record"

    /// 严格解码：非法字节抛出，不替换为 U+FFFD。
    let private strict = UTF8Encoding(false, true)

    /// 包成一帧。文本内含 RS 会破坏边界，直接拒绝（防呆）。
    let encode (text: string) : byte[] =
        if text.IndexOf '\u001e' >= 0 then invalidArg (nameof text) "payload must not contain RS (0x1E)"
        let body = strict.GetBytes text
        let buf = Array.zeroCreate<byte> (body.Length + 2)
        buf[0] <- RS
        Array.blit body 0 buf 1 body.Length
        buf[body.Length + 1] <- LF
        buf

    /// 按 RS 切分。baseOffset 是 data[0] 在整个流中的偏移，且须位于记录边界。
    /// 相邻 RS 之间的空段忽略；第一个 RS 之前的字节成为一条 StrayBytes 记录。
    let split (baseOffset: int64) (data: ReadOnlySpan<byte>) : Record list =
        let result = ResizeArray<Record>()
        let first = data.IndexOf RS
        let lead = if first < 0 then data.Length else first
        if lead > 0 then
            result.Add { payload = ""; fault = Some StrayBytes; startOffset = baseOffset; endOffset = baseOffset + int64 lead }
        let mutable i = lead
        while i < data.Length do
            let mutable j = i + 1
            while j < data.Length && data[j] <> RS do j <- j + 1
            let len = j - i - 1
            if len > 0 then
                let seg = data.Slice(i + 1, len)
                let complete = seg[seg.Length - 1] = LF
                let body = if complete then seg.Slice(0, seg.Length - 1) else seg
                let text, fault =
                    try strict.GetString body, (if complete then None else Some TornTail)
                    with :? DecoderFallbackException -> "", Some InvalidUtf8
                result.Add { payload = text; fault = fault; startOffset = baseOffset + int64 i; endOffset = baseOffset + int64 j }
            i <- j
        List.ofSeq result
