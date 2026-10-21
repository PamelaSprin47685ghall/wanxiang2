namespace Wanxiang.UI

open System

/// 一次进度刷新请求该怎么办
[<RequireQualifiedAccess>]
type ProgressRefresh =
    /// 立即渲染（宿主渲染后调 Rendered）
    | Now
    /// 窗口内已渲染过：等这么多毫秒再补一次（宿主启动尾巴计时器）
    | Schedule of delayMs: float
    /// 尾巴已经排上，这次搭车
    | Skip

/// 旧 Views/ExportDialog.fs 进度文字节流（54-60、105-150）的记账：上次渲染时刻与「尾巴已排上」。
/// 高频态（读取中）窗口内合并，窗口末尾补一次；急件与非高频态立即渲染。
/// 尾巴计时器与控件仍归 ExportDialog。
type ProgressThrottle() =
    let mutable lastRefresh = DateTimeOffset.MinValue
    let mutable pending = false

    /// 节流窗口（旧 MotionLedger.progressTextThrottle）
    static member WindowMs = 100.0

    member _.Pending = pending

    /// 有新进度要显示。force = 急件（阶段切换、终态、保存反馈）；highFrequency = 读取中
    member _.Request(now: DateTimeOffset, force: bool, highFrequency: bool) : ProgressRefresh =
        let elapsedMs = (now - lastRefresh).TotalMilliseconds

        if force || not highFrequency || elapsedMs >= ProgressThrottle.WindowMs then
            ProgressRefresh.Now
        elif not pending then
            pending <- true
            ProgressRefresh.Schedule(ProgressThrottle.WindowMs - elapsedMs)
        else
            ProgressRefresh.Skip

    /// 宿主刚渲染了一帧（任何原因）：记下时刻，尾巴作废
    member _.Rendered(now: DateTimeOffset) =
        pending <- false
        lastRefresh <- now

    /// 尾巴计时器响了：返回是否要补渲染
    member _.TailFired() = pending
