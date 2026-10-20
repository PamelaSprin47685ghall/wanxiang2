module Tests

open System
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

[<Fact>]
let ``空查询：最近使用按新到旧排在最前，其余只列操作，不列设置与对话`` () =
    // 最近使用里已不存在的条目直接丢掉；最近用过的操作不再在操作区重复出现。
    let ranked = CommandPalette.rank "" [ "act:theme"; "conv:gone" ] sample
    Assert.Equal<string list>([ "act:theme"; "act:new" ], ranked |> List.map _.id)
    Assert.DoesNotContain("set:appearance", ranked |> List.map _.id)
    Assert.DoesNotContain("conv:2", ranked |> List.map _.id)

// 落盘字节：期望值由旧程序集的 saveRecent / loadRecent 实际读写得到。
[<Fact>]
let ``palette recent file bytes match old load and save`` () =
    Assert.Equal("""["new","\u8BBE\u7F6E\u0022x\u0022","a"]""", PaletteRecent.encode [ "new"; "设置\"x\""; "a" ])
    Assert.Equal<string list>([ "a"; "b"; "c"; "d"; "e"; "f"; "g"; "h" ], PaletteRecent.decode """["a","b","c","d","e","f","g","h","i","j"]""")
    for bad in [ "null"; "{bad"; "[]"; """["x",1]""" ] do
        Assert.Empty(PaletteRecent.decode bad)

// ---- 壳层面板：给定输入事实，产出的条目清单与选中后的派发命令 ----

let private g (n: int) = Guid.Parse(sprintf "0192b4a0-0000-7000-8000-%012d" n)
let private at = DateTimeOffset(2026, 10, 14, 9, 5, 0, TimeSpan.Zero)

let private summary n title pinned archived isFork : ConversationSummary =
    { id = g n; title = title; preview = ""; running = false; pinned = pinned; archived = archived
      createdAt = at; updatedAt = at.AddDays(float n); messageCount = 1; isFork = isFork
      providerId = "p"; model = "m"; lastCommitId = 1UL }

let private sections =
    [ "Providers", "模型服务", "主机"; "Tools", "工具", "主机"; "Generation", "对话默认值", "主机"; "Devices", "设备", "主机"
      "Appearance", "外观与操作", "这台客户端"; "About", "关于", "这台客户端" ]

/// 有当前对话、主机支持预设与导入、主题为「跟随系统」、隐藏归档对话。
let private paletteInput =
    { hasConversation = true
      presets = [ "p1", "写作", "gpt · 温度 0.7"; "p2", "代码", "本机模型" ]
      supportsPresets = true
      supportsImport = true
      themeLabel = "跟随系统"
      sections = sections
      conversations = [ summary 1 "旅行" true false false; summary 2 "" false true true; summary 3 "归档分叉" true true true ]
      showArchived = false }

let private itemOf id (items: PaletteItem list) = items |> List.find (fun i -> i.id = id)

[<Fact>]
let ``壳层面板：条目顺序是动作、设置分区、设置项、对话`` () =
    let ids = ShellPalette.items paletteInput |> List.map _.id
    Assert.Equal<string list>(
        [ "act:new"; "act:model"; "act:export"; "act:fork"; "act:copy-reply"; "act:regenerate"
          "preset:p1"; "preset:p2"; "act:save-preset"; "act:import"; "act:theme"; "act:sidebar"
          "act:shortcuts"; "act:settings"; "act:add-provider"
          "set:providers"; "set:tools"; "set:generation"; "set:devices"; "set:appearance"; "set:about"
          "set:appearance:主题"; "set:appearance:消息字号"; "set:appearance:发送键（Enter / Ctrl / ⌘ + Enter）"
          "set:appearance:减弱动态效果"; "set:appearance:本机保存草稿"; "set:appearance:代码块折行"
          "set:generation:默认模型与温度"; "set:generation:导入对话"; "set:devices:配对新设备"; "set:tools:工具审批"
          "conv:0192b4a0-0000-7000-8000-000000000001" ],
        ids)

[<Fact>]
let ``壳层面板：动作条目带标题、说明、检索词与快捷键`` () =
    let items = ShellPalette.items paletteInput
    let newItem = itemOf "act:new" items
    Assert.Equal(PaletteAction, newItem.kind)
    Assert.Equal("新建对话", newItem.title)
    Assert.Equal("", newItem.subtitle)
    Assert.Equal<string list>([ "new"; "chat"; "xinjian" ], newItem.keywords)
    Assert.Equal(Some "Ctrl / ⌘ + N", newItem.shortcut)

    let model = itemOf "act:model" items
    Assert.Equal("本对话", model.subtitle)
    Assert.Equal(Some "Ctrl / ⌘ + M", model.shortcut)

    let theme = itemOf "act:theme" items
    Assert.Equal("当前：跟随系统", theme.subtitle)
    Assert.Equal<string list>([ "theme"; "dark"; "light"; "深色"; "浅色" ], theme.keywords)

    let preset = itemOf "preset:p1" items
    Assert.Equal("用预设新建：写作", preset.title)
    Assert.Equal("gpt · 温度 0.7", preset.subtitle)
    Assert.Equal<string list>([ "preset"; "yushe"; "ys"; "写作" ], preset.keywords)

