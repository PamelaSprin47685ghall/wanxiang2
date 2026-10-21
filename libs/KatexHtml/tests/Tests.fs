module Tests

open Xunit
open Wanxiang.UI

// 以下迁自旧 UiPureLogicTests（RichBackendAndKatexHtmlTests）。
[<Fact>]
let ``KatexHtml fontScaleOf computes ratio of size classes`` () =
    // 默认无 class 为 1.0
    Assert.Equal(1.0, KatexHtml.fontScaleOf [])
    // reset-size6 size3 的缩放比率：size6 索引 5 (1.0)，size3 索引 2 (0.7)
    let scaled = KatexHtml.fontScaleOf [ "reset-size6"; "size3" ]
    Assert.Equal(0.7, scaled, 3)

[<Fact>]
let ``KatexHtml emOf parses em and px correctly`` () =
    let style =
        Map.ofList [ "height", "1.5em"; "margin-top", "-0.2em"; "width", "24px" ]

    Assert.Equal(Some 1.5, KatexHtml.emOf style "height")
    Assert.Equal(Some -0.2, KatexHtml.emOf style "margin-top")
    Assert.Equal(Some 24.0, KatexHtml.emOf style "width")
    Assert.True(KatexHtml.emOf style "nonexistent" |> Option.isNone)

[<Fact>]
let ``KatexHtml parse and visualSubtree filters katex-html`` () =
    let html =
        "<span class=\"katex\"><span class=\"katex-mathml\">text</span><span class=\"katex-html\"><span>x</span></span></span>"

    let nodes = KatexHtml.parse html
    Assert.NotEmpty(nodes)
    let vis = KatexHtml.visualSubtree nodes
    Assert.True(vis.IsSome)

[<Fact>]
let ``KatexHtml isDisplay detects display formulas`` () =
    let inlineHtml = "<span class=\"katex\"><span class=\"katex-html\">x</span></span>"

    let displayHtml =
        "<span class=\"katex-display\"><span class=\"katex-html\">x</span></span>"

    Assert.False(KatexHtml.isDisplay (KatexHtml.parse inlineHtml))
    Assert.True(KatexHtml.isDisplay (KatexHtml.parse displayHtml))


// 本库补充：边界与失败路径。

[<Fact>]
let ``单独的 size 类不是缩放`` () =
    Assert.Equal(1.0, KatexHtml.fontScaleOf [ "delimsizing"; "size1" ])
    Assert.Equal(1.0, KatexHtml.fontScaleOf [ "reset-size6" ])
    Assert.Equal(1.0, KatexHtml.fontScaleOf [ "reset-size99"; "size3" ])

[<Fact>]
let ``标签没闭合时已收集的内容不丢`` () =
    match KatexHtml.parse "<span class=\"a\"><span>x" with
    | [ KElement("span", [ "a" ], _, [ KElement("span", [], _, [ KText "x" ]) ]) ] -> ()
    | other -> failwithf "%A" other

[<Fact>]
let ``svg 的 width height d viewBox 并进 style`` () =
    match KatexHtml.parse "<svg width=\"1em\" height=\"2em\" viewBox=\"0 0 1 1\"><path d=\"M0 0\"></path></svg>" with
    | [ KElement("svg", _, style, [ KElement("path", _, pstyle, []) ]) ] ->
        Assert.Equal("1em", style["width"])
        Assert.Equal("0 0 1 1", style["-viewbox"])
        Assert.Equal("M0 0", pstyle["-d"])
    | other -> failwithf "%A" other

[<Fact>]
let ``实体解码，空串为空`` () =
    Assert.Equal<KNode list>([ KText "a<b" ], KatexHtml.parse "a&lt;b")
    Assert.Empty(KatexHtml.parse "")
    Assert.True((KatexHtml.visualSubtree []).IsNone)
