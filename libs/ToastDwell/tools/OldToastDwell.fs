namespace Wanxiang.UI

/// 旧 OverlayHost.ToastWith 的计时与暂停照抄
type ToastDwell(dwellMs: float, hasAction: bool) =
    let mutable paused = false
    let mutable remainingMs = dwellMs
    do if hasAction then remainingMs <- max remainingMs (System.TimeSpan.FromSeconds 8.0).TotalMilliseconds
    member _.RemainingMs = remainingMs
    member _.Paused = paused
    member _.PointerEntered() = paused <- true
    member _.PointerExited(actionFocused: bool) = paused <- actionFocused
    member _.ActionFocused() = paused <- true
    member _.ActionUnfocused() = paused <- false
    member _.Tick(intervalMs: float) : bool =
        let mutable expired = false
        if not paused then
            remainingMs <- remainingMs - intervalMs
            if remainingMs <= 0.0 then expired <- true
        expired
