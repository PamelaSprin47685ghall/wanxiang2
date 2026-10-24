namespace Wanxiang.UI

open System

/// 命令面板条目类别（决策 370）。
type PaletteKind =
    | PaletteConversation
    | PaletteAction
    | PaletteSetting

type PaletteItem =
    {
        /// 稳定 id，最近使用按它记；对话为 "conv:<guid>"，动作为 "act:<name>"，设置为 "set:<name>"。
        id: string
        kind: PaletteKind
        title: string
        subtitle: string
        /// 额外检索词（同义词、英文名），不显示。
        keywords: string list
        /// 对应的全局快捷键文字（决策 372：面板即快捷键的发现面）。
        shortcut: string option
    }

/// 命令面板的检索与排序（组合者）：标题/关键词的子序列打分归 FuzzyMatch，汉字首字母归 PinyinInitials；
/// 本模块安排两者的取舍、类别与最近使用加权。最近使用的读写（文件位置、本地文本）仍归宿主。
module CommandPalette =

    let maxRecent = 8
    let maxResults = 50

    let fuzzyScore (query: string) (text: string) : int option = FuzzyMatch.score query text

    /// 标题 / 关键词 / 标题的拼音首字母取最高分。拼音首字母只对 ASCII 查询生效。
    let score (query: string) (item: PaletteItem) : int option =
        let q = query.Trim()
        let asciiQuery = q |> Seq.forall (fun c -> c < '\u0080')

        let candidates =
            [
                yield fuzzyScore q item.title |> Option.map (fun s -> s + 50)
                for k in item.keywords do
                    yield fuzzyScore q k
                if asciiQuery then
                    let ini = PinyinInitials.initials item.title
                    // 首字母只认前缀/连续命中，避免「d」这种单字母把半个列表都拉进来时乱序。
                    if ini <> "" && ini.Contains(q.ToLowerInvariant().Replace(" ", "")) then
                        yield Some(800 - ini.IndexOf(q.ToLowerInvariant().Replace(" ", "")) * 5 - ini.Length)
                    // 中英混排（「Rust 学习笔记」→ rsxx）退回子序列，分数低于连续命中。
                    elif ini <> "" && q.Length >= 2 then
                        yield fuzzyScore q ini |> Option.map (fun s -> min s 600)
            ]

        candidates
        |> List.choose id
        |> function
            | [] -> None
            | xs -> Some(List.max xs)

    let private kindWeight =
        function
        | PaletteAction -> 30
        | PaletteSetting -> 20
        | PaletteConversation -> 0

    /// 空查询：最近使用（按新到旧）在前，其后是全部动作；有查询：按分数排序，最近使用加权。
    let rank (query: string) (recent: string list) (items: PaletteItem list) : PaletteItem list =
        let byId = items |> List.map (fun i -> i.id, i) |> Map.ofList

        if String.IsNullOrWhiteSpace query then
            let recents = recent |> List.choose (fun id -> Map.tryFind id byId)
            let ids = recents |> List.map _.id |> Set.ofList

            let actions =
                items
                |> List.filter (fun i -> i.kind = PaletteAction && not (ids.Contains i.id))

            recents @ actions |> List.truncate maxResults
        else
            items
            |> List.choose (fun i ->
                score query i
                |> Option.map (fun s ->
                    let r =
                        recent
                        |> List.tryFindIndex ((=) i.id)
                        |> Option.map (fun ix -> 200 - ix * 20)
                        |> Option.defaultValue 0

                    i, s + r + kindWeight i.kind))
            |> List.sortByDescending snd
            |> List.map fst
            |> List.truncate maxResults

    let pushRecent (id: string) (recent: string list) : string list =
        id :: (recent |> List.filter ((<>) id)) |> List.truncate maxRecent

    /// 面板行右侧的紧凑键位：「Ctrl / ⌘ + Shift + E」→「Ctrl+Shift+E」。
    /// 等宽字体缺 ⌘ 字形（r20 截图里是方框），且行内空间有限；macOS 上 Ctrl 同样由 ⌘ 触发，帮助页仍写全。
    let compactKeys (keys: string) =
        keys.Replace(" / ⌘", "").Replace(" + ", "+")

    let kindLabel =
        function
        | PaletteConversation -> "对话"
        | PaletteAction -> "操作"
        | PaletteSetting -> "设置"

    /// 决策 371：面板只按标题找对话；正文全文检索仍归侧栏搜索，面板末尾给一条交接。
    let fullTextId = "act:search-messages"
    let fullTextTitle (query: string) = sprintf "在全部消息中搜索「%s」" (query.Trim())


