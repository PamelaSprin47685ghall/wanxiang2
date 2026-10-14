namespace Wanxiang.UI

open System

[<RequireQualifiedAccess>]
type ScrollFrame =
    | Offset of float
    | Done of float

// 照抄旧 Design/MotionPolicy.fs EaseOutCubicEasing.Ease
module EaseOutCubic =
    let ease (t: float) =
        let p1 = 0.215
        let p2 = 0.61
        let rec solveX lo hi =
            let mid = (lo + hi) / 2.0
            let x = 3.0 * p1 * mid * (1.0 - mid) * (1.0 - mid) + 3.0 * p2 * mid * mid * (1.0 - mid) + mid * mid * mid
            if hi - lo < 1e-9 then mid
            elif x < t then solveX mid hi
            else solveX lo mid
        let u = solveX 0.0 1.0
        3.0 * (1.0 - p1) * u * (1.0 - u) * (1.0 - u) + 3.0 * p2 * u * u * (1.0 - u) + u * u * u

// 照抄旧 Views/ChatView.fs smoothScrollToCard 的分支：DispatcherTimer 换成 Some 闭包状态，
// timer.Tick 体照搬为 Frame；stopSmoothScroll 即 Cancel。
type ScrollTween() =
    let mutable smoothScrollTimer: (float * DateTimeOffset) option = None
    let mutable lastAnimatedOffset = nan
    static member DurationMs = 180.0
    static member FrameMs = 16.0
    static member NearPx = 5.0
    member _.IsRunning = smoothScrollTimer.IsSome
    member _.Start(now: DateTimeOffset, offset: float, target: float, reduced: bool) : bool =
        if abs (offset - target) < 5.0 then smoothScrollTimer <- None; false
        elif reduced then smoothScrollTimer <- None; false
        else
            smoothScrollTimer <- None
            smoothScrollTimer <- Some(offset, now)
            true
    member _.Frame(now: DateTimeOffset, target: float) : ScrollFrame =
        match smoothScrollTimer with
        | None -> ScrollFrame.Done target
        | Some(startOffset, startTime) ->
            let elapsed = (now - startTime).TotalMilliseconds
            let durationMs = 180.0
            if elapsed >= durationMs then
                smoothScrollTimer <- None
                ScrollFrame.Done target
            else
                let progress = min 1.0 (elapsed / durationMs)
                let eased = EaseOutCubic.ease progress
                let newY = startOffset + (target - startOffset) * eased
                lastAnimatedOffset <- newY
                ScrollFrame.Offset newY
    member _.Cancel() = smoothScrollTimer <- None
    member _.Pin(offset: float) = lastAnimatedOffset <- offset
    member _.IsOwnOffset(offset: float) = abs (offset - lastAnimatedOffset) < 1.0
