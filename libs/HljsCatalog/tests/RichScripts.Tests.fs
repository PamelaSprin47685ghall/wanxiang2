module RichScriptsTests

open System.Text.Json
open Xunit
open Wanxiang.UI

[<Fact>]
let ``基座脚本都在且非空`` () =
    Assert.Contains("katex", RichScripts.katex ())
    Assert.Contains("hljs", RichScripts.hljsCore ())

[<Fact>]
let ``目录里每门按需语言都有对应脚本`` () =
    use doc = JsonDocument.Parse(RichScripts.hljsAliases ())

    let names =
        [
            for e in doc.RootElement.GetProperty("onDemand").EnumerateArray() -> e.GetString()
        ]

    Assert.NotEmpty names

    for n in names do
        Assert.False(System.String.IsNullOrWhiteSpace(RichScripts.hljsLanguage n), n)

[<Fact>]
let ``不存在的脚本抛出并带上名字`` () =
    let e = Assert.ThrowsAny<exn>(fun () -> RichScripts.hljsLanguage "nope" |> ignore)
    Assert.Contains("hljs.lang/nope.js", e.Message)