/// 命令面板「最近使用」的落盘格式（旧 `Model/CommandPalette.fs` 的 PaletteRecent）。读写文件 / localStorage 归宿主（LocalText），这里只管字节。
module PaletteRecent =

    /// 与 ui.json 同目录的文件名。
    let fileName = "palette-recent.json"

    /// 解析；坏文件、非字符串数组一律当作没有记录，超过 maxRecent 截断。
    let decode (text: string) : string list =
        try
            match System.Text.Json.Nodes.JsonNode.Parse(text) with
            | :? System.Text.Json.Nodes.JsonArray as arr ->
                [
                    for node in arr do
                        if isNull node then
                            yield null
                        else
                            yield node.GetValue<string>()
                ]
                |> List.truncate CommandPalette.maxRecent
            | _ -> []
        with _ ->
            []

    let encode (recent: string list) : string =
        let arr = System.Text.Json.Nodes.JsonArray()

        for s in recent do
            arr.Add(System.Text.Json.Nodes.JsonValue.Create(s) :> System.Text.Json.Nodes.JsonNode)

        arr.ToJsonString()

/// 壳层命令面板条目的输入：都是 AppShell 手里的事实，按值传入。
type ShellPaletteInput =
    {
        hasConversation: bool
        /// 预设：(id, 名称, 一句话说明)。说明由 Catalog 的 Presets.describe 给出。
        presets: (string * string * string) list
        /// 主机声明支持预设 / 导入（HostFeatures.supports 的结果）。
        supportsPresets: bool
        supportsImport: bool
        /// 当前主题的说法（ThemePreference.label）。
        themeLabel: string
        /// 设置分区，按设置页顺序：(分区名，即 SettingsSection 的 case 名, 名称, 范围)。
        sections: (string * string * string) list
        conversations: ConversationSummary list
        showArchived: bool
    }

/// 选中条目后要做的事（旧 AppShell.RunPaletteItem 按 id 分派）。
[<RequireQualifiedAccess>]
type PaletteCommand =
    | SearchMessages
    | OpenConversation of Guid
    | OpenSettings of sectionKey: string
    | ApplyPreset of presetId: string
    /// "act:<name>" 的 name：new、save-preset、model、export、fork、copy-reply、regenerate、import、theme、sidebar、shortcuts、settings、add-provider。
    | Action of name: string
    | Ignore

