namespace Wanxiang.UI

/// 渲染后视口怎么办（限定访问）
[<RequireQualifiedAccess>]
type RenderFollow =
    /// 渲染前贴底：滚到末尾
    | ScrollToEnd
    /// 不动；按钮外观要刷新
    | Keep

/// 「回到最新」按钮的呈现
type ScrollButtonView =
    { visible: bool
      /// 按钮文字、提示、读屏名
      text: string
      /// 宽按钮带文字（有未读且不是窄屏）
      showLabel: bool }

/// 旧 Views/ChatView.fs 的贴底跟随记账：atBottom、returningToLatest、unreadSinceScrolledUp、
/// previousRenderedMessageCount、wasStreaming、revealPinned。滚动动画、计时器、偏移与尺寸仍归 ChatView
type ScrollFollow() =
    let mutable atBottom = true
    let mutable returning = false
    let mutable unread = 0
    let mutable previousCount = 0
    let mutable wasStreaming = false
    let mutable pinned = false

    member _.AtBottom = atBottom
    member _.Returning = returning
    member _.Unread = unread
    /// 搜索命中、版本切换定位后钉住视口，直到用户滚动、回到最新、换对话
    member _.RevealPinned = pinned

    /// 一次渲染。newCount 含窗口外未挂载的；streaming 为本帧挂着流式卡；prependInFlight 为分页前置在途。
    /// 不贴底时累计未读：分页加载的旧消息与流式刷新不算，流式落定记一次
    member _.Rendered(streaming: bool, newCount: int, prependInFlight: bool) =
        let wasAtBottom = atBottom
        let justEnded = wasStreaming && not streaming
        wasStreaming <- streaming
        let follow =
            if wasAtBottom then RenderFollow.ScrollToEnd
            else
                let delta = if streaming then 0 else newCount - previousCount
                if not prependInFlight then
                    if delta > 0 && previousCount > 0 then unread <- unread + delta
                    elif unread = 0 && delta > 0 then unread <- max 1 delta
                if justEnded && not prependInFlight then unread <- max 1 (unread + 1)
                RenderFollow.Keep
        previousCount <- newCount
        follow

    /// 滚动事件。userUp：向上且不是动画自己写的偏移。返回 true 表示要停掉平滑滚动
    member _.Scrolled(userUp: bool, nowAtBottom: bool) =
        if userUp then returning <- false
        if nowAtBottom then returning <- false
        atBottom <- nowAtBottom || returning
        if nowAtBottom then unread <- 0
        userUp

    /// 滚轮（任一方向）：解除钉住与回到最新的意图；调用方停掉平滑滚动
    member _.Wheel() =
        pinned <- false
        returning <- false

    /// 点「回到最新」或平滑滚到底开始
    member _.ReturnToLatest() =
        pinned <- false
        returning <- true
        atBottom <- true
        unread <- 0

    /// 已到底（动画结束或距离很近）
    member _.Arrived() =
        atBottom <- true
        unread <- 0

    /// 平滑滚到开头开始：一离开底部就不再贴底
    member _.LeaveForBeginning() =
        returning <- false
        atBottom <- false

    /// 定位到某条消息
    member _.Pin() =
        pinned <- true
        atBottom <- false

    /// 问答跳转落到某张卡
    member _.JumpedTo(isLast: bool) =
        atBottom <- isLast
        unread <- 0

    /// 换对话（钉住时 HideEmpty 不调）
    member _.Reset() =
        pinned <- false
        atBottom <- true
        returning <- false
        unread <- 0
        previousCount <- 0

    /// 按钮外观。overflows：内容比视口高出阈值（ContentMetrics.scrollBottomRevealThreshold）。贴底时顺带清零未读
    member _.Button(overflows: bool, compact: bool) =
        if atBottom then
            unread <- 0
            { visible = false; text = "回到最新"; showLabel = false }
        elif unread > 0 then
            { visible = overflows; text = sprintf "%d 条新消息" unread; showLabel = not compact }
        else
            { visible = overflows; text = "回到最新"; showLabel = false }
