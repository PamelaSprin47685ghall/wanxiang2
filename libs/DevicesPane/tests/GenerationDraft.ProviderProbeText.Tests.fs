module GenerationDraftProviderProbeTextTests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``open editor fills models, closed editor caches them`` () =
    let o = ProviderProbeText.feedback true true 3 ""
    Assert.True o.fillModels
    Assert.Equal(Some "", o.editorStatus)
    let c = ProviderProbeText.feedback false true 3 ""
    Assert.True c.cacheModels
    Assert.Equal(None, c.editorStatus)
    Assert.True((ProviderProbeText.feedback false true 0 "").cacheModels)

[<Fact>]
let ``failure shows the parsed message as a failure`` () =
    let f = ProviderProbeText.feedback true false 0 "密钥无效"
    Assert.Equal(ProbeTone.Failure, f.tone)
    Assert.Equal(Some "密钥无效", f.editorStatus)

[<Fact>]
let ``vision summary lists at most twelve`` () =
    let s =
        ProviderProbeText.visionSummary [ for i in 1..20 -> string i ] (fun _ -> VisionVerdict.No, VisionWhy.User)

    Assert.Equal(12, s.Split('\n').Length)
    Assert.Equal("1：只吃文字（你的指定）", s.Split('\n').[0])
