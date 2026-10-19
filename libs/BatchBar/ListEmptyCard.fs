namespace Wanxiang2.ListEmptyCard

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media

/// 卡的样式（与 SidebarEmptyKind 一一对应，由组合者换算）
[<RequireQualifiedAccess>]
type EmptyCardStyle =
    /// 一句说明 + 骨架 + 行动键
    | Note
    /// 品牌标 + 标题 + 骨架 + 行动键
    | Brand
    /// 标题 + 说明
    | Plain

/// 列表空态卡（迁自旧 Views/Sidebar.fs 的 emptyStateHost、emptySkeleton、emptyActionButton 与 Rebuild 末段的卡）。
/// 只在样式翻转时换卡；骨架与行动键跨卡复用，焦点与读屏名连续。
type ListEmptyCard(look: ListEmptyCardLook) =
    let kit = Kit(look)
    let action = Event<unit>()
    let mutable current: EmptyCardStyle option = None

    let host = Border(IsVisible = false, VerticalAlignment = VerticalAlignment.Center)
    let mutable x3 = 0.0
    let mutable x6 = 0.0
    let margin () = host.Margin <- Thickness(x3, x6, x3, x6)

    let skeleton =
        let panel = kit.VStack(look.SpaceMd, [])
        panel.HorizontalAlignment <- HorizontalAlignment.Center
        panel.IsVisible <- false
        kit.Live(look.SpaceMd, fun v -> panel.Margin <- Thickness(0.0, v, 0.0, 0.0))
        // 错落宽度：比例是形状，不是样式
        for fraction in [ 0.85; 0.60; 0.72 ] do
            let bar = Border(Background = look.SkeletonFill, HorizontalAlignment = HorizontalAlignment.Stretch)
            kit.Live(look.SkeletonBarHeight, fun v -> bar.Height <- v)
            kit.Live(look.RadiusSm, fun v -> bar.CornerRadius <- CornerRadius v)
            kit.Live(look.SkeletonOpacity, fun v -> bar.Opacity <- v)
            let row = Grid(HorizontalAlignment = HorizontalAlignment.Stretch)
            row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(fraction, GridUnitType.Star)))
            row.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(max 0.01 (1.0 - fraction), GridUnitType.Star)))
            Grid.SetColumn(bar, 0)
            row.Children.Add bar
            panel.Children.Add row
        AutomationProperties.SetName(panel, "正在加载对话")
        panel
    let button =
        let b = kit.Button(Tone.Primary, "", fun () -> action.Trigger())
        b.HorizontalAlignment <- HorizontalAlignment.Center
        b.IsVisible <- false
        b

    let detach (c: Control) =
        match c.Parent with
        | :? Panel as p -> p.Children.Remove c |> ignore
        | _ -> ()

    let centered (t: TextBlock) =
        t.HorizontalAlignment <- HorizontalAlignment.Center
        t.TextAlignment <- TextAlignment.Center
        t.TextWrapping <- TextWrapping.Wrap
        t

    let build style (title: string) (note: string) =
        detach skeleton
        detach button
        match style with
        | EmptyCardStyle.Note ->
            let stack = kit.VStack(look.SpaceMd, [ centered (kit.Caption note) :> Control; skeleton :> Control; button :> Control ])
            kit.Live(look.SpaceXl, fun v -> stack.Margin <- Thickness v)
            stack :> Control
        | EmptyCardStyle.Brand ->
            let logo = Border(HorizontalAlignment = HorizontalAlignment.Center)
            kit.Live(look.LogoSize, fun v -> logo.Child <- look.Logo v)
            let parts: Control list =
                [ yield logo :> Control
                  if not (String.IsNullOrWhiteSpace title) then yield centered (kit.Heading title) :> Control
                  if not (String.IsNullOrWhiteSpace note) then yield centered (kit.Caption note) :> Control
                  yield skeleton :> Control
                  yield button :> Control ]
            kit.VStack(look.SpaceMd, parts) :> Control
        | EmptyCardStyle.Plain ->
            kit.VStack(look.SpaceMd, [ centered (kit.Label title) :> Control; centered (kit.Caption note) :> Control ]) :> Control

    do
        kit.Live(look.Space3xl, fun v -> x3 <- v; margin ())
        kit.Live(look.Space6xl, fun v -> x6 <- v; margin ())

    member _.View = host :> Control
    member _.ActionButton = button :> Control
    member _.Skeleton = skeleton :> Control
    member _.Style = current
    member _.IsVisible = host.IsVisible

    /// 显示：样式翻转才换卡，同样式只刷可见性
    member _.Show(style: EmptyCardStyle, title: string, note: string, skeletonVisible: bool, actionVisible: bool) =
        if current <> Some style then
            current <- Some style
            host.Child <- build style title note
        skeleton.IsVisible <- skeletonVisible
        button.IsVisible <- actionVisible
        host.IsVisible <- true

    member _.Hide() = host.IsVisible <- false

    /// 行动键文案（读屏名与提示同步）
    member _.SetAction(label: string) =
        button.SetText label
        ToolTip.SetTip(button, label)
        AutomationProperties.SetHelpText(button, label)

    member _.SetActionVisible(visible: bool) = button.IsVisible <- visible

    member _.FocusAction() =
        if button.IsEffectivelyVisible && button.IsEnabled then
            button.Focus Avalonia.Input.NavigationMethod.Directional |> ignore
            true
        else false

    [<CLIEvent>] member _.ActionClicked = action.Publish
