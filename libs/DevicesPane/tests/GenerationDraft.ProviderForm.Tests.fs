module GenerationDraftProviderFormTests

open System
open System.IO
open Xunit
open Wanxiang.UI


let ok = GenerationDraftProviderFormSamples.baseInput

[<Fact>]
let ``empty default picks the first model and reports it`` () =
    let r = ProviderForm.validate ok
    Assert.Empty r.errors
    Assert.Equal(Some "qwen", r.autoPicked)
    Assert.Equal("未指定默认模型，已自动选用“qwen”。", ProviderForm.autoPickedNote "qwen")

[<Fact>]
let ``blank timeout and retries keep the existing values, else 120 and 2`` () =
    let r = ProviderForm.validate { ok with existing = Some(45, 7) }
    Assert.Equal((45, 7), (r.timeout, r.retries))
    let r2 = ProviderForm.validate ok
    Assert.Equal((120, 2), (r2.timeout, r2.retries))

[<Fact>]
let ``models split on newline and comma and dedupe`` () =
    Assert.Equal<string list>([ "a"; "b"; "c" ], ProviderForm.lines "a, b,a\r\nc")

[<Fact>]
let ``vision lists must name listed models and not both`` () =
    Assert.Equal<(ProviderField * string) list>(
        [ ProviderField.VisionNo, "“qwen”同时写在两栏里，请只保留一栏。" ],
        (ProviderForm.validate
            { ok with
                visionYes = "qwen"
                visionNo = "qwen"
            })
            .errors
    )

    Assert.Equal(ProviderField.VisionYes, fst (ProviderForm.validate { ok with visionYes = "gpt" }).errors.Head)

[<Fact>]
let ``loopback forms`` () =
    for u in [ "http://127.0.0.1:1"; "http://localhost"; "http://[::1]:2" ] do
        Assert.True(ProviderForm.isLoopbackUrl u)

    Assert.False(ProviderForm.isLoopbackUrl "https://api.openai.com/v1")
    Assert.False(ProviderForm.isLoopbackUrl " ")
