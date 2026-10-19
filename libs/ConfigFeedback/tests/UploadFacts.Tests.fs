module UploadFactsTests
open System
open System.IO
open Xunit
open Wanxiang.UI

[<Fact>]
let ``分块拼回去就是原文件，序号从 0 连续`` () =
    let bytes = Array.init (UploadFacts.chunkSize * 2 + 5) byte
    let cs = UploadFacts.chunks bytes
    Assert.Equal<int list>([ 0; 1; 2 ], cs |> List.map fst)
    Assert.Equal<byte[]>(bytes, cs |> List.map (snd >> Convert.FromBase64String) |> Array.concat)
    Assert.Empty(UploadFacts.chunks [||])

[<Fact>]
let ``读完文件时已断开或换了主机，就不上传`` () =
    Assert.True(UploadFacts.stillOwned true "a" 3 "a" 3)
    Assert.False(UploadFacts.stillOwned true "a" 3 "a" 4)
    Assert.False(UploadFacts.stillOwned false "a" 3 "a" 3)
