module TitleTextTests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``clip, first line, quotes and first sentence`` () =
    Assert.Equal(System.String('a', 24) + "…", TitleText.clip (System.String('a', 30)))
    Assert.Equal("one", TitleText.firstLine "\none\ntwo")
    Assert.Equal("标题", TitleText.stripQuotes "「标题」。")
    Assert.Equal("Rust 入门", TitleText.firstSentence "Rust 入门。然后")
    Assert.Equal("a。b", TitleText.firstSentence "a。b")

[<Fact>]
let ``placeholder titles`` () =
    Assert.True(TitleText.isPlaceholder "对话 3f8a")
    Assert.True(TitleText.isPlaceholder " ")
    Assert.False(TitleText.isPlaceholder "我的标题")
