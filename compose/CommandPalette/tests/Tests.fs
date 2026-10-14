module Tests

open Xunit
open Wanxiang.UI

// 迁自旧 R20PaletteTests（纯逻辑部分）。
let private item id kind title keywords =
    { id = id; kind = kind; title = title; subtitle = ""; keywords = keywords; shortcut = None }

let private sample =
    [ item "act:new" PaletteAction "新建对话" [ "new" ]
      item "act:theme" PaletteAction "切换主题" [ "theme" ]
      item "set:appearance" PaletteSetting "设置 › 外观与操作" []
      item "conv:1" PaletteConversation "周报草稿" []
      item "conv:2" PaletteConversation "Rust 学习笔记" [] ]

[<Fact>]
let ``373 拼音首字母与子序列都能命中`` () =
    let ranked = CommandPalette.rank "zb" [] sample
    Assert.Equal("conv:1", ranked.Head.id)
    Assert.Equal("act:theme", (CommandPalette.rank "qhzt" [] sample).Head.id)
    Assert.Equal("conv:2", (CommandPalette.rank "rsxx" [] sample).Head.id)
    Assert.Equal("act:new", (CommandPalette.rank "新建" [] sample).Head.id)
    Assert.Empty(CommandPalette.rank "zzzzq" [] sample)

[<Fact>]
let ``375 空查询先列最近使用，再列操作，不铺全部对话`` () =
    let ranked = CommandPalette.rank "" [ "conv:2"; "act:theme" ] sample
    Assert.Equal<string list>([ "conv:2"; "act:theme"; "act:new" ], ranked |> List.map _.id)

[<Fact>]
let ``375 最近使用去重、新的在前、有上限`` () =
    let r = [ 1 .. 12 ] |> List.fold (fun acc i -> CommandPalette.pushRecent (string i) acc) []
    Assert.Equal(CommandPalette.maxRecent, r.Length)
    Assert.Equal("12", r.Head)
    let r2 = CommandPalette.pushRecent "5" r
    Assert.Equal("5", r2.Head)
    Assert.Equal(1, r2 |> List.filter ((=) "5") |> List.length)

[<Fact>]
let ``recent use outranks equal score and keys are compacted`` () =
    let two = [ item "a" PaletteConversation "报告" []; item "b" PaletteConversation "报告" [] ]
    Assert.Equal("b", (CommandPalette.rank "报告" [ "b" ] two).Head.id)
    Assert.Equal("Ctrl+Shift+E", CommandPalette.compactKeys "Ctrl / ⌘ + Shift + E")
    Assert.Equal("在全部消息中搜索「x」", CommandPalette.fullTextTitle " x ")

// 落盘字节：期望值由旧程序集的 saveRecent / loadRecent 实际读写得到。
[<Fact>]
let ``palette recent file bytes match old load and save`` () =
    Assert.Equal("""["new","\u8BBE\u7F6E\u0022x\u0022","a"]""", PaletteRecent.encode [ "new"; "设置\"x\""; "a" ])
    Assert.Equal<string list>([ "a"; "b"; "c"; "d"; "e"; "f"; "g"; "h" ], PaletteRecent.decode """["a","b","c","d","e","f","g","h","i","j"]""")
    for bad in [ "null"; "{bad"; "[]"; """["x",1]""" ] do
        Assert.Empty(PaletteRecent.decode bad)
