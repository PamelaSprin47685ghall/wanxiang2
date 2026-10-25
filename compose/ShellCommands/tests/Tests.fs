module Tests

open System
open Xunit
open Wanxiang.UI

/// 可调的事实 + 记录界面面收到的调用。
type private Rig() =
    let log = ResizeArray<string>()
    member val Overlay = false with get, set
    member val Dialog = false with get, set
    member val Settings = false with get, set
    member val Drawer = false with get, set
    member val Generating = false with get, set
    member val Conversation = true with get, set
    member val Order: string list = [] with get, set
    member val Presets: string list = [] with get, set
    member val Theme = FollowSystem with get, set
    member val Undo = false with get, set
    member val SavedRecent: string list = [] with get, set
    member _.Log = List.ofSeq log
    member _.Clear() = log.Clear()

    member this.Surface: ShellSurface =
        let hit name = fun () -> log.Add name

        {
            OverlayEscape = fun () -> this.Overlay
            IsDialogOpen = fun () -> this.Dialog
            IsPopupOpen = fun () -> false
            IsSettingsOpen = fun () -> this.Settings
            IsDrawerOpen = fun () -> this.Drawer
            IsGenerating = fun () -> this.Generating
            HasConversation = fun () -> this.Conversation
            VisibleOrder = fun () -> this.Order
            HasPreset = fun id -> List.contains id this.Presets
            PaletteInput =
                fun () ->
                    {
                        hasConversation = this.Conversation
                        presets = []
                        supportsPresets = false
                        supportsImport = false
                        themeLabel = "跟随系统"
                        sections =
                            [
                                "Providers", "模型服务", "主机"
                                "Tools", "工具", "主机"
                                "Generation", "对话默认值", "主机"
                                "Devices", "设备", "主机"
                                "Appearance", "外观与操作", "这台客户端"
                                "About", "关于", "这台客户端"
                            ]
                        conversations = []
                        showArchived = false
                    }
            Theme = fun () -> this.Theme
            CloseSettings = hit "close-settings"
            CloseDrawer = hit "close-drawer"
            StopGeneration = hit "stop"
            ToggleSidebar = hit "sidebar"
            CloseWindow =
                fun () ->
                    log.Add "close-window"
                    true
            NewConversation = hit "new"
            FocusSearch = fun q -> log.Add(sprintf "search %A" q)
            OpenSettings = fun key -> log.Add(sprintf "settings %A" key)
            OpenModelPicker = hit "model"
            JumpExchange = fun d -> log.Add(sprintf "jump %d" d)
            ScrollToBeginning = hit "top"
            ScrollToEnd = hit "end"
            FocusComposer = hit "composer"
            CopyLastReply = hit "copy"
            RegenerateLast = hit "regenerate"
            TryUndo =
                fun () ->
                    log.Add "undo"
                    this.Undo
            FocusToast = fun () -> false
            ExportConversation = hit "export"
            OpenConversation = fun id -> log.Add(sprintf "open %O" id)
            ShowShortcuts = hit "shortcuts"
            ApplyPreset = fun id -> log.Add("preset " + id)
            SavePreset = hit "save-preset"
            Fork = hit "fork"
            Import = hit "import"
            AddProvider = hit "add-provider"
            ShowPalette = fun items recent -> log.Add(sprintf "palette %d %A" (items ()).Length (recent ()))
            SaveRecent = fun r -> this.SavedRecent <- r
            SaveTheme =
                fun t ->
                    this.Theme <- t
                    log.Add(sprintf "theme %A" t)
            Toast = fun text tone -> log.Add(sprintf "toast %A %s" tone text)
        }

let private item id =
    {
        id = id
        kind = PaletteAction
        title = id
        subtitle = ""
        keywords = []
        shortcut = None
    }

// 迁自旧 AppShellTests（主题部分；偏好落盘与换色在宿主）。
[<Fact>]
let ``HandleShortcut ToggleTheme cycles through FollowSystem, AlwaysLight and AlwaysDark`` () =
    let rig = Rig()
    let shell = ShellCommands(rig.Surface, [])

    let seen =
        [
            for _ in 1..3 ->
                Assert.True(shell.HandleShortcut ShortcutAction.ToggleTheme)
                rig.Theme
        ]

    Assert.Equal<ThemePreference list>([ AlwaysLight; AlwaysDark; FollowSystem ], seen)
    Assert.Contains("toast Info 已切换为深色主题", rig.Log)

[<Fact>]
let ``HandleShortcut ToggleSidebar triggers sidebar collapse toggle`` () =
    let rig = Rig()
    Assert.True(ShellCommands(rig.Surface, []).HandleShortcut ShortcutAction.ToggleSidebar)
    Assert.Equal<string list>([ "sidebar" ], rig.Log)

[<Fact>]
let ``HandleShortcut OpenSettings shows settingsHost and hides workspace`` () =
    let rig = Rig()
    Assert.True(ShellCommands(rig.Surface, []).HandleShortcut ShortcutAction.OpenSettings)
    Assert.Equal<string list>([ "settings None" ], rig.Log)

