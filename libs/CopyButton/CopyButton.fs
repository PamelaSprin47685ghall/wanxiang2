namespace Wanxiang2.CopyButton

open System
open Avalonia.Controls
open Avalonia.Threading

/// 复制按钮此刻的形态。
[<RequireQualifiedAccess>]
type CopyState =
    /// 复制图标、原提示
    | Default
    /// 勾号「已复制」，透明度 1.0
    | Confirmed
    /// 勾号「已复制」，透明度压到 FadeOpacity
    | Fading

/// 复制确认的外观与时长。全部由主题管理者提供活的实例。
type CopyButtonLook =
    { /// 淡出时的不透明度（旧 Tokens.opacityCopyConfirmFade）
      FadeOpacity: IObservable<float>
      /// 勾号保持时长（旧 MotionLedger.copyConfirmationHold）
      Hold: IObservable<TimeSpan>
      /// 淡出时长（旧 MotionLedger.copyConfirmationFade）
      Fade: IObservable<TimeSpan> }

/// 复制按钮的确认反馈（迁自旧 Controls/CopyFeedback.fs，确认记账照旧 CopyConfirm 自己持有）：
/// 复制后先淡一下，保持勾号一段时间后复位；减弱动效不淡出。离开可视树立即复位。
/// 图标、提示文字、读屏名由使用方按 StateChanged 去换。
type CopyButton(button: Control, look: CopyButtonLook, clock: unit -> DateTimeOffset) =
    let mutable fadeOpacity = 1.0
    let mutable hold = TimeSpan.Zero
    let mutable fade = TimeSpan.Zero
    let mutable confirmedUntil: DateTimeOffset option = None
    let mutable fadeUntil: DateTimeOffset option = None
    let mutable timer: DispatcherTimer option = None
    let changed = Event<CopyState>()
    let mutable state = CopyState.Default

    let stop () =
        timer |> Option.iter (fun t -> t.Stop())
        timer <- None

    let advance (now: DateTimeOffset) =
        match fadeUntil with
        | Some t when now >= t -> fadeUntil <- None
        | _ -> ()
        match confirmedUntil with
        | Some t when now >= t ->
            confirmedUntil <- None
            fadeUntil <- None
        | _ -> ()
        match confirmedUntil, fadeUntil with
        | None, _ -> CopyState.Default
        | Some _, Some _ -> CopyState.Fading
        | Some _, None -> CopyState.Confirmed

    let nextDue () =
        match fadeUntil with
        | Some f -> Some f
        | None -> confirmedUntil

    let show s =
        button.Opacity <- (if s = CopyState.Fading then fadeOpacity else 1.0)
        state <- s
        changed.Trigger s

    let rec render () =
        stop ()
        show (advance (clock ()))
        match nextDue () with
        | Some due ->
            let wait = due - clock ()
            let t = DispatcherTimer(Interval = (if wait > TimeSpan.FromMilliseconds 1.0 then wait else TimeSpan.FromMilliseconds 1.0))
            t.Tick.Add(fun _ ->
                t.Stop()
                if timer = Some t then render ())
            timer <- Some t
            t.Start()
        | None -> ()

    do
        look.FadeOpacity.Subscribe(fun v ->
            fadeOpacity <- v
            if state = CopyState.Fading then button.Opacity <- v) |> ignore
        look.Hold.Subscribe(fun v -> hold <- v) |> ignore
        look.Fade.Subscribe(fun v -> fade <- v) |> ignore
        button.DetachedFromVisualTree.Add(fun _ ->
            stop ()
            confirmedUntil <- None
            fadeUntil <- None
            show CopyState.Default)

    new(button, look) = CopyButton(button, look, fun () -> DateTimeOffset.UtcNow)

    /// 形态变了（含复位）。
    [<CLIEvent>]
    member _.StateChanged = changed.Publish

    member _.State = state

    /// 刚复制。reduced：减弱动效（不淡出）。
    member _.Copied(reduced: bool) =
        let now = clock ()
        confirmedUntil <- Some(now + hold)
        fadeUntil <- if reduced then None else Some(now + fade)
        render ()

    /// 按当前时刻推进一次（计时器到点时调用；测试也可直接调用）。
    member _.Refresh() = render ()
