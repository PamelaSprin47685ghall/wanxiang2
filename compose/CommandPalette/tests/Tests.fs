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

// 金标准：golden/expected.txt 由旧 AppShell.PaletteItems / RunPaletteItem 方法体的逐行复制在 TZ=UTC 下跑同一份 Render.fs 生成。
[<Fact>]
let ``golden: shell palette items and dispatch match old AppShell`` () =
    if System.TimeZoneInfo.Local.BaseUtcOffset = System.TimeSpan.Zero then
        Assert.Equal(System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

[<Fact>]
let ``374 没打开对话时不给导出、分叉、存为预设；主机不支持导入时不给导入`` () =
    let input = { Render.inputs.Head with hasConversation = false; supportsImport = false }
    let ids = ShellPalette.items input |> List.map _.id
    for missing in [ "act:export"; "act:fork"; "act:save-preset"; "act:import" ] do
        Assert.DoesNotContain(missing, ids)
    Assert.Contains("act:new", ids)

[<Fact>]
let ``选中条目：设置项落到分区，对话 id 坏了就什么都不做`` () =
    Assert.Equal(PaletteCommand.OpenSettings "devices", ShellPalette.commandOf "set:devices:配对新设备")
    Assert.Equal(PaletteCommand.Ignore, ShellPalette.commandOf "conv:x")
    Assert.True(ShellPalette.closesDrawer (PaletteCommand.Action "model"))
    Assert.False(ShellPalette.closesDrawer (PaletteCommand.Action "theme"))

// ---- 最近使用的文件读写（迁自旧宿主 PaletteRecent.loadRecent / saveRecent；WANXIANG_HOME 决定落点目录）----
[<Fact>]
let ``palette recent persists across save and load, and bad files degrade to empty`` () =
    let home = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wx2-pal-" + System.Guid.NewGuid().ToString "N")
    System.Environment.SetEnvironmentVariable("WANXIANG_HOME", home)
    try
        Assert.Empty(PaletteRecentFile.loadRecent ())
        PaletteRecentFile.saveRecent [ "act:new"; "conv:1" ]
        Assert.Equal<string list>([ "act:new"; "conv:1" ], PaletteRecentFile.loadRecent ())
        PaletteRecentFile.saveRecent [ "act:theme" ]
        Assert.Equal<string list>([ "act:theme" ], PaletteRecentFile.loadRecent ())
        System.IO.File.WriteAllText(PaletteRecentFile.recentFile (), "{bad")
        Assert.Empty(PaletteRecentFile.loadRecent ())
    finally
        System.Environment.SetEnvironmentVariable("WANXIANG_HOME", null)
