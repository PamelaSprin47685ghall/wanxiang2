module PlainTextTests

open System
open Xunit
open Wanxiang.Core

[<Fact>]
let ``markdown is flattened for plain-text surfaces`` () =
    let raw =
        "### 关于排版\n\n这是 **加粗** 与 `行内代码`，还有[链接](https://example.com)。\n\n- 项目一\n- 项目二\n\n```fsharp\nlet x = 1\n```\n"
    let plain = PlainText.ofMarkdown raw
    Assert.DoesNotContain("#", plain)
    Assert.DoesNotContain("**", plain)
    Assert.DoesNotContain("`", plain)
    Assert.DoesNotContain("https://example.com", plain)
    Assert.DoesNotContain("let x = 1", plain)
    Assert.Contains("关于排版", plain)
    Assert.Contains("加粗", plain)
    Assert.Contains("链接", plain)
    Assert.Contains("项目一", plain)
    Assert.False(plain.Contains "\n")

[<Fact>]
let ``plain-text summary truncates with an ellipsis`` () =
    Assert.Equal("一二三四五六七八…", PlainText.summarize 8 "## 一二三四五六七八九十")

[<Fact>]
let ``image syntax leaves no stray exclamation mark`` () =
    Assert.Equal("示意图", PlainText.ofMarkdown "![示意图](a.png)")

[<Fact>]
let ``summary never splits a surrogate pair`` () =
    let complexText = "👨‍👩‍👧‍👦 🇨🇳 𠮷野家 👍🏽 欢迎光临"
    for maxChars in 1 .. 12 do
        let s = PlainText.summarize maxChars complexText
        Assert.False(String.IsNullOrEmpty s)
        for i in 0 .. s.Length - 1 do
            if Char.IsHighSurrogate s[i] then Assert.True(i + 1 < s.Length && Char.IsLowSurrogate s[i + 1])
            elif Char.IsLowSurrogate s[i] then Assert.True(i > 0 && Char.IsHighSurrogate s[i - 1])