[<Fact>]
let ``HandleShortcut FocusSearch ensures sidebar visible and focuses search`` () =
    let rig = Rig()
    Assert.True(ShellCommands(rig.Surface, []).HandleShortcut ShortcutAction.FocusSearch)
    Assert.Equal<string list>([ "search None" ], rig.Log)

[<Fact>]
let ``HandleShortcut StopGeneration when run is generating invokes generation cancellation`` () =
    let rig = Rig(Generating = true)
    let shell = ShellCommands(rig.Surface, [])
    Assert.True(shell.HandleShortcut ShortcutAction.Escape)
    rig.Generating <- false
    Assert.False(shell.HandleShortcut ShortcutAction.Escape)
    Assert.Equal<string list>([ "stop" ], rig.Log)

[<Fact>]
let ``Escape 依次落到浮层、设置页、抽屉`` () =
    let rig = Rig(Overlay = true, Settings = true, Drawer = true, Generating = true)
    let shell = ShellCommands(rig.Surface, [])
    Assert.True(shell.HandleShortcut ShortcutAction.Escape)
    rig.Overlay <- false
    Assert.True(shell.HandleShortcut ShortcutAction.Escape)
    rig.Settings <- false
    Assert.True(shell.HandleShortcut ShortcutAction.Escape)
    Assert.Equal<string list>([ "close-settings"; "close-drawer" ], rig.Log)

[<Fact>]
let ``HandleShortcut JumpExchange and Scroll dispatches smoothly without errors`` () =
    let rig = Rig()
    let shell = ShellCommands(rig.Surface, [])

    for a in
        [
            ShortcutAction.JumpExchange -1
            ShortcutAction.JumpExchange 1
            ShortcutAction.ScrollToBeginning
            ShortcutAction.ScrollToEnd
        ] do
        Assert.True(shell.HandleShortcut a)

    Assert.Equal<string list>([ "jump -1"; "jump 1"; "top"; "end" ], rig.Log)

[<Fact>]
let ``对话框开着时其余快捷键不接管`` () =
    let rig = Rig(Dialog = true)
    let shell = ShellCommands(rig.Surface, [])
    Assert.False(shell.HandleShortcut ShortcutAction.NewConversation)
    Assert.False(shell.HandleShortcut ShortcutAction.ToggleTheme)
    Assert.Empty(rig.Log)

[<Fact>]
let ``对话框开着时 Ctrl+K 仍交给面板`` () =
    let rig = Rig(Dialog = true)
    let shell = ShellCommands(rig.Surface, [ "act:new" ])
    Assert.True(shell.HandleShortcut ShortcutAction.OpenCommandPalette)
    Assert.StartsWith("palette ", rig.Log.Head)
    Assert.False(shell.HandleShortcut ShortcutAction.NewConversation)

[<Fact>]
let ``232 Ctrl+Z 映射到撤销，并只触发可见提示条上的「撤销」`` () =
    let rig = Rig()
    let shell = ShellCommands(rig.Surface, [])
    Assert.False(shell.HandleShortcut ShortcutAction.UndoPending)
    rig.Undo <- true
    Assert.True(shell.HandleShortcut ShortcutAction.UndoPending)

[<Fact>]
let ``Ctrl+1..9 按侧栏可见顺序打开，超出就不接管`` () =
    let id = Guid.NewGuid()
    let rig = Rig(Order = [ string id ])
    let shell = ShellCommands(rig.Surface, [])
    Assert.True(shell.HandleShortcut(ShortcutAction.OpenConversationAt 0))
    Assert.False(shell.HandleShortcut(ShortcutAction.OpenConversationAt 1))
    Assert.Equal<string list>([ sprintf "open %O" id ], rig.Log)

[<Fact>]
let ``没有打开的对话时导出只提示`` () =
    let rig = Rig(Conversation = false)
    Assert.True(ShellCommands(rig.Surface, []).HandleShortcut ShortcutAction.ExportConversation)
    Assert.Equal<string list>([ "toast Warning 没有打开的对话可导出" ], rig.Log)

[<Fact>]
let ``面板选中：记最近使用，收抽屉，失效预设只提示不收抽屉`` () =
    let rig = Rig(Drawer = true, Presets = [ "p1" ])
    let shell = ShellCommands(rig.Surface, [ "act:new" ])
    shell.RunPaletteItem(item "preset:gone", "")
    shell.RunPaletteItem(item "preset:p1", "")
    shell.RunPaletteItem(item "act:new", "")

    Assert.Equal<string list>(
        [
            "toast Warning 这个预设已经不存在。"
            "close-drawer"
            "preset p1"
            "close-drawer"
            "new"
        ],
        rig.Log
    )

    Assert.Equal<string list>([ "act:new"; "preset:p1"; "preset:gone" ], shell.Recent)
    Assert.Equal<string list>(shell.Recent, rig.SavedRecent)

[<Fact>]
let ``Ctrl+K 用现取的条目和最近使用打开面板`` () =
    let rig = Rig()
    Assert.True(ShellCommands(rig.Surface, [ "act:new" ]).HandleShortcut ShortcutAction.OpenCommandPalette)
    Assert.StartsWith("palette ", rig.Log.Head)
    Assert.EndsWith("[\"act:new\"]", rig.Log.Head)
