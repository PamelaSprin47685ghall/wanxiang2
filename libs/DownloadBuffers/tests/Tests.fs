module Tests

open System
open Xunit
open Wanxiang.UI

// 下载缓冲：重复 Begin 重置、触顶整体丢弃、完成取走、断连清零。
[<Fact>]
let ``buffers reset on retry, drop at the cap and hand over bytes once`` () =
    let b = DownloadBuffers(10L)
    b.Begin("AB", "a.txt")
    Assert.Equal(DownloadChunkResult.Accepted, b.Append("ab", [| 1uy; 2uy |]))
    b.Begin("ab", "b.txt")
    Assert.Equal(DownloadChunkResult.Accepted, b.Append("AB", [| 3uy |]))
    Assert.Equal(Some("b.txt", [| 3uy |]), b.Take("Ab"))
    Assert.Equal(None, b.Take("ab"))
    Assert.True(match b.Append("ab", [| 1uy |]) with DownloadChunkResult.Unknown _ -> true | _ -> false)
    b.Begin("cd", "c")
    Assert.Equal(DownloadChunkResult.Capped, b.Append("cd", Array.zeroCreate 11))
    Assert.Equal(0, b.PendingCount)
    b.Begin("ef", "e")
    b.Clear()
    Assert.Equal(0, b.PendingCount)

// 迁自旧 AppShell 的 AttachmentDownloadBegin / AttachmentDownloadChunk 分支：
// 旧代码原样摘录（toast 换成计数）与 AppendBase64 跑同一串事件，逐步比对是否提示。
[<Fact>]
let ``capped notice once per begin, bad base64 ignored, like old AppShell`` () =
    let oldBuffers = DownloadBuffers(8L)
    let mutable downloadCappedNotified = false
    let oldStep ev =
        let mutable toasted = false
        let toast (_: string) () = toasted <- true
        let Warning = ()
        match ev with
        | Choice1Of2(sha256: string, fileName: string) ->
            oldBuffers.Begin(sha256, fileName)
            downloadCappedNotified <- false
        | Choice2Of2(sha256: string, dataBase64: string) ->
            try
                let bytes = Convert.FromBase64String dataBase64
                match oldBuffers.Append(sha256, bytes) with
                | DownloadChunkResult.Capped when not downloadCappedNotified ->
                    downloadCappedNotified <- true
                    toast "附件过大，下载已中止。" Warning
                | _ -> ()
            with _ -> ()
        toasted
    let buffers = DownloadBuffers(8L)
    let newStep ev =
        match ev with
        | Choice1Of2(s, f) -> buffers.Begin(s, f); false
        | Choice2Of2(s, d) -> buffers.AppendBase64(s, d)
    let b64 n = Convert.ToBase64String(Array.zeroCreate<byte> n)
    let events =
        [ Choice1Of2("A", "a"); Choice2Of2("A", b64 4); Choice2Of2("A", "%%bad"); Choice2Of2("A", b64 6)
          Choice1Of2("B", "b"); Choice2Of2("B", b64 9); Choice2Of2("A", b64 1); Choice2Of2("B", b64 9)
          Choice1Of2("A", "a"); Choice2Of2("A", b64 9); Choice2Of2("C", b64 1) ]
    let oldSeq = events |> List.map oldStep
    Assert.Equal<bool list>(oldSeq, events |> List.map newStep)
    Assert.Contains(true, oldSeq)
