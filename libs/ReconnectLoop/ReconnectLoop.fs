namespace Wanxiang.Client

/// 断线重连的调度状态：连接尝试编号（丢弃过期回调）、至多一个挂起的重连、退避时长。
/// 旧 AppShell.fs 的 connectAttempt、reconnectCts、reconnectDelayMs 与 Connect / ScheduleReconnect / ResumeReconnectNow 的记账。
/// 计时（Async.Sleep）、真正的连接、UI 文案仍归调用方。退避规则由参数给（通常是 ReconnectBackoff.baseDelayMs / next）。
type ReconnectLoop(baseDelayMs: int, next: int -> int) =
    let mutable attempt = 0
    let mutable delay = baseDelayMs
    let mutable pending: int option = None
    let mutable ticket = 0

    member _.Attempt = attempt
    member _.DelayMs = delay
    member _.Pending = pending.IsSome

    /// 发起一次连接：编号加一，挂起的重连作废；返回本次编号
    member _.Connect() =
        attempt <- attempt + 1
        pending <- None
        attempt

    /// 异步回调回来时：只有最新一次连接的回调算数
    member _.IsCurrent(a: int) = a = attempt

    /// 用户手动发起连接或连接成功：退避归零
    member _.ResetDelay() = delay <- baseDelayMs

    /// 失败后排一次重连。已有挂起或没有令牌时不排；返回 (票据, 等待毫秒)，等待时长随后翻倍
    member _.Schedule(hasToken: bool) =
        match pending with
        | Some _ -> None
        | None when not hasToken -> None
        | None ->
            ticket <- ticket + 1
            pending <- Some ticket
            let d = delay
            delay <- next delay
            Some(ticket, d)

    /// 计时到点：票据仍是挂起的那张才执行（返回 true 后调用方 Connect）
    member _.Fire(t: int) =
        if pending = Some t then
            pending <- None
            true
        else false

    /// 网络恢复或回到前台：有挂起的就立刻执行并把退避归零（返回 true 后调用方 Connect）；没有就什么也不做
    member _.ResumeNow() =
        match pending with
        | Some _ ->
            pending <- None
            delay <- baseDelayMs
            true
        | None -> false
