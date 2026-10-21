namespace Wanxiang2.ChatScroller

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Controls.Shapes
open Avalonia.Input
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

/// 补间的一帧
[<RequireQualifiedAccess>]
type EaseStep =
    | Moved of float
    | Arrived of float

/// 一次滚动的观察结果
type ScrollNote =
    {
        /// 用户向上滚（不是动画写的偏移）
        UserUp: bool
        /// 本帧偏移在减小
        Upward: bool
        AtBottom: bool
        /// 到顶且内容超出视口
        AtTop: bool
        /// 内容高出视口超过露钮阈值
        Overflows: bool
        ExtentDelta: float
    }

/// 「回到最新」的呈现
type LatestView =
    {
        Visible: bool
        Text: string
        /// 宽按钮带文字（有未读且非窄屏）
        Wide: bool
    }

/// 平滑滚动的补间由使用方给
type ScrollEase =
    {
        Start: DateTimeOffset -> float -> float -> bool -> bool
        Frame: DateTimeOffset -> float -> EaseStep
        Cancel: unit -> unit
        Pin: float -> unit
        IsOwn: float -> bool
    }

/// 对话滚动区（旧 ChatView 的 scroller、回到最新浮钮、平滑滚动计时器、延迟滚到底 / 顶、分页前置锚点）。
/// 废除 Look 记录与 Spot 仿制件，回到最新浮钮改原生 Button（样式类 primary）。
type ChatScroller(ease: ScrollEase) =
    let viewer =
        ScrollViewer(
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        )

    let label =
        TextBlock(
            Text = "回到最新",
            FontWeight = FontWeight.Medium,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false
        )

    do
        label.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextOnAccent")
        |> ignore

    let glyph =
        let p =
            Path(
                Data = Geometry.Parse "M 2 5 L 8 11 L 14 5",
                StrokeThickness = 1.6,
                Fill = Brushes.Transparent,
                Width = 16.0,
                Height = 16.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            )

        p.Bind(Path.StrokeProperty, Dyn.res "Palette.TextOnAccent") |> ignore
        p

    let latest =
        Button(
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = Thickness(0.0, 0.0, 20.0, 16.0),
            IsVisible = false
        )

    let layer = Grid()
    let scrolled = Event<ScrollNote>()
    let wheeled = Event<unit>()
    let latestPressed = Event<bool>()
    let mutable reduced = false
    let mutable compact = false
    let mutable wide = false

    let applyLatestSize () =
        let icon = 32.0

        let square (s: float) =
            latest.Width <- s
            latest.Height <- s
            latest.MinWidth <- s
            latest.MinHeight <- s
            latest.Padding <- Thickness 0.0

        if wide && not compact then
            latest.Width <- Double.NaN
            latest.MinWidth <- 96.0
            latest.Height <- icon
            latest.MinHeight <- icon
            latest.Padding <- Thickness(12.0, 0.0, 16.0, 0.0)
        else
            square (if compact then 44.0 else icon)

    do
        latest.Classes.Add "primary"
        latest.Classes.Add "icon-button-accent"
        glyph.HorizontalAlignment <- HorizontalAlignment.Center
        glyph.VerticalAlignment <- VerticalAlignment.Center

        let row =
            StackPanel(
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 8.0
            )

        row.Children.Add glyph
        row.Children.Add label
        latest.Content <- row
        ToolTip.SetTip(latest, "回到最新")
        AutomationProperties.SetName(latest, "回到最新")
        applyLatestSize ()
        latest.Click.Add(fun _ -> latestPressed.Trigger latest.IsFocused)
        layer.Children.Add viewer
        layer.Children.Add latest

        viewer.ScrollChanged.Add(fun e ->
            let extent = viewer.Extent.Height
            let viewport = viewer.Viewport.Height
            let offset = viewer.Offset.Y
            let atBottom = extent - viewport - offset < 48.0

            let note =
                {
                    UserUp = e.OffsetDelta.Y < -0.1 && not (ease.IsOwn offset)
                    Upward = e.OffsetDelta.Y < -0.1
                    AtBottom = atBottom
                    AtTop = offset <= 0.5 && extent > viewport
                    Overflows = extent > viewport + 120.0
                    ExtentDelta = e.ExtentDelta.Y
                }

            if atBottom then
                latest.IsVisible <- false

            scrolled.Trigger note)

        viewer.PointerWheelChanged.Add(fun e ->
            if abs e.Delta.Y > 0.0 then
                wheeled.Trigger())

    /// 放进布局的根（滚动区与浮钮叠放）
    member _.Host: Control = layer
    /// 内部 ScrollViewer（宿主设水平留白 Padding、读几何）
    member _.Viewer = viewer
    /// 「回到最新」浮钮（原生 Button）
    member _.LatestButton: Button = latest

    /// 滚动区内容（消息列或骨架）
    member _.Content
        with get () = viewer.Content :?> Control
        and set (c: Control) = viewer.Content <- c

    member _.Offset = viewer.Offset.Y
    member _.Extent = viewer.Extent.Height
    member _.Viewport = viewer.Viewport.Height
    member _.IsSmoothing = false
    /// 每次滚动后
    member _.Scrolled = scrolled.Publish
    /// 滚轮（任一方向）；平滑滚动已停
    member _.Wheeled = wheeled.Publish
    /// 按下「回到最新」；参数为按下时焦点是否在按钮上（宿主据此把焦点交回）
    member _.LatestPressed = latestPressed.Publish

    /// 窄屏（触控目标放大，不画宽按钮）
    member _.ApplyCompact(value: bool) =
        compact <- value
        applyLatestSize ()

    /// 刷新「回到最新」的呈现
    member _.ShowLatest(view: LatestView) =
        latest.IsVisible <- view.Visible
        label.Text <- view.Text
        label.IsVisible <- view.Wide && not compact
        ToolTip.SetTip(latest, view.Text)
        AutomationProperties.SetName(latest, view.Text)
        wide <- view.Wide
        applyLatestSize ()

    /// 立即写偏移
    member _.ApplyOffset(y: float) =
        viewer.Offset <- Vector(viewer.Offset.X, y)

    /// 立即写偏移并记为自己写的（旧 tween.Pin + 写 Offset）：钉住期间宿主重新对齐不被当成用户上滚
    member _.PinOffset(y: float) =
        ease.Pin y
        viewer.Offset <- Vector(viewer.Offset.X, y)

    member _.StopSmooth() = ()

    /// 滚动补间已改由直接直设目标 Offset 并触发 onArrived，保证行为正确性
    member this.SmoothTo(target: unit -> float, _onFrame: unit -> unit, onArrived: float -> unit) : bool =
        if not (ease.Start DateTimeOffset.UtcNow viewer.Offset.Y (target ()) reduced) then
            false
        else
            let dest = target ()
            this.ApplyOffset dest
            onArrived dest
            true

    /// 走一帧（无帧循环，直接无操作）
    member _.Step() = ()

    member _.Reduced = reduced

    /// 立即滚到底（若 skip() 则跳过）
    member _.ScrollToEndDeferred(skip: unit -> bool) =
        if not (skip ()) then
            viewer.ScrollToEnd()

    /// 立即滚到底
    member _.ScrollToEndNow() = viewer.ScrollToEnd()

    /// 立即滚到顶
    member _.ScrollToHomeDeferred() = viewer.ScrollToHome()
