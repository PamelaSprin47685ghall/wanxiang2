namespace Wanxiang2.ChatScroller

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

/// 用到的笔刷
[<RequireQualifiedAccess>]
type ScrollerInk =
    /// 「回到最新」实心底（旧 Tokens.accent）
    | Accent
    /// 按钮上的图标与文字（旧 Tokens.textOnAccent）
    | OnAccent

/// 用到的活度量
[<RequireQualifiedAccess>]
type ScrollerGauge =
    /// 距底不到这么多算贴底（旧 ContentMetrics.scrollBottomThreshold）
    | BottomThreshold
    /// 内容比视口高出这么多才露「回到最新」（旧 scrollBottomRevealThreshold）
    | RevealThreshold
    /// 图标按钮边长（旧 Tokens.iconButton）
    | IconButton
    /// 窄屏触控目标（旧 LayoutPolicy.compactActionTarget）
    | CompactTarget
    /// 宽按钮最小宽（旧 scrollToBottomMinWidth）
    | WideMinWidth
    | FontSmall
    | SpaceMd
    | SpaceXl
    | Space2xl
    | Space3xl

/// 用到的图标
[<RequireQualifiedAccess>]
type ScrollerGlyph =
    | ArrowDown

/// 外观
type ChatScrollerLook =
    { Ink: ScrollerInk -> IBrush
      Gauge: ScrollerGauge -> IObservable<float>
      Glyph: ScrollerGlyph -> IBrush -> Control
      /// 平滑滚动帧间隔（旧 MotionLedger.smoothScrollFrame）
      Frame: IObservable<TimeSpan>
      Reduced: IObservable<bool>
      /// 强调色图标按钮的样式类（旧 Ui.iconButtonAccent）
      DressLatest: Border -> unit
      /// 浮钮阴影（旧 Tokens.shadowPopup）
      Shadow: unit -> BoxShadows }

/// 补间的一帧（限定访问）
[<RequireQualifiedAccess>]
type EaseStep =
    /// 这一帧写到的偏移
    | Moved of float
    /// 到达；带最终偏移
    | Arrived of float

/// 平滑滚动的补间由使用方给（曲线与“动画自己写的偏移”记账在别处，如 ScrollTween）
type ScrollEase =
    { Start: DateTimeOffset -> float -> float -> bool -> bool
      Frame: DateTimeOffset -> float -> EaseStep
      Cancel: unit -> unit
      Pin: float -> unit
      IsOwn: float -> bool }

/// 一次滚动的观察结果
type ScrollNote =
    { /// 用户向上滚（不是动画写的偏移）
      UserUp: bool
      /// 本帧偏移在减小
      Upward: bool
      AtBottom: bool
      /// 到顶且内容超出视口
      AtTop: bool
      /// 内容高出视口超过露钮阈值
      Overflows: bool
      ExtentDelta: float }

/// 「回到最新」的呈现
type LatestView =
    { Visible: bool
      Text: string
      /// 宽按钮带文字（有未读且非窄屏）
      Wide: bool }

