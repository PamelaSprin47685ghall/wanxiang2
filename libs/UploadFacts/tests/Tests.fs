module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由 tools/ 下原样摘出的旧代码跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

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
