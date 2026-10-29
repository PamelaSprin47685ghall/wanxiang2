namespace JsonSeq

open System
open System.IO

/// 把 [from, until) 区间按 RS 记录流式切分。baseOffset 是 from 在流中的偏移，
/// 与 Frame.split 的语义一致：记录跨块时保留未完字节续读，结果与一次性读入逐字节相同。
module StreamSplit =

    /// 默认流式读取的分块大小（字节）。每次只把这么多字节读进内存再切分，
    /// 记录跨块边界时把残余字节挪到下一块，语义与一次性读入完全相同。
    [<Literal>]
    let DefaultChunkBytes = 1 <<< 20 // 1 MiB

    /// 流式切分，返回全部记录。chunkSize 是每块字节数；
    /// reader 把 (流内偏移, 长度) 读进给定的字节数组，返回实际读到的字节数。
    let readAll (chunkSize: int) (reader: int64 * int * byte[] -> int) (from: int64) (until: int64) : Record list =
        let buf = Array.zeroCreate<byte> chunkSize
        let result = ResizeArray<Record>()
        // pending：上一块末尾没处理完（记录还没闭合）的字节；pendingStart 是它们在流中的起点。
        let pending = ResizeArray<byte>()
        let mutable pendingStart = from
        let mutable pos = from

        // 把 pending 与新读到的字节拼起来切分。
        let consume (chunk: byte[]) (count: int) : unit =
            let data = Array.zeroCreate<byte>(pending.Count + count)
            pending.CopyTo data
            Array.blit chunk 0 data pending.Count count

            // 在本块内最后一个 RS（下标 > 0）处下刀：Frame 以 RS 划分记录边界，正文内允许含 LF。
            // 把 data[lastRs] 这个 RS 一并作为哨兵传给 Frame.split，使 [0, lastRs) 内紧邻的无正文 RS
            // 看到后续 RS 而被判为 EmptyRecord（而非流末尾的 TornTail），再丢弃哨兵自身的尾记录。
            let mutable lastRs = -1

            for i in 1 .. data.Length - 1 do
                if data[i] = Frame.RS then
                    lastRs <- i

            if lastRs > 0 then
                let rs = Frame.split pendingStart (ReadOnlySpan(data, 0, lastRs + 1))

                for k in 0 .. rs.Length - 2 do
                    result.Add rs[k]

                pendingStart <- pendingStart + int64 lastRs
                pending.Clear()

                for i in lastRs .. data.Length - 1 do
                    pending.Add data[i]
            else
                pending.Clear()

                for i in 0 .. data.Length - 1 do
                    pending.Add data[i]

        while pos < until do
            let want = int (min (int64 chunkSize) (until - pos))
            let got = reader (pos, want, buf)
            let count = min want got

            if count <= 0 then
                failwithf "stream ended early at %d" pos

            consume buf count
            pos <- pos + int64 count

        // 收尾：流里最后剩下的字节按 Frame.split 的规则处理（TornTail / StrayBytes / 无正文 RS）。
        if pending.Count > 0 || pendingStart < until then
            let tail = Array.ofSeq pending
            result.AddRange(Frame.split pendingStart (ReadOnlySpan tail))

        List.ofSeq result
