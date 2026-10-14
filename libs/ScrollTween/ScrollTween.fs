namespace Wanxiang.UI

open System

/// 一帧补间的结果
[<RequireQualifiedAccess>]
type ScrollFrame =
    /// 还在途中：把偏移设到这里
    | Offset of float
    /// 到点：宿主落到终点（贴目标或 ScrollToEnd），补间已结束
    | Done of float

/// 统一缓动曲线 easeOutCubic（cubic-bezier 0.215, 0.61, 0.355, 1），旧 MotionPolicy.EaseOutCubicEasing 的数学
module EaseOutCubic =
    /// 二分求参数 u 使 x(u) = t，再代入 y(u)
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

/// 旧 Views/ChatView.fs 平滑滚动（smoothScrollToCard、SmoothScrollToBeginning、SmoothScrollToEnd）的补间记账：
/// 起点、起始时刻、是否在途、最近一次由动画写下的偏移。帧计时器与 ScrollViewer 仍归 ChatView；
/// 目标每帧由宿主现取（布局可能在途中变化），本对象只按时间与曲线给出偏移。
type ScrollTween() =
    let mutable running = false
    let mutable startOffset = 0.0
    let mutable startTime = DateTimeOffset.MinValue
    let mutable lastAnimated = nan

    /// 补间总时长（旧 MotionLedger.smoothScrollDuration）
    static member DurationMs = 180.0
    /// 帧间隔（旧 MotionLedger.smoothScrollFrame）
    static member FrameMs = 16.0
    /// 起点离目标不到这么远就不做动画
    static member NearPx = 5.0

    member _.IsRunning = running

    /// 开始补间。减弱动效或已在目标附近时不开始、返回 false（宿主直接落位）；开始前先停掉在途的一段
    member _.Start(now: DateTimeOffset, offset: float, target: float, reduced: bool) : bool =
        running <- false
        if reduced || abs (target - offset) < ScrollTween.NearPx then false
        else
            startOffset <- offset
            startTime <- now
            running <- true
            true

    /// 推进一帧：target 为本帧现取的目标
    member _.Frame(now: DateTimeOffset, target: float) : ScrollFrame =
        let elapsed = (now - startTime).TotalMilliseconds
        if not running || elapsed >= ScrollTween.DurationMs then
            running <- false
            ScrollFrame.Done target
        else
            let eased = EaseOutCubic.ease (min 1.0 (elapsed / ScrollTween.DurationMs))
            let y = startOffset + (target - startOffset) * eased
            lastAnimated <- y
            ScrollFrame.Offset y

    /// 停掉在途补间（用户滚轮、换对话、新一段补间）
    member _.Cancel() = running <- false

    /// 宿主自己把偏移贴到某处，并希望随后的 ScrollChanged 不被当成用户滚动
    member _.Pin(offset: float) = lastAnimated <- offset

    /// 这个偏移是不是动画（或 Pin）刚写下的：差不到 1 像素
    member _.IsOwnOffset(offset: float) = abs (offset - lastAnimated) < 1.0
