namespace Wanxiang2.PaletteDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.ImeShield
open Wanxiang2.OverlayDesk
open Wanxiang2.PalettePanel

/// 从主题管理者取命令面板的外观。
module PaletteLook =
    let ofTheme (t: ThemeManager) : PalettePanelLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        let space s = t.Space s :> IObservable<float>
        let xl = t.Space Space.Xl
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Faint = brush PaletteKey.TextFaint
          Selected = brush PaletteKey.SurfaceContainer
          FieldSurface = brush PaletteKey.Surface
          FieldEdge = brush PaletteKey.Border
          Caret = brush PaletteKey.Accent
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          MicroFontSize = metric Metric.FontMicro
          MonoFont = t.MonoFontFamily
          ItemPadding = Live.map2 (fun x y -> Thickness(x, y)) xl (t.Metric Metric.MenuItemPaddingY)
          ItemRadius = Live.map CornerRadius (space Space.RadiusSm)
          ItemMinHeight = metric Metric.MenuItemMinHeight
          ItemSpacing = space Space.XXs
          ColumnSpacing = space Space.Md
          StackSpacing = space Space.Xs
          StatusMargin = Live.map2 (fun x y -> Thickness(x, y)) xl (t.Space Space.Xs)
          HeaderMargin = Live.map2 (fun x y -> Thickness(x, y, x, (t.Space Space.Xs).Value)) xl (t.Space Space.Md)
          HintMargin = Live.map2 (fun x y -> Thickness(x, 0.0, x, y)) xl (t.Space Space.Xs)
          ListMaxHeight = metric Metric.MenuMaxHeight
          FieldPadding = Live.map2 (fun x y -> Thickness(x, y)) xl (t.Metric Metric.TextFieldPaddingY)
          FieldRadius = Live.map CornerRadius (space Space.RadiusLg)
          FieldEdgeWidth = Live.map Thickness (metric Metric.BorderWidth)
          FieldTextMinHeight = metric Metric.TextFieldTextMinHeight }

/// 命令面板（决策 370–379）的接线：条目与最近使用每次输入都重新取并经 CommandPalette.rank 排序，
/// 有查询时在末尾追加「在全部消息中搜索」交接行；放进对话层（宽 560，搜索框初始焦点）；
/// 执行时先关面板、等焦点归还，再在下一拍发 `Run`（执行可能再开对话框或切页面）。
/// 中文输入法组合期间的回车 / 方向键归输入法：构造时装上全局 ImeShield（幂等）。
type PaletteDesk(look: PalettePanelLook, dialogWidth: IObservable<float>, overlay: OverlayDesk, prefersTouch: unit -> bool) =
    do ImeShield.install ()
    let run = Event<PaletteItem * string>()

    let fullTextItem (q: string) =
        { id = CommandPalette.fullTextId
          kind = PaletteAction
          title = CommandPalette.fullTextTitle q
          subtitle = "交给侧栏搜索，查找消息正文"
          keywords = []
          shortcut = Some "Ctrl / ⌘ + Shift + F" }

    new(theme: ThemeManager, overlay: OverlayDesk, prefersTouch: unit -> bool) = PaletteDesk(PaletteLook.ofTheme theme, theme.Metric Metric.PaletteDialogWidth, overlay, prefersTouch)

    /// 只建内容（不进对话层）；`execute` 收到所选条目与当时的输入。返回 (内容, 搜索框)。
    member _.Build(items: unit -> PaletteItem list, recent: unit -> string list, execute: PaletteItem -> string -> unit) : Control * TextBox =
        let shown = Collections.Generic.Dictionary<string, PaletteItem>()
        let query (q: string) =
            let recentIds = recent ()
            let ranked = CommandPalette.rank q recentIds (items ())
            let all = if String.IsNullOrWhiteSpace q then ranked else ranked @ [ fullTextItem q ]
            let recentSet = Set.ofList recentIds
            shown.Clear()
            for i in all do shown[i.id] <- i
            { Rows =
                all
                |> List.map (fun i ->
                    { Id = i.id
                      Title = i.title
                      Subtitle = i.subtitle
                      KindLabel = CommandPalette.kindLabel i.kind
                      Shortcut = i.shortcut
                      Recent = recentSet.Contains i.id })
              MatchCount = ranked.Length
              HasRecent = not recentSet.IsEmpty }
        let slots = { Query = query; CompactKeys = CommandPalette.compactKeys; PrefersTouch = prefersTouch }
        PalettePanel.build look slots (fun row q ->
            match shown.TryGetValue row.Id with
            | true, item -> execute item q
            | _ -> ())

    /// Ctrl/⌘+K：已有对话框时不开；有浮层先关浮层。
    member this.Show(items: unit -> PaletteItem list, recent: unit -> string list) =
        if not overlay.Layer.IsDialogOpen then
            if overlay.Layer.IsPopupOpen then overlay.ClosePopup()
            let content, search =
                this.Build(items, recent, fun item q ->
                    overlay.CloseDialog()
                    Dispatcher.UIThread.Post(fun () -> run.Trigger(item, q)))
            overlay.Layer.ShowDialog(content, (let mutable w = 0.0 in (dialogWidth.Subscribe(fun v -> w <- v)).Dispose(); w), initialFocus = search, scrollWholeContent = false)

    /// 面板关闭后执行所选条目（条目, 当时的输入）；宿主据 ShellPalette.commandOf 分派
    [<CLIEvent>] member _.Run = run.Publish
