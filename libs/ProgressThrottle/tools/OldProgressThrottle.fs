namespace Wanxiang.UI

open System

[<RequireQualifiedAccess>]
type ProgressRefresh =
    | Now
    | Schedule of delayMs: float
    | Skip

// 照抄旧 Views/ExportDialog.fs Refresh / RenderProgress 的节流字段与 ExportView.refresh 判据；
// phase 换成 highFrequency，throttleTimer 的 Start/Stop 去掉（由 Render 替身负责）。
type ProgressThrottle() =
    let mutable lastProgressRefresh = DateTimeOffset.MinValue
    let mutable throttlePending = false
    let refresh (force: bool) (highFrequency: bool) (elapsedMs: float) (windowMs: float) (pending: bool) =
        if force || not highFrequency || elapsedMs >= windowMs then ProgressRefresh.Now
        elif not pending then ProgressRefresh.Schedule(windowMs - elapsedMs)
        else ProgressRefresh.Skip
    static member WindowMs = 100.0
    member _.Pending = throttlePending
    member _.Request(now: DateTimeOffset, force: bool, highFrequency: bool) : ProgressRefresh =
        let elapsedMs = (now - lastProgressRefresh).TotalMilliseconds
        match refresh force highFrequency elapsedMs 100.0 throttlePending with
        | ProgressRefresh.Schedule delayMs ->
            throttlePending <- true
            ProgressRefresh.Schedule delayMs
        | other -> other
    member _.Rendered(now: DateTimeOffset) =
        throttlePending <- false
        lastProgressRefresh <- now
    member _.TailFired() = throttlePending
