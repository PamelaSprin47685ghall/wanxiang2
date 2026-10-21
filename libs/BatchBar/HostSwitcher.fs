namespace Wanxiang2.HostSwitcher

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState

/// 侧栏页脚的主机状态行与周边动作。
/// 废除 Look 记录，采用原生 Button、样式类与 DynamicResource。
type HostSwitcher() =
    let reconnect = Event<unit>()
    let settings = Event<unit>()
    let palette = Event<unit>()
    let mutable connected = false

    let caption =
        TextBlock(Text = "选择主机", FontWeight = FontWeight.Medium, TextTrimming = TextTrimming.CharacterEllipsis)

    let status =
        TextBlock(
            Text = "未连接",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        )

    let dot =
        Border(
            Width = 8.0,
            Height = 8.0,
            CornerRadius = CornerRadius 4.0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Gray
        )

    let statusRow =
        let statusStack = StackPanel(Orientation = Orientation.Horizontal, Spacing = 6.0)
        statusStack.Children.Add dot
        statusStack.Children.Add status
        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)
        stack.Children.Add caption
        stack.Children.Add statusStack
        let b = Button(Content = stack)
        b.Classes.Add "ghost"
        b.Classes.Add "surface"
        b.Classes.Add "hover-plain"
        b.HorizontalAlignment <- HorizontalAlignment.Stretch
        AutomationProperties.SetName(b, "重新连接主机")
        ToolTip.SetTip(b, "切换或重新连接主机")
        b.Click.Add(fun _ -> reconnect.Trigger())
        b

    let settingsButton =
        let b = Button(Content = "⚙")
        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        AutomationProperties.SetName(b, "设置（Ctrl / ⌘ + ,）")
        ToolTip.SetTip(b, "设置（Ctrl / ⌘ + ,）")
        b.Click.Add(fun _ -> settings.Trigger())
        b

    let paletteButton =
        let b = Button(Content = "🔍")
        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        b.IsVisible <- false
        b.Name <- "sidebar-palette"
        AutomationProperties.SetName(b, "命令面板")
        AutomationProperties.SetHelpText(b, "搜索对话、操作与设置（Ctrl / ⌘ + K）")
        b.Click.Add(fun _ -> palette.Trigger())
        b

    let view =
        let dock =
            DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)

        DockPanel.SetDock(settingsButton, Dock.Right)
        dock.Children.Add settingsButton
        DockPanel.SetDock(paletteButton, Dock.Right)
        dock.Children.Add paletteButton
        dock.Children.Add statusRow
        let b = Border(Child = dock)
        b.Classes.Add "hairline"
        b.Padding <- Thickness(16.0, 8.0)
        b

    member _.View = view :> Control
    member _.StatusRow = statusRow :> Control
    member _.SettingsButton = settingsButton :> Control
    member _.PaletteButton = paletteButton :> Control
    member _.ConnectionText = status.Text
    member _.IsConnected = connected

    /// 命令面板入口：宿主是否提供面板由宿主自己知道，故走插槽而非每帧投影。
    member _.SlotPalette(visible: bool) = paletteButton.IsVisible <- visible

    /// 单入口渲染
    member _.Render(vs: HostSwitcherVS) =
        // 主机地址 → 显示权限部分；非法则「选择主机」
        caption.Text <-
            match Uri.TryCreate(vs.ServerUrl, UriKind.Absolute) with
            | true, uri -> uri.Authority
            | _ -> "选择主机"

        connected <- vs.Connected
        status.Text <- vs.StatusText
        dot.Background <- if vs.Connected then Brushes.ForestGreen else Brushes.Gray

    [<CLIEvent>]
    member _.ReconnectClicked = reconnect.Publish

    [<CLIEvent>]
    member _.SettingsClicked = settings.Publish

    [<CLIEvent>]
    member _.PaletteClicked = palette.Publish
