namespace Wanxiang2.PaletteDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.ImeShield
open Wanxiang2.OverlayDesk
open Wanxiang2.PalettePanel

/// 命令面板（决策 370–379）的接线：条目与最近使用每次输入都重新取并经 CommandPalette.rank 排序，
/// 有查询时在末尾追加「在全部消息中搜索」交接行；放进对话层（宽 = Metric.PaletteDialogWidth，搜索框初始焦点）；
/// 执行时先关面板，触发 `Run`。
/// 中文输入法组合期间的回车 / 方向键归输入法：构造时装上全局 ImeShield（幂等）。
type PaletteDesk(dialogWidth: float, overlay: OverlayDesk, prefersTouch: unit -> bool) =
    do ImeShield.install ()
    let run = Event<PaletteItem * string>()
    let mutable paletteOpen = false

    let fullTextItem (q: string) =
        {
            id = CommandPalette.fullTextId
            kind = PaletteAction
            title = CommandPalette.fullTextTitle q
            subtitle = "交给侧栏搜索，查找消息正文"
            keywords = []
            shortcut = Some "Ctrl / ⌘ + Shift + F"
        }

    new(theme: ThemeManager, overlay: OverlayDesk, prefersTouch: unit -> bool) =
        PaletteDesk((theme.Metric Metric.PaletteDialogWidth).Value, overlay, prefersTouch)

    /// 只建内容（不进对话层）；`execute` 收到所选条目与当时的输入。返回 (内容, 搜索框)。
    member _.Build
        (items: unit -> PaletteItem list, recent: unit -> string list, execute: PaletteItem -> string -> unit)
        : Control * TextBox =
        let shown = Collections.Generic.Dictionary<string, PaletteItem>()

        let query (q: string) =
            let recentIds = recent ()
            let ranked = CommandPalette.rank q recentIds (items ())

            let all =
                if String.IsNullOrWhiteSpace q then
                    ranked
                else
                    ranked @ [ fullTextItem q ]

            let recentSet = Set.ofList recentIds
            shown.Clear()

            for i in all do
                shown[i.id] <- i

            {
                Rows =
                    all
                    |> List.map (fun i ->
                        {
                            Id = i.id
                            Title = i.title
                            Subtitle = i.subtitle
                            KindLabel = CommandPalette.kindLabel i.kind
                            Shortcut = i.shortcut
                            Recent = recentSet.Contains i.id
                        })
                MatchCount = ranked.Length
                HasRecent = not recentSet.IsEmpty
            }

        let slots =
            {
                Query = query
                CompactKeys = CommandPalette.compactKeys
                PrefersTouch = prefersTouch
            }

        PalettePanel.build slots (fun row q ->
            match shown.TryGetValue row.Id with
            | true, item -> execute item q
            | _ -> ())

    /// Ctrl/⌘+K：自己开着就关掉；别的对话框开着时不动它；只有浮层时先关浮层再开。
    member this.Show(items: unit -> PaletteItem list, recent: unit -> string list) =
        if paletteOpen then
            overlay.CloseDialog()
        elif not overlay.Layer.IsDialogOpen then
            if overlay.Layer.IsPopupOpen then
                overlay.ClosePopup()

            let content, search =
                this.Build(
                    items,
                    recent,
                    fun item q ->
                        overlay.CloseDialog()
                        run.Trigger(item, q)
                )

            paletteOpen <- true

            overlay.Layer.ShowDialog(
                content,
                dialogWidth,
                onClosed = (fun () -> paletteOpen <- false),
                initialFocus = search,
                scrollWholeContent = false
            )

    /// 面板关闭后执行所选条目（条目, 当时的输入）；宿主据 ShellPalette.commandOf 分派
    [<CLIEvent>]
    member _.Run = run.Publish
