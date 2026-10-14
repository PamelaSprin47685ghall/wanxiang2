namespace Wanxiang2.BatchBar

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 两条批量条的呈现（文案与启用由组合者从多选状态算出）
type BatchBarState =
    { CountText: string
      SelectAllText: string
      SelectAllHelp: string
      PinText: string
      SelectAllEnabled: bool
      /// 置顶、归档、删除三个键
      BatchEnabled: bool
      Visible: bool }

/// 多选模式的头条与底部批量操作条（迁自旧 Views/Sidebar.fs 的 selectionHeader、selectionActionBar、
/// RefreshSelectionChrome 的控件部分）。选中集合、批量动作与确认归组合者。
type BatchBar(look: BatchBarLook) =
    let kit = Kit(look)
    let exit = Event<unit>()
    let selectAll = Event<unit>()
    let pin = Event<unit>()
    let archive = Event<unit>()
    let delete = Event<unit>()

    let countText =
        let t = kit.Text("已选 0 项", look.SmallFontSize, look.Text)
        t.FontWeight <- FontWeight.Medium
        t.VerticalAlignment <- VerticalAlignment.Center
        t.TextTrimming <- TextTrimming.CharacterEllipsis
        t
    let cancelButton = kit.IconButton("close", "退出多选（Esc）", fun () -> exit.Trigger())
    let selectAllButton = kit.Button(Tone.Ghost, "全选", fun () -> selectAll.Trigger())
    let pinButton = kit.Button(Tone.Secondary, "置顶", fun () -> pin.Trigger())
    let archiveButton = kit.Button(Tone.Secondary, "归档", fun () -> archive.Trigger())
    let deleteButton = kit.Button(Tone.Danger, "删除", fun () -> delete.Trigger())

    let header =
        let b = Border(Background = look.HeaderSurface, BorderBrush = look.Hairline, IsVisible = false)
        kit.Live(Kit.Pair(look.SpaceXl, look.SpaceMd), fun (x, y) -> b.Padding <- Thickness(x, y))
        kit.Live(look.DividerWidth, fun v -> b.BorderThickness <- Thickness(0.0, 0.0, 0.0, v))
        kit.Live(Kit.Pair(look.SpaceXl, look.SelectButtonPaddingY), fun (x, y) -> selectAllButton.Padding <- Thickness(x, y))
        let dock = DockPanel(LastChildFill = false, VerticalAlignment = VerticalAlignment.Center)
        DockPanel.SetDock(cancelButton, Dock.Left)
        DockPanel.SetDock(selectAllButton, Dock.Right)
        dock.Children.Add cancelButton
        dock.Children.Add selectAllButton
        dock.Children.Add countText
        b.Child <- dock
        b

    let bar =
        let b = Border(Background = look.BarSurface, BorderBrush = look.Hairline, IsVisible = false)
        kit.Live(look.SpaceMd, fun v -> b.Padding <- Thickness(v, v))
        kit.Live(look.DividerWidth, fun v -> b.BorderThickness <- Thickness(0.0, v, 0.0, 0.0))
        // 横向工具条左右键走邻键；不可用的邻键不接焦点
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
        b.Child <- kit.HStack(look.SpaceMd, [ pinButton :> Control; archiveButton :> Control; deleteButton :> Control ])
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

    member _.Render(s: BatchBarState) =
        countText.Text <- s.CountText
        selectAllButton.SetText s.SelectAllText
        pinButton.SetText s.PinText
        selectAllButton.SetEnabled s.SelectAllEnabled
        pinButton.SetEnabled s.BatchEnabled
        archiveButton.SetEnabled s.BatchEnabled
        deleteButton.SetEnabled s.BatchEnabled
        AutomationProperties.SetHelpText(selectAllButton, s.SelectAllHelp)
        header.IsVisible <- s.Visible
        bar.IsVisible <- s.Visible

    [<CLIEvent>] member _.ExitClicked = exit.Publish
    [<CLIEvent>] member _.SelectAllClicked = selectAll.Publish
    [<CLIEvent>] member _.PinClicked = pin.Publish
    [<CLIEvent>] member _.ArchiveClicked = archive.Publish
    [<CLIEvent>] member _.DeleteClicked = delete.Publish
