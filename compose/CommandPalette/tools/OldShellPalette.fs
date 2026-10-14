namespace Wanxiang.UI

open System

// 原样摘自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs：PaletteItems 与 RunPaletteItem 的方法体（由脚本逐行复制，只去掉一层缩进）。
// 方法体读到的 MainView 字段与协作者在下面按旧定义给出同名的值：类型照抄旧 CommandPalette / ConversationSummary，
// SettingsSection 照抄旧 Views/SettingsView.fs，HostFeatures.supports 照抄 Wire；副作用（打开、切换、toast）换成记下 PaletteCommand。

type PaletteKind =
    | PaletteConversation
    | PaletteAction
    | PaletteSetting

type PaletteItem = { id: string; kind: PaletteKind; title: string; subtitle: string; keywords: string list; shortcut: string option }

type ConversationSummary = {
    id: Guid; title: string; preview: string; running: bool; pinned: bool; archived: bool
    createdAt: DateTimeOffset; updatedAt: DateTimeOffset; messageCount: int; isFork: bool
    providerId: string; model: string; lastCommitId: uint64 }

type SettingsSection =
    | Providers
    | Tools
    | Generation
    | Devices
    | Appearance
    | About

module SettingsSection =
    let groups =
        [ "主机", [ Providers; Tools; Generation; Devices ]
          "这台客户端", [ Appearance; About ] ]
    let all = groups |> List.collect snd
    let scope = function
        | Providers | Tools | Generation | Devices -> "主机"
        | Appearance | About -> "这台客户端"
    let label (section: SettingsSection) =
        match section with
        | Providers -> "模型服务"
        | Tools -> "工具"
        | Generation -> "对话默认值"
        | Devices -> "设备"
        | Appearance -> "外观与操作"
        | About -> "关于"

type ShellPaletteInput = {
    hasConversation: bool
    presets: (string * string * string) list
    supportsPresets: bool
    supportsImport: bool
    themeLabel: string
    sections: (string * string * string) list
    conversations: ConversationSummary list
    showArchived: bool
}

[<RequireQualifiedAccess>]
type PaletteCommand =
    | SearchMessages
    | OpenConversation of Guid
    | OpenSettings of sectionKey: string
    | ApplyPreset of presetId: string
    | Action of name: string
    | Ignore

type private OldPreset = { id: string; name: string; description: string }

module private CommandPalette =
    let fullTextId = "act:search-messages"
    let pushRecent (id: string) (recent: string list) = id :: recent

module private PaletteRecent =
    let saveRecent (_: string list) = ()

module private MessageView =
    let isUser (_: {| text: string |}) = true

module private Dialogs =
    let shortcuts (_: unit) = ()

type private Tone = | Warning

module private HostFeatures =
    let Presets = "presets"
    let ConversationImport = "conversation-import"
    let supports (known: Set<string> option) (feature: string) =
        match known with
        | None -> true
        | Some set -> set.Contains feature

