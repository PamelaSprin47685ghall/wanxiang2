namespace Wanxiang.UI

open System.Collections.Generic

/// 旧 Rich/Highlight.fs（22-75）后台着色的排队记账：哪些代码正在后台算、合并通知是否已排上。
/// 所有状态只在同一把锁里读写，可从任意线程调用。Task.Run、通知窗口的延时、缓存与 Ready 事件仍归宿主。
type HighlightQueue<'K when 'K: equality>() =
    let inFlight = HashSet<'K>()
    let mutable notifyScheduled = false
    let gate = obj ()

    /// 合并通知窗口（毫秒）
    static member NotifyWindowMs = 120

    member _.InFlightCount = lock gate (fun () -> inFlight.Count)

    /// 认领一段代码去后台算；已有人在算就返回 false（不重复排队）
    member _.TryClaim(key: 'K) : bool = lock gate (fun () -> inFlight.Add key)

    /// 后台算完（ok = 算成功并已写入缓存）。返回 true 时由这次完成负责安排合并通知：
    /// 宿主延时 NotifyWindowMs 后调 NotifyFired 再触发 Ready；返回 false 时搭车
    member _.Finished(key: 'K, ok: bool) : bool =
        lock gate (fun () -> inFlight.Remove key |> ignore)
        ok
        && lock gate (fun () ->
            if notifyScheduled then false
            else
                notifyScheduled <- true
                true)

    /// 通知窗口到点：放开下一次合并
    member _.NotifyFired() = lock gate (fun () -> notifyScheduled <- false)
