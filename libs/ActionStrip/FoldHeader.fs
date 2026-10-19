namespace Wanxiang2.FoldHeader

open System
open Avalonia
open Avalonia.Animation
open Avalonia.Animation.Easings
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 折叠披露头（旧 MessageCard 里思考过程 / 工具活动 / 错误细节三处共用的头部）：
/// 右指箭头展开时转到 90°，标题文本由使用方往 Caption 里加 Run，开合后发 Toggled。
type FoldHeader(expanded: bool) =
    let toggled = Event<bool>()
    let mutable open' = expanded
    let mutable reportState = true
    let rotate = RotateTransform(if expanded then 90.0 else 0.0)
    let rotation = DoubleTransition(Property = RotateTransform.AngleProperty, Duration = TimeSpan.FromMilliseconds 160.0, Easing = CubicEaseOut())
    let chevronHost = Border(VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center)
    let chevron =
        Path(
            Data = Geometry.Parse "M 4 2 L 9 7 L 4 12",
            StrokeThickness = 1.3,
            Fill = Brushes.Transparent,
            Width = 10.0,
            Height = 10.0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center)
    let caption =
        TextBlock(
            FontWeight = FontWeight.Medium,
            FontSize = 12.0,
            LineHeight = 16.0,
            VerticalAlignment = VerticalAlignment.Center)
    let row = StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0, VerticalAlignment = VerticalAlignment.Center)
    let header =
        Button(
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            Margin = Thickness 0.0,
            Padding = Thickness(4.0, 4.0),
            CornerRadius = CornerRadius 4.0,
            BorderThickness = Thickness 1.0,
            Content = row)
    let flip () =
        open' <- not open'
        rotate.Angle <- (if open' then 90.0 else 0.0)
        if reportState then AutomationProperties.SetItemStatus(header, (if open' then "已展开" else "已折叠"))
        toggled.Trigger open'
    do
        chevron.Bind(Path.StrokeProperty, Dyn.res "Palette.TextMuted") |> ignore
        caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
        let transitions = Transitions()
        transitions.Add rotation
        rotate.Transitions <- transitions
        chevron.RenderTransform <- rotate
        chevron.RenderTransformOrigin <- RelativePoint.Center
        chevronHost.Child <- chevron
        chevronHost.Width <- 14.0
        chevronHost.Height <- 14.0
        row.Children.Add chevronHost
        row.Children.Add caption
        header.Classes.Add "ghost"
        header.Classes.Add "hover-bordered"
        AutomationProperties.SetItemStatus(header, (if expanded then "已展开" else "已折叠"))
        header.Click.Add(fun _ -> flip ())

    /// 可点的头部（放进布局的根；外边距由使用方定）
    member _.Header: Button = header
    /// 标题文本块：使用方往 Inlines 里加 Run
    member _.Caption = caption
    /// 一行：箭头、[前置件]、标题、[尾随件]
    member _.Row = row
    /// 箭头的旋转（测试读角度）
    member _.Rotation = rotate
    member _.Expanded = open'
    /// 是否经 ItemStatus 报展开状态（旧工具活动头不报，默认报）
    member _.ReportState
        with get () = reportState
        and set (v: bool) =
            reportState <- v
            AutomationProperties.SetItemStatus(header, (if v then (if open' then "已展开" else "已折叠") else null))
    /// 开合之后触发，参数是新的展开状态
    member _.Toggled = toggled.Publish
    /// 在箭头之后、标题之前插一个件（旧工具活动头的状态图标）
    member _.AddLeading(c: Control) = row.Children.Insert(1, c)
    /// 在标题之后加一个件（旧思考过程头的复制按钮）
    member _.AddTrailing(c: Control) = row.Children.Add c
    /// 按一下（同点击 / Enter / Space）
    member _.Toggle() = flip ()

    /// 单入口：从 FoldHeaderVS 渲染
    member this.Render(vs: FoldHeaderVS) =
        caption.Text <- vs.Title
        if vs.Expanded <> open' then this.Toggle()

/// 标题里常用的一段 Run
module FoldHeader =
    let run (text: string) (brush: IBrush) = Run(text, Foreground = brush)
