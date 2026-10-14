module Tests

open Xunit
open Wanxiang.Pwa

// 浏览器之外没有 JS 宿主：JSImport 调用会抛。这里钉住的正是旧实现的退化规则——绝不抛给界面，一律 None。
[<Fact>]
let ``浏览器之外调用退化为 None，不抛`` () =
    let b = BrowserRichBackend()
    b.Warmup()
    Assert.Equal(None, b.HighlightHtml("let x = 1", "javascript"))
    Assert.Equal(None, b.HighlightHtml("let x = 1", "fsharp"))
    Assert.Equal(None, b.MathHtml("x^2", false))
