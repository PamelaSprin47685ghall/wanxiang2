namespace Wanxiang2.SidebarBrand

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Wanxiang2.WorkspaceState

/// 侧栏顶部品牌行。
/// 废除 Look 记录，采用原生 Button、样式类与 DynamicResource。
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
        let b = Button(Content = "新建对话")
        b.Classes.Add "secondary"
        b.Classes.Add "button-secondary"
        b.HorizontalAlignment <- HorizontalAlignment.Stretch
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

    let logoSlot = Border(VerticalAlignment = VerticalAlignment.Center)

    let wordmark =
        TextBlock(Text = "万象", FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center)

    let view =
        let brandRow = StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0)
        brandRow.Children.Add logoSlot
        brandRow.Children.Add wordmark
        let leading = StackPanel(Orientation = Orientation.Horizontal, Spacing = 4.0)
        leading.Children.Add backButton
        leading.Children.Add brandRow
        let line = Border(Child = leading)
        let newRow = DockPanel(LastChildFill = true)
        DockPanel.SetDock(presetButton, Dock.Right)
        presetButton.Margin <- Thickness(4.0, 0.0, 0.0, 0.0)
        newRow.Children.Add presetButton
        newRow.Children.Add newButton
        let column = StackPanel(Orientation = Orientation.Vertical, Spacing = 8.0)
        column.Children.Add line
        column.Children.Add newRow
        let b = Border(Child = column)
        b.Padding <- Thickness(16.0, 8.0, 16.0, 8.0)
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
