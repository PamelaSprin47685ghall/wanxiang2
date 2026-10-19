namespace Wanxiang2.StreamCaret

open Wanxiang2.UiKit

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading

[<RequireQualifiedAccess>]
type CaretGauge =
    | Width
    | Height
    | Radius
    /// 呼吸上限（减弱动效时恒定取它）
    | OpacityMax

type StreamCaretLook =
    { Ink: IBrush
      Gauge: CaretGauge -> IObservable<float>
      /// 帧间隔（旧 MotionLedger.caretBreathFrame）
      Frame: IObservable<TimeSpan>
      Reduced: IObservable<bool> }

/// 流式光标（旧 MessageCard.renderWithStream 里的 caret）：只做透明度呼吸，尺寸几何一概不动。
/// 当前不透明度由使用方给的 `breath` 算（相位挂在跨重建持久的时钟上，重建不从峰值重启）；
/// 本控件只在挂在树上时按帧重算，减弱动效时静态取上限、不起计时器。
type StreamCaret(look: StreamCaretLook, breath: unit -> float) =
    let caret =
        Border(Background = look.Ink, HorizontalAlignment = HorizontalAlignment.Left, Margin = Thickness 0.0)
    let timer = DispatcherTimer()
    let mutable attached = false
    let mutable reduced = false
    let mutable peak = 1.0
    let refresh () =
        if reduced || not attached then
            timer.Stop()
            caret.Opacity <- peak
        else
            caret.Opacity <- breath ()
            timer.Start()
    do
        Follow.live2 caret (look.Gauge CaretGauge.Width) (look.Gauge CaretGauge.Height) (fun w h ->
            caret.Width <- w
            caret.Height <- h)
        Follow.live caret (look.Gauge CaretGauge.Radius) (fun r -> caret.CornerRadius <- CornerRadius r)
        Follow.live caret (look.Gauge CaretGauge.OpacityMax) (fun m ->
            peak <- m
            if reduced || not attached then caret.Opacity <- m)
        Follow.live caret look.Frame (fun f -> timer.Interval <- f)
        Follow.live caret look.Reduced (fun r ->
            reduced <- r
            refresh ())
        // 帧 tick 只按帧重算：相位是流逝时间的纯函数，掉帧不累积漂移
        timer.Tick.Add(fun _ -> caret.Opacity <- breath ())
        caret.AttachedToVisualTree.Add(fun _ ->
            attached <- true
            refresh ())
        caret.DetachedFromVisualTree.Add(fun _ ->
            attached <- false
            timer.Stop())

    member _.Host: Control = caret
    member _.IsBreathing = timer.IsEnabled
    /// 走一帧（测试不等真计时器）
    member _.Step() = if attached && not reduced then caret.Opacity <- breath ()