type private OldShell(input: ShellPaletteInput) =
    let activeConvId = if input.hasConversation then Some Guid.Empty else None
    let catalog = {| presets = [ for (i, n, d) in input.presets -> { id = i; name = n; description = d } ] |}
    let hostFeatures =
        Some(set [ if input.supportsPresets then HostFeatures.Presets
                   if input.supportsImport then HostFeatures.ConversationImport ])
    let prefs = {| theme = input.themeLabel; showArchived = input.showArchived |}
    let summaries = input.conversations
    let mutable paletteRecent: string list = []
    let mutable command = PaletteCommand.Ignore
    let mutable drawerClosed = false
    let navigation = {| State = {| compactMode = true; compactNavigationOpen = true |} |}
    let chat = {| HeaderModelVisible = false; HeaderModelAnchor = () |}
    let composer = {| ModelAnchor = fun () -> () |}
    let overlay = ()
    let toast (_: string) (_: Tone) = command <- PaletteCommand.Ignore
    let activeSummary () = Some()
    let activeConversation () = Some()
    let messagesOf () : {| text: string |} list = []
    let settings = {| ShowSection = fun (s: SettingsSection) -> command <- PaletteCommand.OpenSettings((string s).ToLowerInvariant()) |}
    member _.Command = command
    member _.DrawerClosed = drawerClosed
    member _.SetCompactNavigation(_: bool) = drawerClosed <- true
    member _.FocusSidebarSearch(_: string option) = command <- PaletteCommand.SearchMessages
    member _.OpenConversation(id: Guid) = command <- PaletteCommand.OpenConversation id
    member _.ShowSettings() = command <- PaletteCommand.Action "settings"
    member _.CreateConversation() = command <- PaletteCommand.Action "new"
    member _.ApplyPreset(p: OldPreset) = command <- PaletteCommand.ApplyPreset p.id
    member _.SaveConversationAsPreset() = command <- PaletteCommand.Action "save-preset"
    member _.ShowModelPicker(_: unit) = command <- PaletteCommand.Action "model"
    member _.ExportConversation(_: unit) = command <- PaletteCommand.Action "export"
    member _.ForkFrom(_: uint64 option, _: string) = command <- PaletteCommand.Action "fork"
    member _.CopyLatestReply() = command <- PaletteCommand.Action "copy-reply"
    member _.RegenerateLatest() = command <- PaletteCommand.Action "regenerate"
    member _.ImportConversation() = command <- PaletteCommand.Action "import"
    member _.CycleTheme() = command <- PaletteCommand.Action "theme"
    member _.ToggleSidebar() = command <- PaletteCommand.Action "sidebar"
    member _.ShowAddProvider() = command <- PaletteCommand.Action "add-provider"

    member this.PaletteItems() : PaletteItem list =
        let act id title subtitle keywords shortcut =
            { id = "act:" + id; kind = PaletteAction; title = title; subtitle = subtitle; keywords = keywords; shortcut = shortcut }
        let hasConv = activeConvId.IsSome
        let actions =
            [ act "new" "新建对话" "" [ "new"; "chat"; "xinjian" ] (Some "Ctrl / ⌘ + N")
              act "model" "切换模型" (if hasConv then "本对话" else "新对话使用的模型") [ "model"; "switch" ] (Some "Ctrl / ⌘ + M")
              if hasConv then
                  act "export" "导出当前对话" "Markdown 或 JSON" [ "export"; "download" ] (Some "Ctrl / ⌘ + Shift + E")
                  act "fork" "从最新提问分叉" "编辑后另开一个分叉" [ "fork"; "branch"; "分支" ] None
                  act "copy-reply" "复制最新回复" "" [ "copy" ] (Some "Ctrl / ⌘ + Shift + C")
                  act "regenerate" "重新生成最新回复" "" [ "regenerate"; "retry"; "重试" ] (Some "Ctrl / ⌘ + Shift + G")
              for p in catalog.presets do
                  { id = "preset:" + p.id; kind = PaletteAction; title = "用预设新建：" + p.name
                    subtitle = p.description; keywords = [ "preset"; "yushe"; "ys"; p.name ]; shortcut = None }
              if hasConv && HostFeatures.supports hostFeatures HostFeatures.Presets then
                  act "save-preset" "存为预设" "记下本对话的模型、指令、参数和工具" [ "preset"; "save"; "yushe" ] None
              if HostFeatures.supports hostFeatures HostFeatures.ConversationImport then
                act "import" "导入对话" "从导出的 JSON 文件" [ "import"; "upload" ] None
              act "theme" "切换主题" (sprintf "当前：%s" (prefs.theme)) [ "theme"; "dark"; "light"; "深色"; "浅色" ] (Some "Ctrl / ⌘ + Shift + S")
              act "sidebar" "显示 / 隐藏侧栏" "" [ "sidebar" ] (Some "Ctrl / ⌘ + B")
              act "shortcuts" "键盘快捷键" "" [ "shortcuts"; "keyboard"; "help"; "帮助" ] (Some "F1")
              act "settings" "打开设置" "" [ "settings"; "preferences" ] (Some "Ctrl / ⌘ + ,")
              act "add-provider" "添加模型服务" "" [ "provider"; "api key"; "密钥" ] None ]
        let sectionItems =
            SettingsSection.all
            |> List.map (fun section ->
                { id = "set:" + (string section).ToLowerInvariant()
                  kind = PaletteSetting
                  title = "设置 › " + SettingsSection.label section
                  subtitle = SettingsSection.scope section
                  keywords = [ string section; SettingsSection.label section ]
                  shortcut = None })
        let settingItems =
            [ "appearance", "主题", [ "theme" ]
              "appearance", "消息字号", [ "font"; "字体" ]
              "appearance", "发送键（Enter / Ctrl / ⌘ + Enter）", [ "enter"; "send" ]
              "appearance", "减弱动态效果", [ "motion"; "动画" ]
              "appearance", "本机保存草稿", [ "draft" ]
              "appearance", "代码块折行", [ "wrap"; "code" ]
              "generation", "默认模型与温度", [ "temperature"; "default model" ]
              "generation", "导入对话", [ "import" ]
              "devices", "配对新设备", [ "pair"; "qr"; "二维码" ]
              "tools", "工具审批", [ "approval"; "tool" ] ]
            |> List.map (fun (section, title, keywords) ->
                { id = "set:" + section + ":" + title
                  kind = PaletteSetting
                  title = title
                  subtitle = "设置 › " + (SettingsSection.all |> List.find (fun s -> (string s).ToLowerInvariant() = section) |> SettingsSection.label)
                  keywords = keywords
                  shortcut = None })
        let conversations =
            summaries
            |> List.filter (fun s -> not s.archived || prefs.showArchived)
            |> List.map (fun s ->
                { id = "conv:" + string s.id
                  kind = PaletteConversation
                  title = (if String.IsNullOrWhiteSpace s.title then "新对话" else s.title)
                  subtitle =
                      let tags =
                          [ (s.pinned, "已置顶"); (s.archived, "已归档"); (s.isFork, "分叉") ]
                          |> List.filter fst |> List.map snd
                      String.Join(" · ", tags @ [ s.updatedAt.ToLocalTime().ToString("M月d日 HH:mm") ])
                  keywords = []
                  shortcut = None })
        actions @ sectionItems @ settingItems @ conversations

    member this.RunPaletteItem(item: PaletteItem, query: string) =
        paletteRecent <- CommandPalette.pushRecent item.id paletteRecent
        PaletteRecent.saveRecent paletteRecent
        let closeDrawer () =
            if navigation.State.compactMode && navigation.State.compactNavigationOpen then this.SetCompactNavigation false
        let sectionOf (key: string) =
            SettingsSection.all |> List.tryFind (fun s -> (string s).ToLowerInvariant() = key)
        match item.id with
        | id when id = CommandPalette.fullTextId -> this.FocusSidebarSearch(Some(query.Trim()))
        | id when id.StartsWith "conv:" ->
            match Guid.TryParse(id.Substring 5) with
            | true, convId -> closeDrawer (); this.OpenConversation convId
            | _ -> ()
        | id when id.StartsWith "set:" ->
            let key = id.Substring(4).Split(':')[0]
            closeDrawer ()
            this.ShowSettings()
            sectionOf key |> Option.iter settings.ShowSection
        | "act:new" -> closeDrawer (); this.CreateConversation() |> ignore
        | id when id.StartsWith "preset:" ->
            match catalog.presets |> List.tryFind (fun p -> "preset:" + p.id = id) with
            | Some p -> closeDrawer (); this.ApplyPreset p
            | None -> toast "这个预设已经不存在。" Warning
        | "act:save-preset" -> this.SaveConversationAsPreset()
        | "act:model" ->
            closeDrawer ()
            this.ShowModelPicker(if chat.HeaderModelVisible then chat.HeaderModelAnchor else composer.ModelAnchor())
        | "act:export" ->
            match activeSummary () with
            | Some summary -> this.ExportConversation summary
            | None -> toast "没有打开的对话可导出" Warning
        | "act:fork" ->
            match activeConversation () with
            | Some view ->
                let seed = messagesOf view |> List.filter MessageView.isUser |> List.tryLast |> Option.map (fun m -> m.text) |> Option.defaultValue ""
                this.ForkFrom(None, seed)
            | None -> toast "当前没有可分叉的对话。" Warning
        | "act:copy-reply" -> this.CopyLatestReply()
        | "act:regenerate" -> closeDrawer (); this.RegenerateLatest()
        | "act:import" -> this.ImportConversation()
        | "act:theme" -> this.CycleTheme()
        | "act:sidebar" -> this.ToggleSidebar()
        | "act:shortcuts" -> Dialogs.shortcuts overlay
        | "act:settings" -> closeDrawer (); this.ShowSettings()
        | "act:add-provider" -> closeDrawer (); this.ShowAddProvider()
        | _ -> ()

    member this.Shortcuts() = Dialogs.shortcuts overlay

/// 与新组合者同名的入口：每次用一个新的 OldShell 跑旧方法体。
module ShellPalette =
    let items (input: ShellPaletteInput) : PaletteItem list = OldShell(input).PaletteItems()

    let run (input: ShellPaletteInput) (id: string) : PaletteCommand * bool =
        let shell = OldShell(input)
        shell.RunPaletteItem({ id = id; kind = PaletteAction; title = ""; subtitle = ""; keywords = []; shortcut = None }, "  q  ")
        let c = shell.Command
        // 旧 "act:shortcuts" 直接开对话框，不经成员；按 id 记下
        (if id = "act:shortcuts" then PaletteCommand.Action "shortcuts" else c), shell.DrawerClosed
