module Tests

open Xunit
open Wanxiang.UI

// 以下迁自旧 PolishTests（用真实的内嵌目录）。
[<Fact>]
let ``language annotations resolve through hljs aliases`` () =
    Assert.Equal(Some "typescript", RichAssets.resolveLanguage "TS")
    Assert.Equal(Some "fsharp", RichAssets.resolveLanguage "F#")
    Assert.Equal(Some "python", RichAssets.resolveLanguage "py")
    Assert.Equal(Some "bash", RichAssets.resolveLanguage "sh")
    Assert.Equal(Some "latex", RichAssets.resolveLanguage "tex")
    // shell 是 hljs 里独立的一门语言（shell 对话），不是 bash 的别名
    Assert.Equal(Some "shell", RichAssets.resolveLanguage "shell")

[<Fact>]
let ``an unknown language annotation resolves to nothing`` () =
    Assert.Equal(None, RichAssets.resolveLanguage "")
    Assert.Equal(None, RichAssets.resolveLanguage "   ")
    Assert.Equal(None, RichAssets.resolveLanguage "沒有這種語言")

// 本组合补充。

[<Fact>]
let ``按需语言经别名解析后都能取到脚本`` () =
    for tag in [ "F#"; "dockerfile"; "hs"; "tex" ] do
        match RichAssets.resolveLanguage tag with
        | Some c when RichAssets.requiresLoad c -> Assert.NotEmpty(RichAssets.hljsLanguageScript c)
        | other -> failwithf "%s -> %A" tag other

[<Fact>]
let ``基座语言不需要额外加载`` () =
    Assert.False(RichAssets.requiresLoad "python")
    Assert.NotEmpty(RichAssets.hljsCoreScript ())
    Assert.NotEmpty(RichAssets.katexScript ())
