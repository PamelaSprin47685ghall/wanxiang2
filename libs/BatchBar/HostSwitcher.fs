namespace Wanxiang2.HostSwitcher

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 侧栏页脚的主机状态行与周边动作（迁自旧 Views/Sidebar.fs 的 statusRow、serverCaption、statusDot、
/// settingsButton、paletteButton、SetServerUrl、SetConnection）。重连 / 设置 / 命令面板的动作归组合者。
type HostSwitcher(look: HostSwitcherLook) =
    let kit = Kit(look)
    let reconnect = Event<unit>()
    let settings = Event<unit>()
    let palette = Event<unit>()
    let mutable compact = false
    let mutable iconSize = 0.0
    let mutable compactSize = 0.0
    let mutable connected = false

    let caption =
        let t = kit.Text("选择主机", look.SmallFontSize, look.Text)
        t.TextTrimming <- TextTrimming.CharacterEllipsis
        t
    let status =
        let t = kit.Text("未连接", look.CaptionFontSize, look.Muted)
        t.VerticalAlignment <- VerticalAlignment.Center
        t.TextTrimming <- TextTrimming.CharacterEllipsis
        t
    let mutable dot = kit.StatusDot(look.DotSize, look.Faint) :> Control
    let statusRow =
        let stack = kit.VStack(look.SpaceXs, [ caption :> Control; kit.HStack(look.SpaceMd, [ dot; status :> Control ]) :> Control ])
        let t = Tap(stack, "重新连接主机", [ "surface"; "hover-plain" ], fun () -> reconnect.Trigger())
        t.Background <- Brushes.Transparent
        kit.Live(look.RadiusSm, fun v -> t.CornerRadius <- CornerRadius v)
        ToolTip.SetTip(t, "切换或重新连接主机")
        t
    let settingsButton = kit.IconButton("gear", "设置（Ctrl / ⌘ + ,）", fun () -> settings.Trigger())
    let paletteButton =
        let b = kit.IconButton("search", "命令面板（Ctrl / ⌘ + K）", fun () -> palette.Trigger())
        b.IsVisible <- false
        b.Name <- "sidebar-palette"
        AutomationProperties.SetName(b, "命令面板")
        AutomationProperties.SetHelpText(b, "搜索对话、操作与设置（Ctrl / ⌘ + K）")
        b

    let square (b: Tap) =
        let v = if compact then compactSize else iconSize
        b.Width <- v
        b.Height <- v

    let view =
        let dock = DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)
        DockPanel.SetDock(settingsButton, Dock.Right)
        dock.Children.Add settingsButton
        DockPanel.SetDock(paletteButton, Dock.Right)
        dock.Children.Add paletteButton
        dock.Children.Add statusRow
        let b = Border(Child = dock, BorderBrush = look.Hairline)
        kit.Live(look.BarHeight, fun v -> b.Height <- v)
        kit.Live(look.Space2xl, fun v -> b.Padding <- Thickness(v, 0.0, v, 0.0))
        kit.Live(look.DividerWidth, fun v -> b.BorderThickness <- Thickness(0.0, v, 0.0, 0.0))
        b

    do
        kit.Live(look.IconButtonSize, fun v -> iconSize <- v; square settingsButton; square paletteButton)
        kit.Live(look.CompactTarget, fun v -> compactSize <- v; square settingsButton; square paletteButton)
        square settingsButton
        square paletteButton

    member _.View = view :> Control
    member _.StatusRow = statusRow :> Control
    member _.SettingsButton = settingsButton :> Control
    member _.PaletteButton = paletteButton :> Control
    member _.ConnectionText = status.Text
    member _.IsConnected = connected

    /// 主机地址 → 显示权限部分；非法则「选择主机」
    member _.SetServerUrl(url: string) =
        caption.Text <-
            match Uri.TryCreate(url, UriKind.Absolute) with
            | true, uri -> uri.Authority
            | _ -> "选择主机"

    member _.SetConnection(isConnected: bool, text: string) =
        connected <- isConnected
        status.Text <- text
        // 换圆点填充：StatusDot 的 Fill 是构造时定的，这里重建
        let fill = if isConnected then look.Success else look.Faint
        let next = kit.StatusDot(look.DotSize, fill) :> Control
        match statusRow.Child with
        | :? StackPanel as col when col.Children.Count >= 2 ->
            match col.Children[1] with
            | :? StackPanel as row when row.Children.Count >= 1 ->
                row.Children[0] <- next
                dot <- next
            | _ -> ()
        | _ -> ()

    member _.ConfigurePalette(visible: bool) = paletteButton.IsVisible <- visible

    member _.SetCompact(value: bool) =
        compact <- value
        square settingsButton
        square paletteButton

    [<CLIEvent>] member _.ReconnectClicked = reconnect.Publish
    [<CLIEvent>] member _.SettingsClicked = settings.Publish
    [<CLIEvent>] member _.PaletteClicked = palette.Publish
