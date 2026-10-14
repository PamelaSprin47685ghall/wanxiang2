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
