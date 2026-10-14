namespace Wanxiang.UI

/// 一条提示的停留计时（旧 Controls/OverlayHost.fs 的 remainingMs 与 paused）。
///
/// 停留时长按语气由调用方给（旧 MotionLedger：失败 9000、警告 6500、其他 4000 毫秒）；
/// 带行内动作的至少停 8 秒。指针悬停或动作按钮有焦点时暂停；
/// 指针离开时若按钮仍有焦点则继续暂停；按钮失焦一律恢复计时（即使指针还在上面，旧行为）。
type ToastDwell(dwellMs: float, hasAction: bool) =
    let mutable remainingMs = if hasAction then max dwellMs 8000.0 else dwellMs
    let mutable paused = false

    member _.RemainingMs = remainingMs
    member _.Paused = paused

    member _.PointerEntered() = paused <- true
    member _.PointerExited(actionFocused: bool) = paused <- actionFocused
    member _.ActionFocused() = paused <- true
    member _.ActionUnfocused() = paused <- false

    /// 计时器每跳一次调用；返回 true 表示该撤下了。
    member _.Tick(intervalMs: float) : bool =
        if paused then false
        else
            remainingMs <- remainingMs - intervalMs
            remainingMs <= 0.0