[<Fact>]
let ``壳层面板：设置分区与设置项各带名称与范围`` () =
    let items = ShellPalette.items paletteInput
    let section = itemOf "set:providers" items
    Assert.Equal(PaletteSetting, section.kind)
    Assert.Equal("设置 › 模型服务", section.title)
    Assert.Equal("主机", section.subtitle)
    Assert.Equal<string list>([ "Providers"; "模型服务" ], section.keywords)

    let setting = itemOf "set:devices:配对新设备" items
    Assert.Equal(PaletteSetting, setting.kind)
    Assert.Equal("配对新设备", setting.title)
    Assert.Equal("设置 › 设备", setting.subtitle)
    Assert.Equal<string list>([ "pair"; "qr"; "二维码" ], setting.keywords)

    let client = itemOf "set:about" items
    Assert.Equal("设置 › 关于", client.title)
    Assert.Equal("这台客户端", client.subtitle)

[<Fact>]
let ``壳层面板：对话条目给标题与标记，空标题落成「新对话」`` () =
    let items = ShellPalette.items paletteInput
    let conv = itemOf "conv:0192b4a0-0000-7000-8000-000000000001" items
    Assert.Equal(PaletteConversation, conv.kind)
    Assert.Equal("旅行", conv.title)
    Assert.Empty(conv.keywords)
    // 副标题是「标记 · 本地时间」：标记按置顶 / 归档 / 分叉的次序。
    let tags = conv.subtitle.Split(" · ") |> Array.toList
    Assert.Equal("已置顶", tags.Head)
    Assert.Matches(@"\d+月\d+日 \d{2}:\d{2}$", conv.subtitle)

    // 归档对话默认不列出；打开「显示归档」后才有，空标题显示为「新对话」。
    let shown = ShellPalette.items { paletteInput with showArchived = true }
    Assert.Equal("新对话", (itemOf "conv:0192b4a0-0000-7000-8000-000000000002" shown).title)
    let archivedTags = (itemOf "conv:0192b4a0-0000-7000-8000-000000000002" shown).subtitle.Split(" · ") |> Array.toList
    Assert.Equal<string list>([ "已归档"; "分叉" ], archivedTags |> List.take 2)
    Assert.Equal("归档分叉", (itemOf "conv:0192b4a0-0000-7000-8000-000000000003" shown).title)

[<Fact>]
let ``374 没打开对话时不给导出、分叉、存为预设；主机不支持导入时不给导入`` () =
    let input = { paletteInput with hasConversation = false; supportsImport = false }
    let ids = ShellPalette.items input |> List.map _.id
    for missing in [ "act:export"; "act:fork"; "act:save-preset"; "act:import" ] do
        Assert.DoesNotContain(missing, ids)
    Assert.Contains("act:new", ids)

[<Fact>]
let ``壳层面板：没有当前对话时不给对话相关的动作，模型说明改成新对话`` () =
    let items = ShellPalette.items { paletteInput with hasConversation = false }
    let ids = items |> List.map _.id
    for missing in [ "act:export"; "act:fork"; "act:copy-reply"; "act:regenerate"; "act:save-preset" ] do
        Assert.DoesNotContain(missing, ids)
    Assert.Equal("新对话使用的模型", (itemOf "act:model" items).subtitle)
    // 主机不支持预设时也不给「存为预设」；导入仍看主机能力。
    let noPresets = ShellPalette.items { paletteInput with supportsPresets = false }
    Assert.DoesNotContain("act:save-preset", noPresets |> List.map _.id)
    let noImport = ShellPalette.items { paletteInput with supportsImport = false }
    Assert.DoesNotContain("act:import", noImport |> List.map _.id)

[<Fact>]
let ``壳层面板：选中条目后派发命令，全文检索、对话、设置、预设各走各的`` () =
    Assert.Equal(PaletteCommand.SearchMessages, ShellPalette.commandOf "act:search-messages")
    Assert.Equal(PaletteCommand.OpenConversation(g 1), ShellPalette.commandOf (sprintf "conv:%O" (g 1)))
    Assert.Equal(PaletteCommand.OpenSettings "providers", ShellPalette.commandOf "set:providers")
    Assert.Equal(PaletteCommand.OpenSettings "appearance", ShellPalette.commandOf "set:appearance:主题")
    Assert.Equal(PaletteCommand.OpenSettings "devices", ShellPalette.commandOf "set:devices:配对新设备")
    Assert.Equal(PaletteCommand.ApplyPreset "p1", ShellPalette.commandOf "preset:p1")
    Assert.Equal(PaletteCommand.Action "new", ShellPalette.commandOf "act:new")
    Assert.Equal(PaletteCommand.Action "add-provider", ShellPalette.commandOf "act:add-provider")

[<Fact>]
let ``壳层面板：坏对话 id、未知动作与未知前缀都什么都不做`` () =
    Assert.Equal(PaletteCommand.Ignore, ShellPalette.commandOf "conv:not-a-guid")
    Assert.Equal(PaletteCommand.Ignore, ShellPalette.commandOf "act:unknown")
    Assert.Equal(PaletteCommand.Ignore, ShellPalette.commandOf "zzz")

[<Fact>]
let ``壳层面板：哪些命令要收起窄屏抽屉`` () =
    for id in [ "act:new"; "act:model"; "act:regenerate"; "act:settings"; "act:add-provider" ] do
        Assert.True(ShellPalette.closesDrawer (ShellPalette.commandOf id), id)
    for id in [ "act:search-messages"; "act:save-preset"; "act:export"; "act:fork"; "act:copy-reply"; "act:import"; "act:theme" ] do
        Assert.False(ShellPalette.closesDrawer (ShellPalette.commandOf id), id)
    Assert.True(ShellPalette.closesDrawer (ShellPalette.commandOf (sprintf "conv:%O" (g 1))))
    Assert.True(ShellPalette.closesDrawer (ShellPalette.commandOf "set:providers"))
    Assert.True(ShellPalette.closesDrawer (ShellPalette.commandOf "preset:p1"))

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
