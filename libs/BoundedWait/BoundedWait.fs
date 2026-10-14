namespace Wanxiang.UI

open System

/// 请求→回应的有界等待（迁自旧 Views/SettingsDevices.fs 的 BeginLoad/NotifyLoadTimeout，
/// 与旧 AppShell 「检查主机」的 30 秒超时）：旧主机忽略未知事件时不会回复，界面不能一直显示「正在读取」。
/// 计时器归宿主：宿主按 Due 计时，到点调用 TimeOut；回复到达调用 Settle。
type BoundedWait(limit: TimeSpan) =
    let mutable waiting = false
    let mutable due: DateTimeOffset option = None

    member _.Limit = limit

    /// 发出请求：开始（或重新开始）等待。
    member _.Begin(now: DateTimeOffset) =
        waiting <- true
        due <- Some(now + limit)

    member _.IsWaiting = waiting

    /// 到点时间；不在等待时为 None。
    member _.Due = due

    /// 回复到达：结束等待。返回此前是否在等待。
    member _.Settle() : bool =
        let was = waiting
        waiting <- false
        due <- None
        was

    /// 计时到点（也可由测试直接调用）：仍在等待则结束等待并返回 true，宿主据此给出超时说明；
    /// 回复已到时返回 false，什么也不做。
    member this.TimeOut() : bool = this.Settle()

    /// 按时间判断：到 now 已过期才结束等待并返回 true。
    member this.Tick(now: DateTimeOffset) : bool =
        match due with
        | Some t when waiting && now >= t -> this.TimeOut()
        | _ -> false

/// 两处有界等待的上限与超时说明（旧代码原文）。
module BoundedWaits =
    /// 设备列表：8 秒。
    let devicesLimit = TimeSpan.FromSeconds 8.0
    let devicesTimeoutText = "主机没有返回设备列表。如果主机版本较旧，可能还不支持设备管理，请先升级主机。"
    /// 检查主机：30 秒。
    let doctorLimit = TimeSpan.FromSeconds 30.0
    let doctorTimeoutText = "没有拿到检查结果：主机可能版本较旧，或检查超时了。请稍后重试，仍不行就升级主机。"
    /// 未连接时请求检查主机。
    let doctorOfflineText = "还没连接主机，无法检查。连接后再试。"
