namespace Wanxiang2.SidebarSearch

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState

/// 搜索框按键；组合者置 Handled 表示已接手
type SearchKeyArgs(key: Key) =
    member _.Key = key
    member val Handled = false with get, set

/// 侧栏搜索（单入口 Render(SearchBoxVS)）。
/// 搜索防抖不再自开定时器——文本变化即时上报，何时发查询由 WorkspaceState.update 的 MsgTick 判定；
/// 控件自身只 Render(SearchBoxVS)。
type SidebarSearch() =
    let box =
        let tb = TextBox(PlaceholderText = "搜索对话")
        tb.Classes.Add "control-stroke"
        tb.VerticalAlignment <- VerticalAlignment.Center
        tb
    let changed = Event<string>()
    let debounced = Event<string>()
    let keys = Event<SearchKeyArgs>()
    let clear = Event<unit>()

    let clearButton =
        let b = Button(Content = "✕")
        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        AutomationProperties.SetName(b, "清空搜索")
        ToolTip.SetTip(b, "清空搜索")
        b.Click.Add(fun _ -> clear.Trigger())
        b

    let reserve (visible: bool) =
        clearButton.IsVisible <- true
        clearButton.Opacity <- (if visible then 1.0 else 0.0)
        clearButton.IsHitTestVisible <- visible
        clearButton.Focusable <- visible
        AutomationProperties.SetAccessibilityView(clearButton, if visible then AccessibilityView.Default else AccessibilityView.Raw)

    let hintTitle =
        let t = TextBlock(Text = "未找到匹配对话")
        t.FontWeight <- FontWeight.Medium
        t.TextAlignment <- TextAlignment.Center
        t
    let hintSub =
        let t = TextBlock(Text = "点击清空，或按 Enter / Esc 恢复")
        t.TextAlignment <- TextAlignment.Center
        t

    let hint =
        let panel = StackPanel(Orientation = Orientation.Vertical, Spacing = 4.0)
        panel.Children.Add hintTitle
        panel.Children.Add hintSub
        let b = Button(Content = panel)
        b.Classes.Add "ghost"
        b.Classes.Add "surface"
        b.Classes.Add "hover-plain"
        b.HorizontalAlignment <- HorizontalAlignment.Center
        b.VerticalAlignment <- VerticalAlignment.Top
        b.IsVisible <- false
        b.Padding <- Thickness(16.0, 12.0)
        b.Margin <- Thickness(16.0, 24.0, 16.0, 0.0)
        AutomationProperties.SetName(b, "未找到匹配对话，点击清空搜索")
        ToolTip.SetTip(b, "点击清空搜索（Esc）")
        b.Click.Add(fun _ -> clear.Trigger())
        b.KeyDown.Add(fun e ->
            if e.Key = Key.Escape then
                e.Handled <- true
                clear.Trigger())
        b

    let searchIcon =
        TextBlock(
            Text = "🔍",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = Thickness(0.0, 0.0, 8.0, 0.0))

    let view =
        AutomationProperties.SetName(box, "搜索对话")
        reserve false
        let row = DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)
        DockPanel.SetDock(searchIcon, Dock.Left)
        DockPanel.SetDock(clearButton, Dock.Right)
        clearButton.Margin <- Thickness(8.0, 0.0, 0.0, 0.0)
        row.Children.Add searchIcon
        row.Children.Add clearButton
        row.Children.Add box
        let area = Border(Child = row)
        area.Padding <- Thickness(16.0, 8.0)
        area

    do
        box.TextChanged.Add(fun _ ->
            let txt = if isNull box.Text then "" else box.Text
            reserve (not (String.IsNullOrWhiteSpace txt))
            changed.Trigger txt)
        box.KeyDown.Add(fun e ->
            if e.Key = Key.Down || e.Key = Key.Enter || e.Key = Key.Escape then
                let args = SearchKeyArgs(e.Key)
                keys.Trigger args
                if args.Handled || e.Key = Key.Escape then e.Handled <- true)

    /// 搜索区
    member _.View = view :> Control
    member _.Box = box
    member _.ClearButton = clearButton :> Control
    /// 「未找到匹配对话」提示，组合者放在列表上方
    member _.EmptyHint = hint :> Control

    member _.Text
        with get () = if isNull box.Text then "" else box.Text
        and set (v: string) =
            if box.Text <> v then box.Text <- v

    /// 单入口渲染
    member this.Render(vs: SearchBoxVS) =
        this.Text <- vs.Text
        hint.IsVisible <- vs.EmptyHintVisible
        reserve (not (String.IsNullOrWhiteSpace vs.Text))

    /// 停掉在途防抖（兼容易用）
    member _.StopDebounce() = ()

    /// 聚焦
    member _.Focus(selectAll: bool) =
        box.Focus() |> ignore
        if selectAll || String.IsNullOrEmpty box.Text then box.SelectAll()

    member _.ApplyEmptyHint(visible: bool) = hint.IsVisible <- visible
    member _.FocusEmptyHint() = hint.Focus NavigationMethod.Directional |> ignore

    /// 每次输入即时上报
    [<CLIEvent>] member _.Changed = changed.Publish
    /// 防抖到点，外部或 Tick 发出
    [<CLIEvent>] member _.Debounced = debounced.Publish
    member _.TriggerDebounced(text: string) = debounced.Trigger text
    /// Down / Enter / Escape
    [<CLIEvent>] member _.Keys = keys.Publish
    /// 清空请求
    [<CLIEvent>] member _.ClearRequested = clear.Publish
