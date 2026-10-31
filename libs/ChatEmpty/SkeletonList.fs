namespace Wanxiang2.SkeletonList

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media

/// 动态资源绑定辅助
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 历史加载骨架（助手 → 用户 → 助手三块占位）。
/// 视觉呼吸采用 Theme 声明式动效类 anim-breathe，无独立定时器与 Look。
/// 阅读列宽 / 消息间距 / 尾部留白走主题活键（Metric.ReadingWidth / MessageGap / Margin.MessageEnd）。
type SkeletonList() =
    let panel =
        StackPanel(
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 24.0,
            MaxWidth = 748.0,
            Margin = Thickness(0.0, 0.0, 0.0, 48.0),
            IsVisible = false
        )

    do
        panel.Bind(StackPanel.SpacingProperty, Dyn.res "Metric.MessageGap") |> ignore
        panel.Bind(Layoutable.MaxWidthProperty, Dyn.res "Metric.ReadingWidth") |> ignore
        panel.Bind(Layoutable.MarginProperty, Dyn.res "Margin.MessageEnd") |> ignore

    let textBar (height: float) (fraction: float) =
        let bar =
            Border(
                Height = height,
                CornerRadius = CornerRadius 6.0,
                Opacity = 0.65,
                HorizontalAlignment = HorizontalAlignment.Stretch
            )

        bar.Bind(Border.BackgroundProperty, Dyn.res "Palette.BorderSoft") |> ignore
        let grid = Grid(HorizontalAlignment = HorizontalAlignment.Stretch)
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(fraction, GridUnitType.Star)))
        grid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength(max 0.01 (1.0 - fraction), GridUnitType.Star)))
        Grid.SetColumn(bar, 0)
        grid.Children.Add bar
        grid :> Control

    let avatar (opacityVal: float) (gap: bool) =
        let a =
            Border(
                Width = 28.0,
                Height = 28.0,
                CornerRadius = CornerRadius 999.0,
                Opacity = opacityVal,
                VerticalAlignment = VerticalAlignment.Top
            )

        a.Bind(Border.BackgroundProperty, Dyn.res "Palette.BorderSoft") |> ignore

        if gap then
            a.Margin <- Thickness(0.0, 0.0, 12.0, 0.0)
        else
            a.Margin <- Thickness 0.0

        a

    let assistant (lines: (float * float) list) =
        let a = avatar 0.85 true

        let column =
            StackPanel(
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Spacing = 8.0
            )

        for h, w in lines do
            column.Children.Add(textBar h w)

        let dock =
            DockPanel(LastChildFill = true, HorizontalAlignment = HorizontalAlignment.Stretch)

        DockPanel.SetDock(a, Dock.Left)
        dock.Children.Add a
        dock.Children.Add column
        AutomationProperties.SetName(dock, "正在加载助手消息")
        dock :> Control

    let user () =
        let bubble =
            Border(
                BorderThickness = Thickness 1.0,
                CornerRadius = CornerRadius(12.0, 12.0, 6.0, 12.0),
                Opacity = 0.55,
                Width = 260.0,
                Height = 40.0,
                HorizontalAlignment = HorizontalAlignment.Right
            )

        bubble.Bind(Border.BackgroundProperty, Dyn.res "Palette.BorderSoft") |> ignore

        bubble.Bind(Border.BorderBrushProperty, Dyn.res "Palette.HairlineStrong")
        |> ignore

        let row =
            StackPanel(
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12.0
            )

        row.Children.Add bubble
        row.Children.Add(avatar 0.55 false)
        AutomationProperties.SetName(row, "正在加载用户消息")
        row :> Control

    do
        AutomationProperties.SetName(panel, "正在加载历史消息")
        panel.Children.Add(assistant [ (14.0, 0.45); (13.0, 0.85); (13.0, 0.70); (13.0, 0.35) ])
        panel.Children.Add(user ())
        panel.Children.Add(assistant [ (14.0, 0.60); (13.0, 0.90); (13.0, 0.75); (13.0, 0.50) ])

    /// 放进布局的根（宿主把它设为滚动区内容）
    member _.Host: Control = panel
    member _.IsShown = panel.IsVisible
    member _.IsBreathing = panel.Classes.Contains "anim-breathe"

    /// 显示并开始呼吸（旧 ShowSkeletonLoading 的骨架部分）
    member _.Show() =
        panel.IsVisible <- true

        if not (panel.Classes.Contains "anim-breathe") then
            panel.Classes.Add "anim-breathe"

    /// 停止呼吸并隐藏（旧 HideSkeletonLoading 的骨架部分）
    member _.Hide() =
        if panel.IsVisible then
            panel.Classes.Remove "anim-breathe" |> ignore
            panel.IsVisible <- false
