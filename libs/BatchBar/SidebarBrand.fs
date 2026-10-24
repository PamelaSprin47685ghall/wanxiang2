namespace Wanxiang2.SidebarBrand

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState

/// 侧栏顶栏。一行 52：左边品牌，右边新对话图标键。
/// 不再在品牌下面叠一条通栏按钮。
type SidebarBrand() =
    let back = Event<unit>()
    let create = Event<unit>()
    let preset = Event<Control>()

    let backButton =
        let b = Button(Content = "←")
        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        AutomationProperties.SetName(b, "返回对话")
        ToolTip.SetTip(b, "返回对话")
        b.IsVisible <- false
        b.Click.Add(fun _ -> back.Trigger())
        b

    let newButton =
        let b =
            Button(
                Content = "+",
                Width = 30.0,
                Height = 30.0,
                Padding = Thickness 0.0,
                FontSize = 18.0,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            )

        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        AutomationProperties.SetName(b, "新建对话（Ctrl / ⌘ + N）")
        ToolTip.SetTip(b, "新建对话（Ctrl / ⌘ + N）")
        b.Click.Add(fun _ -> create.Trigger())
        b

    let presetButton =
        let b = Button(Content = "▼")
        b.Classes.Add "icon-button"
        b.Classes.Add "ghost"
        b.IsVisible <- false
        b.Name <- "preset-menu-button"
        AutomationProperties.SetName(b, "用预设新建对话")
        ToolTip.SetTip(b, "用预设新建对话")
        b.Click.Add(fun _ -> preset.Trigger(b :> Control))
        b

    let logoSlot =
        Border(Width = 26.0, Height = 26.0, VerticalAlignment = VerticalAlignment.Center)

    let wordmark =
        TextBlock(
            Text = "万象",
            FontSize = 16.0,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        )

    let view =
        let brandRow =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 8.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        brandRow.Children.Add logoSlot
        brandRow.Children.Add wordmark

        let leading =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 4.0,
                VerticalAlignment = VerticalAlignment.Center
            )

        leading.Children.Add backButton
        leading.Children.Add brandRow

        let trailing =
            StackPanel(
                Orientation = Orientation.Horizontal,
                Spacing = 4.0,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            )

        trailing.Children.Add presetButton
        trailing.Children.Add newButton

        let row = Grid(Height = 52.0)
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Auto))
        row.Children.Add leading
        row.Children.Add trailing
        Grid.SetColumn(trailing, 1)

        let b = Border(Child = row)
        b.Padding <- Thickness(16.0, 0.0, 8.0, 0.0)
        b

    member _.View = view :> Control
    member _.BackButton = backButton :> Control
    member _.NewButton = newButton :> Control
    member _.PresetButton = presetButton :> Control
    member _.LogoSlot = logoSlot

    /// 宿主挂载品牌图标（插槽：构造期一次性接入，不随每帧投影变）
    member _.SlotLogo(logo: Control) = logoSlot.Child <- logo

    /// 单入口渲染
    member _.Render(vs: SidebarBrandVS) =
        presetButton.IsVisible <- vs.PresetsVisible
        backButton.IsVisible <- vs.Compact

    [<CLIEvent>]
    member _.BackClicked = back.Publish

    [<CLIEvent>]
    member _.NewClicked = create.Publish

    /// 参数是锚点（预设键）
    [<CLIEvent>]
    member _.PresetClicked = preset.Publish
