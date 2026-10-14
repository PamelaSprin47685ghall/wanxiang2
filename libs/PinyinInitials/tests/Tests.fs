module Tests

open Xunit
open Wanxiang.UI

// 迁自旧 R20PaletteTests。
[<Fact>]
let ``373 拼音首字母覆盖常用字`` () =
    Assert.Equal("zbcg", PinyinInitials.initials "周报草稿")
    Assert.Equal("xjdh", PinyinInitials.initials "新建对话")
    Assert.Equal("rustxxbj", PinyinInitials.initials "Rust 学习笔记")

[<Fact>]
let ``non-ascii non-han characters are dropped and ascii is lowered`` () =
    Assert.Equal("ab1", PinyinInitials.initials "A·b—1！")
    Assert.Equal(None, PinyinInitials.ofChar 'x')
    Assert.Equal(Some 'z', PinyinInitials.ofChar '中')
