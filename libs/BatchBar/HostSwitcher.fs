namespace Wanxiang2.HostSwitcher

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 侧栏页脚的主机状态行与周边动作。
/// 废除 Look 记录，采用原生 Button、样式类与 DynamicResource。
type HostSwitcher() =
    let reconnect = Event<unit>()
    let settings = Event<unit>()
    let palette = Event<unit>()
    let mutable connected = false

    let caption =
        TextBlock(
            Text = "选择主机",
            FontSize = 13.0,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        )

    let status =
        TextBlock(
            Text = "未连接",
            FontSize = 12.0,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        )

    let dot =
        Border(Width = 6.0, Height = 6.0, CornerRadius = CornerRadius 3.0, VerticalAlignment = VerticalAlignment.Center)

    let statusRow =
        let stack = StackPanel(Orientation = Orientation.Vertical, Spacing = 2.0)
        stack.Children.Add caption
        stack.Children.Add status
        let b = Button(Content = stack)
        b.Classes.Add "ghost"
        b.Classes.Add "surface"
        b.Classes.Add "hover-plain"
        b.HorizontalAlignment <- HorizontalAlignment.Stretch
        AutomationProperties.SetName(b, "重新连接主机")
        ToolTip.SetTip(b, "切换或重新连接主机")
        b.Click.Add(fun _ -> reconnect.Trigger())
        b

    let glyph (data: string) =
        let p =
            Path(
                Data = Geometry.Parse data,
                StrokeThickness = 1.4,
                Fill = Brushes.Transparent,
                Width = 14.0,
                Height = 14.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            )

        p.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore
        p

    let settingsButton =
        let b =
            Button(
                Content =
                    glyph "M 8 3.2 L 8 5 M 8 11 L 8 12.8 M 3.2 8 L 5 8 M 11 8 L 12.8 8 M 8 6.2 A 1.8 1.8 0 1 1 7.9 6.2",
                Width = 30.0,
                Height = 30.0,
                Padding = Thickness 0.0
            )

        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        AutomationProperties.SetName(b, "设置（Ctrl / ⌘ + ,）")
        ToolTip.SetTip(b, "设置（Ctrl / ⌘ + ,）")
        b.Click.Add(fun _ -> settings.Trigger())
        b

    let paletteButton =
        let b =
            Button(
                Content = glyph "M 6 6 A 3.2 3.2 0 1 1 6 6.01 M 8.4 8.4 L 12 12",
                Width = 30.0,
                Height = 30.0,
                Padding = Thickness 0.0
            )

        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        b.IsVisible <- false
        b.Name <- "sidebar-palette"
        AutomationProperties.SetName(b, "命令面板")
        AutomationProperties.SetHelpText(b, "搜索对话、操作与设置（Ctrl / ⌘ + K）")
        b.Click.Add(fun _ -> palette.Trigger())
        b

    do status.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore

    let view =
        let dock =
            DockPanel(LastChildFill = true, VerticalAlignment = VerticalAlignment.Center)

        DockPanel.SetDock(settingsButton, Dock.Right)
        dock.Children.Add settingsButton
        DockPanel.SetDock(paletteButton, Dock.Right)
        dock.Children.Add paletteButton

        // 页脚行序对齐 react rail-foot：[状态点][主机两行文字][设置]
        let row =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 8.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        row.Children.Add dot
        row.Children.Add statusRow
        dock.Children.Add row
        let b = Border(Child = dock)
        b.Classes.Add "hairline"
        b.Padding <- Thickness(16.0, 8.0, 8.0, 12.0)
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

        dot.Bind(
            Border.BackgroundProperty,
            Dyn.res (
                if vs.Connected then
                    "Palette.Success"
                else
                    "Palette.Warning"
            )
        )
        |> ignore

    [<CLIEvent>]
    member _.ReconnectClicked = reconnect.Publish

    [<CLIEvent>]
    member _.SettingsClicked = settings.Publish

    [<CLIEvent>]
    member _.PaletteClicked = palette.Publish