/// 壳层命令面板（决策 370–375）：条目清单与选中后的分派，迁自旧 AppShell.PaletteItems / RunPaletteItem。
module ShellPalette =

    let private actionNames =
        set
            [
                "new"
                "save-preset"
                "model"
                "export"
                "fork"
                "copy-reply"
                "regenerate"
                "import"
                "theme"
                "sidebar"
                "shortcuts"
                "settings"
                "add-provider"
            ]

    let items (input: ShellPaletteInput) : PaletteItem list =
        let act id title subtitle keywords shortcut =
            {
                id = "act:" + id
                kind = PaletteAction
                title = title
                subtitle = subtitle
                keywords = keywords
                shortcut = shortcut
            }

        let hasConv = input.hasConversation

        let actions =
            [
                act "new" "新建对话" "" [ "new"; "chat"; "xinjian" ] (Some "Ctrl / ⌘ + N")
                act "model" "切换模型" (if hasConv then "本对话" else "新对话使用的模型") [ "model"; "switch" ] (Some "Ctrl / ⌘ + M")
                if hasConv then
                    act "export" "导出当前对话" "Markdown 或 JSON" [ "export"; "download" ] (Some "Ctrl / ⌘ + Shift + E")
                    act "fork" "从最新提问分叉" "编辑后另开一个分叉" [ "fork"; "branch"; "分支" ] None
                    act "copy-reply" "复制最新回复" "" [ "copy" ] (Some "Ctrl / ⌘ + Shift + C")
                    act "regenerate" "重新生成最新回复" "" [ "regenerate"; "retry"; "重试" ] (Some "Ctrl / ⌘ + Shift + G")
                for (pid, name, description) in input.presets do
                    {
                        id = "preset:" + pid
                        kind = PaletteAction
                        title = "用预设新建：" + name
                        subtitle = description
                        keywords = [ "preset"; "yushe"; "ys"; name ]
                        shortcut = None
                    }
                if hasConv && input.supportsPresets then
                    act "save-preset" "存为预设" "记下本对话的模型、指令、参数和工具" [ "preset"; "save"; "yushe" ] None
                if input.supportsImport then
                    act "import" "导入对话" "从导出的 JSON 文件" [ "import"; "upload" ] None
                act
                    "theme"
                    "切换主题"
                    (sprintf "当前：%s" input.themeLabel)
                    [ "theme"; "dark"; "light"; "深色"; "浅色" ]
                    (Some "Ctrl / ⌘ + Shift + S")
                act "sidebar" "显示 / 隐藏侧栏" "" [ "sidebar" ] (Some "Ctrl / ⌘ + B")
                act "shortcuts" "键盘快捷键" "" [ "shortcuts"; "keyboard"; "help"; "帮助" ] (Some "F1")
                act "settings" "打开设置" "" [ "settings"; "preferences" ] (Some "Ctrl / ⌘ + ,")
                act "add-provider" "添加模型服务" "" [ "provider"; "api key"; "密钥" ] None
            ]

        let sectionItems =
            input.sections
            |> List.map (fun (name: string, label, scope) ->
                {
                    id = "set:" + name.ToLowerInvariant()
                    kind = PaletteSetting
                    title = "设置 › " + label
                    subtitle = scope
                    keywords = [ name; label ]
                    shortcut = None
                })

        let labelOf section =
            input.sections
            |> List.find (fun (n: string, _, _) -> n.ToLowerInvariant() = section)
            |> fun (_, l, _) -> l

        let settingItems =
            [
                "appearance", "主题", [ "theme" ]
                "appearance", "消息字号", [ "font"; "字体" ]
                "appearance", "发送键（Enter / Ctrl / ⌘ + Enter）", [ "enter"; "send" ]
                "appearance", "减弱动态效果", [ "motion"; "动画" ]
                "appearance", "本机保存草稿", [ "draft" ]
                "appearance", "代码块折行", [ "wrap"; "code" ]
                "generation", "默认模型与温度", [ "temperature"; "default model" ]
                "generation", "导入对话", [ "import" ]
                "devices", "配对新设备", [ "pair"; "qr"; "二维码" ]
                "tools", "工具审批", [ "approval"; "tool" ]
            ]
            |> List.map (fun (section, title, keywords) ->
                {
                    id = "set:" + section + ":" + title
                    kind = PaletteSetting
                    title = title
                    subtitle = "设置 › " + labelOf section
                    keywords = keywords
                    shortcut = None
                })

        let conversations =
            input.conversations
            |> List.filter (fun s -> not s.archived || input.showArchived)
            |> List.map (fun s ->
                {
                    id = "conv:" + string s.id
                    kind = PaletteConversation
                    title = (if String.IsNullOrWhiteSpace s.title then "新对话" else s.title)
                    subtitle =
                        let tags =
                            [ (s.pinned, "已置顶"); (s.archived, "已归档"); (s.isFork, "分叉") ]
                            |> List.filter fst
                            |> List.map snd

                        String.Join(" · ", tags @ [ s.updatedAt.ToLocalTime().ToString("M月d日 HH:mm") ])
                    keywords = []
                    shortcut = None
                })

        actions @ sectionItems @ settingItems @ conversations

    /// 条目 id → 要做的事。
    let commandOf (id: string) : PaletteCommand =
        match id with
        | id when id = CommandPalette.fullTextId -> PaletteCommand.SearchMessages
        | id when id.StartsWith "conv:" ->
            match Guid.TryParse(id.Substring 5) with
            | true, convId -> PaletteCommand.OpenConversation convId
            | _ -> PaletteCommand.Ignore
        | id when id.StartsWith "set:" -> PaletteCommand.OpenSettings(id.Substring(4).Split(':')[0])
        | "act:new" -> PaletteCommand.Action "new"
        | id when id.StartsWith "preset:" -> PaletteCommand.ApplyPreset(id.Substring 7)
        | id when id.StartsWith "act:" && actionNames.Contains(id.Substring 4) -> PaletteCommand.Action(id.Substring 4)
        | _ -> PaletteCommand.Ignore

    /// 执行前要不要先收起窄屏抽屉（旧代码在这些分支里先 closeDrawer）。预设已不存在时不收。
    let closesDrawer (command: PaletteCommand) =
        match command with
        | PaletteCommand.OpenConversation _
        | PaletteCommand.OpenSettings _
        | PaletteCommand.ApplyPreset _ -> true
        | PaletteCommand.Action("new" | "model" | "regenerate" | "settings" | "add-provider") -> true
        | _ -> false
