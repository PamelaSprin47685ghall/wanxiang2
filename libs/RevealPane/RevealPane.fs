namespace Wanxiang2.RevealPane

open Wanxiang2.UiKit

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Threading

/// 本控件用到的活度量
[<RequireQualifiedAccess>]
type RevealGauge =
    /// 视口与「展开全部」按钮之间的竖向间距（旧 spaceXs）
    | Gap
    /// 视口右侧给滚动条让出的槽（旧 nestedScrollGutter）
    | Gutter

/// 外观：度量取活值；幽灵按钮的样式类由 Dress 挂上
type RevealPaneLook =
    { Gauge: RevealGauge -> IObservable<float>
      Dress: Border -> unit }

/// 限高视口（旧 MessageCard.detailViewport）：内容超出 maxHeight 时露出「展开全部 / 收起」幽灵按钮。
/// 首次布局前视口高度为 0，不据此判定裁切，免得短内容亮出假按钮。
type RevealPane(look: RevealPaneLook, content: Control, maxHeight: float, initiallyVisible: bool) =
    let scroller =
        ScrollViewer(
            Content = content,
            ClipToBounds = true,
            MaxHeight = maxHeight,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsVisible = initiallyVisible)
    let label = TextBlock(Text = "展开全部", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center)
    let button =
        Spot(
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
            IsVisible = false,
            Child = label)
    let host = StackPanel(Orientation = Orientation.Vertical, IsVisible = initiallyVisible)
    let mutable visible = initiallyVisible
    let mutable full = false
    let setText (text: string) =
        label.Text <- text
        // 文字按钮自解释：不挂 tooltip，只留读屏名
        AutomationProperties.SetName(button, text)
    let refresh () =
        let clipped = scroller.Viewport.Height > 0.0 && scroller.Extent.Height > scroller.Viewport.Height + 1.0
        button.IsVisible <- visible && (full || clipped)
    do
        look.Dress button
        AutomationProperties.SetName(button, "展开全部")
        Follow.live scroller (look.Gauge RevealGauge.Gutter) (fun g -> scroller.Margin <- Thickness(0.0, 0.0, g, 0.0))
        Follow.live host (look.Gauge RevealGauge.Gap) (fun g -> host.Spacing <- g)
        host.Children.Add scroller
        host.Children.Add button
        scroller.LayoutUpdated.Add(fun _ -> if visible then refresh ())
        scroller.PropertyChanged.Add(fun e ->
            if e.Property = ScrollViewer.ExtentProperty || e.Property = ScrollViewer.ViewportProperty then refresh ())
        button.OnInvoke(fun () ->
            full <- not full
            scroller.MaxHeight <- (if full then Double.PositiveInfinity else maxHeight)
            setText (if full then "收起" else "展开全部")
            Dispatcher.UIThread.Post refresh)

    /// 放进布局的根
    member _.Host: Control = host
    /// 内部滚动视口（测试与宿主读裁切状态用）
    member _.Scroller = scroller
    /// 「展开全部 / 收起」按钮
    member _.Toggle: Border = button
    member _.IsFull = full
    member _.IsShown = visible
    /// 展开 / 收起整块；收起时顺带退出全文态
    member _.SetVisible(value: bool) =
        visible <- value
        scroller.IsVisible <- value
        host.IsVisible <- value
        if not value && full then
            full <- false
            scroller.MaxHeight <- maxHeight
            setText "展开全部"
        Dispatcher.UIThread.Post refresh
    /// 切换全文态（同按下按钮）
    member _.ToggleFull() = button.Invoke()
