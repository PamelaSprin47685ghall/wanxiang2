namespace Wanxiang2.SidebarSearch

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Threading

/// 搜索框按键；组合者置 Handled 表示已接手
type SearchKeyArgs(key: Key) =
    member _.Key = key
    member val Handled = false with get, set

/// 侧栏搜索（迁自旧 Views/Sidebar.fs 的 searchShell / searchBox / clearSearchButton / searchEmptyHint、
/// searchDebounce 计时与搜索框 KeyDown）。查询会话、是否重发、列表重建都归组合者。
type SidebarSearch(look: SidebarSearchLook) =
    let kit = Kit(look)
    let shell, box = kit.TextField "搜索对话"
    let changed = Event<string>()
    let debounced = Event<string>()
    let keys = Event<SearchKeyArgs>()
    let clear = Event<unit>()
    let timer = DispatcherTimer()
    let mutable compact = false
    let mutable iconSize = 0.0
    let mutable compactSize = 0.0
    let clearButton = kit.IconButton("close", "清空搜索", fun () -> clear.Trigger())

    /// 槽位常驻：只切透明度、命中与键盘（与旧 Ui.setReservedActionVisible 同义）
    let reserve (visible: bool) =
        clearButton.IsVisible <- true
        clearButton.Opacity <- (if visible then 1.0 else 0.0)
        clearButton.IsHitTestVisible <- visible
        clearButton.Focusable <- visible
        AutomationProperties.SetAccessibilityView(clearButton, if visible then AccessibilityView.Default else AccessibilityView.Raw)

    let square () =
        let v = if compact then compactSize else iconSize
        clearButton.Width <- v
        clearButton.Height <- v

    let hintTitle = kit.Text("未找到匹配对话", look.SmallFontSize, look.Muted)
    let hintSub = kit.Text("点击清空，或按 Enter / Esc 恢复", look.MicroFontSize, look.Faint)
    let hint =
        hintTitle.FontWeight <- Avalonia.Media.FontWeight.Medium
        hintTitle.TextAlignment <- Avalonia.Media.TextAlignment.Center
        hintSub.TextAlignment <- Avalonia.Media.TextAlignment.Center
        let panel = kit.VStack(look.SpaceXs, [ hintTitle :> Control; hintSub :> Control ])
        panel.HorizontalAlignment <- HorizontalAlignment.Center
        let t = Tap(panel, "未找到匹配对话，点击清空搜索", [ "surface"; "hover-plain" ], fun () -> clear.Trigger())
        t.Background <- Avalonia.Media.Brushes.Transparent
        t.HorizontalAlignment <- HorizontalAlignment.Center
        t.VerticalAlignment <- VerticalAlignment.Top
        t.IsVisible <- false
        kit.Live(Kit.Pair(look.Space2xl, look.SpaceXl), fun (x, y) -> t.Padding <- Thickness(x, y))
        kit.Live(Kit.Pair(look.Space2xl, look.Space6xl), fun (x, top) -> t.Margin <- Thickness(x, top, x, 0.0))
        kit.Live(look.RadiusMd, fun v -> t.CornerRadius <- CornerRadius v)
        ToolTip.SetTip(t, "点击清空搜索（Esc）")
        // Enter/Space 归 Tap；Escape 单独接
        t.KeyDown.Add(fun e ->
            if e.Key = Key.Escape then
                e.Handled <- true
                clear.Trigger())
        t

    let view =
        AutomationProperties.SetName(box, "搜索对话")
        reserve false
        square ()
        shell.Child <- null
        let icon = look.Glyph "search" look.Faint
        icon.VerticalAlignment <- VerticalAlignment.Center
        kit.Live(look.GlyphSize, fun v -> icon.Width <- v; icon.Height <- v)
        kit.Live(look.SpaceMd, fun v ->
            icon.Margin <- Thickness(0.0, 0.0, v, 0.0)
            clearButton.Margin <- Thickness(v, 0.0, 0.0, 0.0))
        clearButton.VerticalAlignment <- VerticalAlignment.Center
        box.VerticalAlignment <- VerticalAlignment.Center
        // 占位符用 muted：比正文弱、比 faint 强
        box.PlaceholderForeground <- look.Muted
        let row = DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)
        DockPanel.SetDock(icon, Dock.Left)
        DockPanel.SetDock(clearButton, Dock.Right)
        row.Children.Add icon
        row.Children.Add clearButton
        row.Children.Add box
        shell.Child <- row
        let area = Border(Background = Avalonia.Media.Brushes.Transparent, Child = shell)
        kit.Live(Kit.Pair(look.Space2xl, look.SpaceMd), fun (x, y) -> area.Padding <- Thickness(x, y, x, y))
        area

    do
        kit.Live(look.DebounceMs, fun v -> timer.Interval <- TimeSpan.FromMilliseconds v)
        kit.Live(look.IconButtonSize, fun v -> iconSize <- v; square ())
        kit.Live(look.CompactTarget, fun v -> compactSize <- v; square ())
        timer.Tick.Add(fun _ ->
            timer.Stop()
            debounced.Trigger box.Text)
        box.TextChanged.Add(fun _ ->
            reserve (not (String.IsNullOrWhiteSpace box.Text))
            changed.Trigger box.Text
            timer.Stop()
            timer.Start())
        box.KeyDown.Add(fun e ->
            if e.Key = Key.Down || e.Key = Key.Enter || e.Key = Key.Escape then
                let args = SearchKeyArgs(e.Key)
                keys.Trigger args
                // Escape 始终吞掉（与旧实现一致），其余看组合者
                if args.Handled || e.Key = Key.Escape then e.Handled <- true)

    /// 搜索区（含外边距）
    member _.View = view :> Control
    member _.Box = box
    member _.ClearButton = clearButton :> Control
    /// 「未找到匹配对话」提示，组合者放在列表上方
    member _.EmptyHint = hint :> Control

    member _.Text
        with get () = if isNull box.Text then "" else box.Text
        and set (v: string) = box.Text <- v

    /// 停掉在途的防抖（手动发起查询或清空后）
    member _.StopDebounce() = timer.Stop()

    /// 行内 Up 回来保留插入点（selectAll = false）；Ctrl+K 与空输入全选
    member _.Focus(selectAll: bool) =
        box.Focus() |> ignore
        if selectAll || String.IsNullOrEmpty box.Text then box.SelectAll()

    member _.SetEmptyHint(visible: bool) = hint.IsVisible <- visible
    member _.FocusEmptyHint() = hint.Focus NavigationMethod.Directional |> ignore

    member _.SetCompact(value: bool) =
        compact <- value
        square ()

    /// 每次输入（未防抖）
    [<CLIEvent>] member _.Changed = changed.Publish
    /// 防抖到点，带当时的输入
    [<CLIEvent>] member _.Debounced = debounced.Publish
    /// Down / Enter / Escape
    [<CLIEvent>] member _.Keys = keys.Publish
    /// 清空键、空结果提示（点击 / Enter / Space / Esc）
    [<CLIEvent>] member _.ClearRequested = clear.Publish
