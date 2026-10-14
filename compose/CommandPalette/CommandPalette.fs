namespace Wanxiang.UI

open System

/// 命令面板条目类别（决策 370）。
type PaletteKind =
    | PaletteConversation
    | PaletteAction
    | PaletteSetting

type PaletteItem = {
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
            [ yield fuzzyScore q item.title |> Option.map (fun s -> s + 50)
              for k in item.keywords do yield fuzzyScore q k
              if asciiQuery then
                  let ini = PinyinInitials.initials item.title
                  // 首字母只认前缀/连续命中，避免「d」这种单字母把半个列表都拉进来时乱序。
                  if ini <> "" && ini.Contains(q.ToLowerInvariant().Replace(" ", "")) then
                      yield Some(800 - ini.IndexOf(q.ToLowerInvariant().Replace(" ", "")) * 5 - ini.Length)
                  // 中英混排（「Rust 学习笔记」→ rsxx）退回子序列，分数低于连续命中。
                  elif ini <> "" && q.Length >= 2 then
                      yield fuzzyScore q ini |> Option.map (fun s -> min s 600) ]
        candidates |> List.choose id |> function [] -> None | xs -> Some(List.max xs)

    let private kindWeight = function
        | PaletteAction -> 30
        | PaletteSetting -> 20
        | PaletteConversation -> 0

    /// 空查询：最近使用（按新到旧）在前，其后是全部动作；有查询：按分数排序，最近使用加权。
    let rank (query: string) (recent: string list) (items: PaletteItem list) : PaletteItem list =
        let byId = items |> List.map (fun i -> i.id, i) |> Map.ofList
        if String.IsNullOrWhiteSpace query then
            let recents = recent |> List.choose (fun id -> Map.tryFind id byId)
            let ids = recents |> List.map _.id |> Set.ofList
            let actions = items |> List.filter (fun i -> i.kind = PaletteAction && not (ids.Contains i.id))
            recents @ actions |> List.truncate maxResults
        else
            items
            |> List.choose (fun i ->
                score query i
                |> Option.map (fun s ->
                    let r = recent |> List.tryFindIndex ((=) i.id) |> Option.map (fun ix -> 200 - ix * 20) |> Option.defaultValue 0
                    i, s + r + kindWeight i.kind))
            |> List.sortByDescending snd
            |> List.map fst
            |> List.truncate maxResults

    let pushRecent (id: string) (recent: string list) : string list =
        id :: (recent |> List.filter ((<>) id)) |> List.truncate maxRecent

    /// 面板行右侧的紧凑键位：「Ctrl / ⌘ + Shift + E」→「Ctrl+Shift+E」。
    /// 等宽字体缺 ⌘ 字形（r20 截图里是方框），且行内空间有限；macOS 上 Ctrl 同样由 ⌘ 触发，帮助页仍写全。
    let compactKeys (keys: string) = keys.Replace(" / ⌘", "").Replace(" + ", "+")

    let kindLabel = function
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
            Text.Json.JsonSerializer.Deserialize<string[]>(text)
            |> Option.ofObj |> Option.map (List.ofArray >> List.truncate CommandPalette.maxRecent) |> Option.defaultValue []
        with _ -> []

    let encode (recent: string list) : string = Text.Json.JsonSerializer.Serialize(Array.ofList recent)