/// 对话滚动区（旧 ChatView 的 scroller、回到最新浮钮、平滑滚动计时器、延迟滚到底 / 顶、分页前置锚点）。
/// 贴底、未读、补间曲线、挂载窗口的记账由使用方持有；本控件只报滚动观察并执行滚动。帧计时器属于控件自己。
type ChatScroller(look: ChatScrollerLook, ease: ScrollEase) =
    let gauge = look.Gauge
    let viewer =
        ScrollViewer(
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Stretch)
    let label =
        TextBlock(Text = "回到最新", Foreground = look.Ink ScrollerInk.OnAccent, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center, IsVisible = false)
    let glyph = look.Glyph ScrollerGlyph.ArrowDown (look.Ink ScrollerInk.OnAccent)
    let latest =
        Spot(
            Background = look.Ink ScrollerInk.Accent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsVisible = false)
    let layer = Grid()
    let timer = DispatcherTimer()
    let scrolled = Event<ScrollNote>()
    let wheeled = Event<unit>()
    let latestPressed = Event<bool>()
    let mutable frame: unit -> unit = ignore
    let mutable reduced = false
    let mutable compact = false
    let mutable wide = false
    let mutable anchor: (float * float) option = None

    let stopSmooth () =
        timer.Stop()
        frame <- ignore

    let applyLatestSize () =
        let icon = Follow.now (gauge ScrollerGauge.IconButton)
        let square (s: float) =
            latest.Width <- s
            latest.Height <- s
            latest.MinWidth <- s
            latest.MinHeight <- s
            latest.Padding <- Thickness 0.0
        if wide && not compact then
            latest.Width <- Double.NaN
            latest.MinWidth <- Follow.now (gauge ScrollerGauge.WideMinWidth)
            latest.Height <- icon
            latest.MinHeight <- icon
            latest.Padding <- Thickness(Follow.now (gauge ScrollerGauge.SpaceXl), 0.0, Follow.now (gauge ScrollerGauge.Space2xl), 0.0)
        else square (if compact then Follow.now (gauge ScrollerGauge.CompactTarget) else icon)

    do
        look.DressLatest latest
        latest.BoxShadow <- look.Shadow ()
        glyph.HorizontalAlignment <- HorizontalAlignment.Center
        glyph.VerticalAlignment <- VerticalAlignment.Center
        let row = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
        Follow.live row (gauge ScrollerGauge.SpaceMd) (fun s -> row.Spacing <- s)
        Follow.live label (gauge ScrollerGauge.FontSmall) (fun s -> label.FontSize <- s)
        row.Children.Add glyph
        row.Children.Add label
        latest.Child <- row
        ToolTip.SetTip(latest, "回到最新")
        AutomationProperties.SetName(latest, "回到最新")
        Follow.live2 latest (gauge ScrollerGauge.Space3xl) (gauge ScrollerGauge.Space2xl) (fun r b ->
            latest.Margin <- Thickness(0.0, 0.0, r, b))
        Follow.live latest (gauge ScrollerGauge.IconButton) (fun _ -> applyLatestSize ())
        latest.OnInvoke(fun () -> latestPressed.Trigger latest.IsFocused)
        layer.Children.Add viewer
        layer.Children.Add latest
        Follow.live viewer look.Frame (fun f -> timer.Interval <- f)
        Follow.live viewer look.Reduced (fun r -> reduced <- r)
        timer.Tick.Add(fun _ -> frame ())
        viewer.DetachedFromVisualTree.Add(fun _ -> stopSmooth ())
        viewer.ScrollChanged.Add(fun e ->
            let extent = viewer.Extent.Height
            let viewport = viewer.Viewport.Height
            let offset = viewer.Offset.Y
            let atBottom = extent - viewport - offset < Follow.now (gauge ScrollerGauge.BottomThreshold)
            let note =
                { UserUp = e.OffsetDelta.Y < -0.1 && not (ease.IsOwn offset)
                  Upward = e.OffsetDelta.Y < -0.1
                  AtBottom = atBottom
                  AtTop = offset <= 0.5 && extent > viewport
                  Overflows = extent > viewport + Follow.now (gauge ScrollerGauge.RevealThreshold)
                  ExtentDelta = e.ExtentDelta.Y }
            if atBottom then latest.IsVisible <- false
            scrolled.Trigger note)
        // 滚轮不论方向都是“用户此刻要动”：停掉平滑滚动
        viewer.PointerWheelChanged.Add(fun e ->
            if abs e.Delta.Y > 0.0 then
                stopSmooth ()
                wheeled.Trigger())

    /// 放进布局的根（滚动区与浮钮叠放）
    member _.Host: Control = layer
    /// 内部 ScrollViewer（宿主设水平留白 Padding、读几何）
    member _.Viewer = viewer
    /// 「回到最新」浮钮
    member _.LatestButton: Border = latest
    /// 滚动区内容（消息列或骨架）
    member _.Content
        with get () = viewer.Content :?> Control
        and set (c: Control) = viewer.Content <- c
    member _.Offset = viewer.Offset.Y
    member _.Extent = viewer.Extent.Height
    member _.Viewport = viewer.Viewport.Height
    member _.IsSmoothing = timer.IsEnabled
    /// 每次滚动后
    member _.Scrolled = scrolled.Publish
    /// 滚轮（任一方向）；平滑滚动已停
    member _.Wheeled = wheeled.Publish
    /// 按下「回到最新」；参数为按下时焦点是否在按钮上（宿主据此把焦点交回）
    member _.LatestPressed = latestPressed.Publish

    /// 窄屏（触控目标放大，不画宽按钮）
    member _.SetCompact(value: bool) =
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
    member _.SetOffset(y: float) = viewer.Offset <- Vector(viewer.Offset.X, y)

    member _.StopSmooth() = stopSmooth ()

    /// 平滑滚到 target()（每帧重取目标）。减弱动效或已很近时返回 false，不起动画；到达后回调 onArrived 最终偏移
    member this.SmoothTo(target: unit -> float, onFrame: unit -> unit, onArrived: float -> unit) : bool =
        if not (ease.Start DateTimeOffset.UtcNow viewer.Offset.Y (target ()) reduced) then false
        else
            stopSmooth ()
            frame <- fun () ->
                match ease.Frame DateTimeOffset.UtcNow (target ()) with
                | EaseStep.Arrived y ->
                    stopSmooth ()
                    this.SetOffset y
                    onArrived y
                | EaseStep.Moved y ->
                    this.SetOffset y
                    onFrame ()
            timer.Start()
            true

    /// 走一帧（测试不等真计时器）
    member _.Step() = if timer.IsEnabled then frame ()

    member _.Reduced = reduced

    /// 滚到底要等布局把新高度算进 Extent：按 Background 优先级重试几次；skip() 为真的那一拍不滚（定位钉住）
    member _.ScrollToEndDeferred(skip: unit -> bool) =
        let rec attempt remaining =
            Dispatcher.UIThread.Post(
                (fun () ->
                    if not (skip ()) then viewer.ScrollToEnd()
                    if remaining > 0 then attempt (remaining - 1)),
                DispatcherPriority.Background)
        attempt 3

    /// 立即滚到底
    member _.ScrollToEndNow() = viewer.ScrollToEnd()

    /// 与 ScrollToEndDeferred 对称地重试滚到顶
    member _.ScrollToHomeDeferred() =
        let rec attempt remaining =
            Dispatcher.UIThread.Post(
                (fun () ->
                    viewer.ScrollToHome()
                    if remaining > 0 then attempt (remaining - 1)),
                DispatcherPriority.Background)
        attempt 3

    /// 分页前置前记下几何（旧 BeginHistoryPrependAnchor）
    member _.BeginPrependAnchor() = anchor <- Some(viewer.Extent.Height, viewer.Offset.Y)
    member _.PrependAnchorPending = anchor.IsSome
    member _.CancelPrependAnchor() = anchor <- None

    /// 前置内容渲染后用 extent 增量补偿偏移，原先视口内的内容留在原位
    member this.RestorePrependAnchorDeferred() =
        match anchor with
        | None -> ()
        | Some(oldExtent, oldOffset) ->
            anchor <- None
            Dispatcher.UIThread.Post(
                (fun () ->
                    let delta = max 0.0 (viewer.Extent.Height - oldExtent)
                    this.SetOffset(max 0.0 (oldOffset + delta))),
                DispatcherPriority.Background)
