namespace Wanxiang.UI

open System

/// 延迟显示（迁自旧 AppShell.ShowConversationLoadingDeferred / DisarmSkeletonLoading）：
/// 对话快照到达前先不挂任何 chrome，过了延迟仍没到才显示骨架。
/// 同一个键只武装一次：后台事件反复触发渲染时，不能把显示无限顺延（D1）。
/// 计时器归宿主：宿主在 Arm 返回 true 时按 Due 启动计时，到点调用 Expired。
type DeferredReveal<'key when 'key: equality>(delay: TimeSpan) =
    let mutable armedFor: 'key option = None
    let mutable due: DateTimeOffset option = None

    /// 旧 MotionLedger.conversationSkeletonDelay。
    static member SkeletonDelay = TimeSpan.FromMilliseconds 300.0

    member _.Delay = delay

    /// 为这个键武装。已为同一个键武装时什么也不做，返回 false（不重启计时）；
    /// 换了键或刚解除过，返回 true，宿主应停掉旧计时并按 Due 重新计时。
    member _.Arm(key: 'key, now: DateTimeOffset) : bool =
        if armedFor = Some key then
            false
        else
            armedFor <- Some key
            due <- Some(now + delay)
            true

    /// 到点时间；未武装为 None。
    member _.Due = due

    member _.ArmedFor = armedFor

    /// 计时到点：这次计时是为 key 启动的，且之后没有解除或换键，才返回 true（宿主再看快照是否仍未到）。
    /// 与旧实现一样，到点不解除武装：同一个键不会被再次武装。
    member _.Expired(key: 'key) : bool =
        match armedFor, due with
        | Some armed, Some _ when armed = key ->
            due <- None
            true
        | _ -> false

    /// 快照到达或切换对话：解除武装。
    member _.Disarm() =
        armedFor <- None
        due <- None
