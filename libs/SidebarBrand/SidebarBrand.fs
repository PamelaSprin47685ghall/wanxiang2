namespace Wanxiang2.SidebarBrand

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media

/// 侧栏顶部品牌行（迁自旧 Views/Sidebar.fs 的 brandHeader、compactBackButton、newConversationButton、
/// presetMenuButton）。新建、预设菜单、返回的动作归组合者。
type SidebarBrand(look: SidebarBrandLook) =
    let kit = Kit(look)
    let back = Event<unit>()
    let create = Event<unit>()
    let preset = Event<Control>()
    let mutable compact = false
    let mutable iconSize = 0.0
    let mutable compactSize = 0.0
    let mutable textMin = 0.0

    let backButton =
        let b = kit.IconButton("arrow-left", "返回对话", fun () -> back.Trigger())
        b.IsVisible <- false
        b
    let newButton =
        let b = kit.Button(Tone.Secondary, "新建对话", fun () -> create.Trigger())
        b.HorizontalAlignment <- HorizontalAlignment.Stretch
        AutomationProperties.SetName(b, "新建对话（Ctrl / ⌘ + N）")
        ToolTip.SetTip(b, "新建对话（Ctrl / ⌘ + N）")
        b
    let mutable presetButton: Tap = Unchecked.defaultof<_>
    do
        presetButton <-
            kit.IconButton("chevron-down", "用预设新建对话", fun () -> preset.Trigger(presetButton :> Control))
        presetButton.IsVisible <- false
        presetButton.Name <- "preset-menu-button"

    let logoSlot = Border(VerticalAlignment = VerticalAlignment.Center)
    let wordmark =
        let t = kit.Text("万象", look.BodyFontSize, look.Text)
        t.FontWeight <- FontWeight.Medium
        t.VerticalAlignment <- VerticalAlignment.Center
        kit.Live(look.DisplayTracking, fun v -> t.LetterSpacing <- v)
        t

    let square (b: Tap) =
        let v = if compact then compactSize else iconSize
        b.Width <- v
        b.Height <- v
    let sync () =
        square backButton
        square presetButton
        newButton.MinHeight <- (if compact then compactSize else textMin)

    let view =
        let brandRow = kit.HStack(look.SpaceMd, [ logoSlot :> Control; wordmark :> Control ])
        let leading = kit.HStack(look.SpaceXs, [ backButton :> Control; brandRow :> Control ])
        let line = Border(Child = leading)
        kit.Live(look.BarHeight, fun v -> line.Height <- v)
        let newRow = DockPanel(LastChildFill = true)
        DockPanel.SetDock(presetButton, Dock.Right)
        kit.Live(look.SpaceXs, fun v -> presetButton.Margin <- Thickness(v, 0.0, 0.0, 0.0))
        newRow.Children.Add presetButton
        newRow.Children.Add newButton
        let column = kit.VStack(look.SpaceMd, [ line :> Control; newRow :> Control ])
        let b = Border(Child = column)
        kit.Live(look.Space2xl, fun v -> b.Padding <- Thickness(v, 0.0, v, b.Padding.Bottom))
        kit.Live(look.SpaceMd, fun v -> b.Padding <- Thickness(b.Padding.Left, 0.0, b.Padding.Right, v))
        b

    do
        kit.Live(look.LogoSize, fun v ->
            let g = look.Logo v
            g.VerticalAlignment <- VerticalAlignment.Center
            logoSlot.Child <- g)
        kit.Live(look.IconButtonSize, fun v -> iconSize <- v; sync ())
        kit.Live(look.CompactTarget, fun v -> compactSize <- v; sync ())
        kit.Live(look.TextButtonMinHeight, fun v -> textMin <- v; sync ())

    member _.View = view :> Control
    member _.BackButton = backButton :> Control
    member _.NewButton = newButton :> Control
    member _.PresetButton = presetButton :> Control

    /// 有预设才出现
    member _.SetPresetsVisible(visible: bool) = presetButton.IsVisible <- visible

    /// 紧凑：出现返回键，各键抬到触控下限
    member _.SetCompact(value: bool) =
        compact <- value
        backButton.IsVisible <- value
        sync ()

    [<CLIEvent>] member _.BackClicked = back.Publish
    [<CLIEvent>] member _.NewClicked = create.Publish
    /// 参数是锚点（预设键）
    [<CLIEvent>] member _.PresetClicked = preset.Publish
