namespace Wanxiang2.BatchBar

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState

/// 多选模式的头条与底部批量操作条（单入口 Render(BatchBarVS option)）。
/// 废除 Look 记录，采用原生 Button + 样式类 + 语义化布局。
type BatchBar() =
    let exit = Event<unit>()
    let selectAll = Event<unit>()
    let pin = Event<unit>()
    let archive = Event<unit>()
    let delete = Event<unit>()

    let countText =
        TextBlock(
            Text = "已选 0 项",
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis)

    let cancelButton =
        let b = Button(Content = "✕")
        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        AutomationProperties.SetName(b, "退出多选（Esc）")
        ToolTip.SetTip(b, "退出多选（Esc）")
        b.Click.Add(fun _ -> exit.Trigger())
        b

    let selectAllButton =
        let b = Button(Content = "全选")
        b.Classes.Add "ghost"
        b.Classes.Add "button-ghost"
        b.Click.Add(fun _ -> selectAll.Trigger())
        b

    let pinButton =
        let b = Button(Content = "置顶")
        b.Classes.Add "secondary"
        b.Classes.Add "button-secondary"
        b.HorizontalAlignment <- HorizontalAlignment.Stretch
        b.Click.Add(fun _ -> pin.Trigger())
        b

    let archiveButton =
        let b = Button(Content = "归档")
        b.Classes.Add "secondary"
        b.Classes.Add "button-secondary"
        b.HorizontalAlignment <- HorizontalAlignment.Stretch
        b.Click.Add(fun _ -> archive.Trigger())
        b

    let deleteButton =
        let b = Button(Content = "删除")
        b.Classes.Add "danger"
        b.Classes.Add "button-danger"
        b.HorizontalAlignment <- HorizontalAlignment.Stretch
        b.Click.Add(fun _ -> delete.Trigger())
        b

    let header =
        let b = Border(IsVisible = false)
        b.Classes.Add "grouping-card"
        b.Padding <- Thickness(16.0, 8.0)
        let dock = DockPanel(LastChildFill = false, VerticalAlignment = VerticalAlignment.Center)
        DockPanel.SetDock(cancelButton, Dock.Left)
        DockPanel.SetDock(selectAllButton, Dock.Right)
        dock.Children.Add cancelButton
        dock.Children.Add selectAllButton
        dock.Children.Add countText
        b.Child <- dock
        b

    let bar =
        let b = Border(IsVisible = false)
        b.Classes.Add "surface"
        b.Padding <- Thickness(8.0)
        let wire (left: Control) (right: Control) =
            left.KeyDown.Add(fun e ->
                if e.Key = Key.Right && right.IsEnabled && right.IsEffectivelyVisible then
                    e.Handled <- true
                    right.Focus NavigationMethod.Directional |> ignore)
            right.KeyDown.Add(fun e ->
                if e.Key = Key.Left && left.IsEnabled && left.IsEffectivelyVisible then
                    e.Handled <- true
                    left.Focus NavigationMethod.Directional |> ignore)
        wire pinButton archiveButton
        wire archiveButton deleteButton
        let grid = Grid()
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(1.0, GridUnitType.Star)))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(8.0, GridUnitType.Pixel)))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(1.0, GridUnitType.Star)))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(8.0, GridUnitType.Pixel)))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(1.0, GridUnitType.Star)))
        Grid.SetColumn(pinButton, 0)
        Grid.SetColumn(archiveButton, 2)
        Grid.SetColumn(deleteButton, 4)
        grid.Children.Add pinButton
        grid.Children.Add archiveButton
        grid.Children.Add deleteButton
        b.Child <- grid
        b

    /// 顶部：退出、计数、全选
    member _.Header = header :> Control
    /// 底部：置顶、归档、删除
    member _.ActionBar = bar :> Control
    member _.CountText = countText.Text
    member _.SelectAllButton = selectAllButton :> Control
    member _.PinButton = pinButton :> Control
    member _.ArchiveButton = archiveButton :> Control
    member _.DeleteButton = deleteButton :> Control

    /// 单入口幂等渲染：None 隐藏两条，Some 展开并更新状态
    member _.Render(vs: BatchBarVS option) =
        match vs with
        | None ->
            header.IsVisible <- false
            bar.IsVisible <- false
        | Some s ->
            countText.Text <- sprintf "已选 %d 项" s.SelectedCount
            let selAllTxt = if s.AllSelected then "取消全选" else "全选"
            let pinTxt = if s.AllPinned then "取消置顶" else "置顶"
            selectAllButton.Content <- box selAllTxt
            pinButton.Content <- box pinTxt
            AutomationProperties.SetName(selectAllButton, string selAllTxt)
            AutomationProperties.SetName(pinButton, string pinTxt)
            let hasSelection = s.SelectedCount > 0
            selectAllButton.IsEnabled <- true
            pinButton.IsEnabled <- hasSelection && not s.Busy
            pinButton.Focusable <- hasSelection && not s.Busy
            archiveButton.IsEnabled <- hasSelection && not s.Busy
            archiveButton.Focusable <- hasSelection && not s.Busy
            deleteButton.IsEnabled <- hasSelection && not s.Busy
            deleteButton.Focusable <- hasSelection && not s.Busy
            AutomationProperties.SetHelpText(selectAllButton, if s.AllSelected then "取消全选对话" else "全选可见对话")
            header.IsVisible <- true
            bar.IsVisible <- true

    [<CLIEvent>] member _.ExitClicked = exit.Publish
    [<CLIEvent>] member _.SelectAllClicked = selectAll.Publish
    [<CLIEvent>] member _.PinClicked = pin.Publish
    [<CLIEvent>] member _.ArchiveClicked = archive.Publish
    [<CLIEvent>] member _.DeleteClicked = delete.Publish
