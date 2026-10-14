namespace Wanxiang2.FoldHeader

open System
open Avalonia
open Avalonia.Animation
open Avalonia.Animation.Easings
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media

/// 本控件用到的活度量
[<RequireQualifiedAccess>]
type FoldGauge =
    /// 箭头槽边长（旧 Tokens.iconGlyph）
    | IconGlyph
    /// 头部一行内各件的间距（旧 spaceMd）
    | RowGap
    /// 头部横向内边距（旧 spaceXs）
    | PadX
    /// 头部纵向内边距（旧 spaceSm）
    | PadY
    /// 头部圆角（旧 Radius.sm）
    | Radius
    /// 描边粗细（旧 borderWidth）
    | BorderWidth
    /// 标题字号（旧 fontCaption）
    | CaptionSize
    /// 标题行高（旧 captionLineHeight）
    | CaptionLineHeight
    /// 箭头转动时长，毫秒（旧 disclosureChevronRotate）
    | RotateMs

/// 外观：笔刷与箭头图形由组装者给，表面状态反馈的样式类由 Dress 挂
type FoldHeaderLook =
    { Muted: IBrush
      Gauge: FoldGauge -> IObservable<float>
      Chevron: IBrush -> Control
      Dress: Border -> unit
      Reduced: IObservable<bool> }

/// 折叠披露头（旧 MessageCard 里思考过程 / 工具活动 / 错误细节三处共用的头部）：
/// 右指箭头展开时转到 90°，标题文本由使用方往 Caption 里加 Run，开合后发 Toggled。
type FoldHeader(look: FoldHeaderLook, expanded: bool) =
    let toggled = Event<bool>()
    let mutable open' = expanded
    let rotate = RotateTransform(if expanded then 90.0 else 0.0)
    let rotation = DoubleTransition(Property = RotateTransform.AngleProperty, Easing = CubicEaseOut())
    let chevronHost = Border(VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center)
    let chevron = look.Chevron look.Muted
    let caption =
        TextBlock(FontWeight = FontWeight.Medium, Foreground = look.Muted, VerticalAlignment = VerticalAlignment.Center)
    let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
    let header =
        Spot(
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            Child = row)
    let flip () =
        open' <- not open'
        rotate.Angle <- (if open' then 90.0 else 0.0)
        AutomationProperties.SetItemStatus(header, (if open' then "已展开" else "已折叠"))
        toggled.Trigger open'
    do
        let transitions = Transitions()
        transitions.Add rotation
        rotate.Transitions <- transitions
        Follow.live2 chevronHost (look.Gauge FoldGauge.RotateMs) look.Reduced (fun ms reduced ->
            rotation.Duration <- (if reduced then TimeSpan.Zero else TimeSpan.FromMilliseconds ms))
        chevron.HorizontalAlignment <- HorizontalAlignment.Center
        chevron.VerticalAlignment <- VerticalAlignment.Center
        chevron.RenderTransform <- rotate
        chevron.RenderTransformOrigin <- RelativePoint.Center
        chevronHost.Child <- chevron
        Follow.live chevronHost (look.Gauge FoldGauge.IconGlyph) (fun g ->
            chevronHost.Width <- g
            chevronHost.Height <- g
            chevronHost.MinWidth <- g
            chevronHost.MinHeight <- g)
        Follow.live2 caption (look.Gauge FoldGauge.CaptionSize) (look.Gauge FoldGauge.CaptionLineHeight) (fun s lh ->
            caption.FontSize <- s
            caption.LineHeight <- lh)
        row.Cursor <- new Cursor(StandardCursorType.Hand)
        Follow.live row (look.Gauge FoldGauge.RowGap) (fun g -> row.Spacing <- g)
        row.Children.Add chevronHost
        row.Children.Add caption
        Follow.live3 header (look.Gauge FoldGauge.PadX) (look.Gauge FoldGauge.PadY) (look.Gauge FoldGauge.Radius) (fun x y r ->
            header.Padding <- Thickness(x, y)
            header.CornerRadius <- CornerRadius r)
        Follow.live header (look.Gauge FoldGauge.BorderWidth) (fun w -> header.BorderThickness <- Thickness w)
        look.Dress header
        // 旧 SetRole Button 由 Spot 的自动化层报；旧 SetExpanded 改报 ItemStatus（同 MarkdownView 的展开按钮）
        AutomationProperties.SetItemStatus(header, (if expanded then "已展开" else "已折叠"))
        header.OnInvoke flip

    /// 可点的头部（放进布局的根；外边距由使用方定）
    member _.Header: Border = header
    /// 标题文本块：使用方往 Inlines 里加 Run
    member _.Caption = caption
    /// 一行：箭头、[前置件]、标题、[尾随件]
    member _.Row = row
    /// 箭头的旋转（测试读角度）
    member _.Rotation = rotate
    member _.Expanded = open'
    /// 开合之后触发，参数是新的展开状态
    member _.Toggled = toggled.Publish
    /// 在箭头之后、标题之前插一个件（旧工具活动头的状态图标）
    member _.AddLeading(c: Control) = row.Children.Insert(1, c)
    /// 在标题之后加一个件（旧思考过程头的复制按钮）
    member _.AddTrailing(c: Control) = row.Children.Add c
    /// 按一下（同点击 / Enter / Space）
    member _.Toggle() = header.Invoke()

/// 标题里常用的一段 Run
module FoldHeader =
    let run (text: string) (brush: IBrush) = Run(text, Foreground = brush)
