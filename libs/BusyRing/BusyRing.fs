namespace Wanxiang2.BusyRing

open Wanxiang2.UiKit

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Media
open Avalonia.Threading

/// 用到的笔刷
[<RequireQualifiedAccess>]
type BusyInk =
    /// 减弱动效时露出的整圈底轨（旧 Tokens.border）
    | Track
    /// 转动的弧（旧 Tokens.accent）
    | Arc

/// 外观：笔刷是活实例；描边粗细、帧间隔、减弱动效都是活值
type BusyRingLook =
    { Ink: BusyInk -> IBrush
      StrokeWidth: IObservable<float>
      Frame: IObservable<TimeSpan>
      Reduced: IObservable<bool> }

/// 忙碌转圈（旧 Ui.spinner）：16×16 画布上的弧经 Viewbox 缩到目标尺寸，挂在树上才转；
/// 减弱动效时停在 0° 并露出整圈底轨。
type BusyRing(look: BusyRingLook, size: float) =
    let track =
        Path(
            Data = Geometry.Parse "M 8 1.6 A 6.4 6.4 0 1 1 7.99 1.6",
            Stroke = look.Ink BusyInk.Track,
            Fill = Brushes.Transparent,
            Stretch = Stretch.None,
            IsVisible = false)
    let arc =
        Path(
            Data = Geometry.Parse "M 8 1.6 A 6.4 6.4 0 0 1 14.4 8",
            Stroke = look.Ink BusyInk.Arc,
            StrokeLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.None)
    // 画布尺寸是弧几何的坐标系，不是布局量
    let canvas = Canvas(Width = 16.0, Height = 16.0)
    let view =
        Viewbox(
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Child = canvas,
            RenderTransform = RotateTransform 0.0,
            RenderTransformOrigin = RelativePoint.Center)
    let timer = DispatcherTimer()
    let mutable angle = 0.0
    let mutable attached = false
    let mutable reduced = false
    let step () =
        angle <- (angle + 18.0) % 360.0
        view.RenderTransform <- RotateTransform angle
    let refresh () =
        if reduced then
            timer.Stop()
            angle <- 0.0
            view.RenderTransform <- RotateTransform 0.0
            track.IsVisible <- true
        else
            track.IsVisible <- false
            if attached then timer.Start() else timer.Stop()
    do
        canvas.Children.Add track
        canvas.Children.Add arc
        Follow.live view look.StrokeWidth (fun w ->
            track.StrokeThickness <- w
            arc.StrokeThickness <- w)
        Follow.live view look.Frame (fun f -> timer.Interval <- f)
        Follow.live view look.Reduced (fun r ->
            reduced <- r
            refresh ())
        timer.Tick.Add(fun _ -> step ())
        view.AttachedToVisualTree.Add(fun _ ->
            attached <- true
            refresh ())
        view.DetachedFromVisualTree.Add(fun _ ->
            attached <- false
            timer.Stop())

    member _.Host: Control = view
    member _.Angle = angle
    member _.IsSpinning = timer.IsEnabled
    /// 走一帧（测试不等真计时器）
    member _.Step() = step ()
