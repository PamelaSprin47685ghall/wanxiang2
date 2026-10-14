namespace Wanxiang.Client

// 照抄旧 AppShell.fs 的 Connect 开头（connectAttempt、取消 reconnectCts）、ResumeReconnectNow、ScheduleReconnect
// 与两处退避归零；CancellationTokenSource 原样保留，计时器换成票据，到点由 Fire 模拟
open System.Threading

type ReconnectLoop(baseDelayMs: int, next: int -> int) =
    let mutable connectAttempt = 0
    let mutable reconnectDelayMs = baseDelayMs
    let mutable reconnectCts: CancellationTokenSource option = None
    let timers = System.Collections.Generic.Dictionary<int, CancellationTokenSource>()
    let mutable ticket = 0

    member _.Attempt = connectAttempt
    member _.DelayMs = reconnectDelayMs
    member _.Pending = reconnectCts.IsSome

    member _.Connect() =
        connectAttempt <- connectAttempt + 1
        let attempt = connectAttempt
        match reconnectCts with
        | Some cts -> cts.Cancel()
        | None -> ()
        reconnectCts <- None
        attempt

    member _.IsCurrent(attempt: int) = attempt = connectAttempt
    member _.ResetDelay() = reconnectDelayMs <- baseDelayMs

    member _.Schedule(hasToken: bool) =
        let lastToken = if hasToken then Some() else None
        match reconnectCts, lastToken with
        | Some _, _ -> None
        | None, None -> None
        | None, Some _ ->
            let cts = new CancellationTokenSource()
            reconnectCts <- Some cts
            let delay = reconnectDelayMs
            reconnectDelayMs <- next reconnectDelayMs
            ticket <- ticket + 1
            timers[ticket] <- cts
            Some(ticket, delay)

    member _.Fire(t: int) =
        match timers.TryGetValue t with
        | true, cts when not cts.IsCancellationRequested ->
            if not cts.IsCancellationRequested then
                reconnectCts <- None
                timers.Remove t |> ignore
                true
            else false
        | _ -> false

    member _.ResumeNow() =
        match reconnectCts with
        | Some cts ->
            cts.Cancel()
            reconnectCts <- None
            reconnectDelayMs <- baseDelayMs
            true
        | None -> false
