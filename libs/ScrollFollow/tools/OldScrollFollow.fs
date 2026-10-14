namespace Wanxiang.UI

// 照抄旧 Views/ChatView.fs：RenderMessages 的 wasAtBottom / streamingJustEnded / 未读段、ScrollChanged 与
// PointerWheelChanged 的记账、SmoothScrollToEnd、SmoothScrollToBeginning、ScrollToMessage、JumpExchange 落点、
// ResetScrollState、UpdateScrollToBottomAppearance（控件写入换成返回值）
[<RequireQualifiedAccess>]
type RenderFollow = | ScrollToEnd | Keep
type ScrollButtonView = { visible: bool; text: string; showLabel: bool }

type ScrollFollow() =
    let mutable atBottom = true
    let mutable returningToLatest = false
    let mutable unreadSinceScrolledUp = 0
    let mutable previousRenderedMessageCount = 0
    let mutable wasStreaming = false
    let mutable revealPinned = false

    member _.AtBottom = atBottom
    member _.Returning = returningToLatest
    member _.Unread = unreadSinceScrolledUp
    member _.RevealPinned = revealPinned

    member _.Rendered(streamingIsSome: bool, newCountTotal: int, pendingHistoryAnchorIsSome: bool) =
        let wasAtBottom = atBottom
        let streamingJustEnded = wasStreaming && not streamingIsSome
        wasStreaming <- streamingIsSome
        let mutable r = RenderFollow.Keep
        if wasAtBottom then r <- RenderFollow.ScrollToEnd
        else
            let newCount = newCountTotal
            let delta = newCount - previousRenderedMessageCount
            let countDelta = if streamingIsSome then 0 else delta
            if not pendingHistoryAnchorIsSome then
                if countDelta > 0 && previousRenderedMessageCount > 0 then
                    unreadSinceScrolledUp <- unreadSinceScrolledUp + countDelta
                elif unreadSinceScrolledUp = 0 && not wasAtBottom && countDelta > 0 then
                    unreadSinceScrolledUp <- max 1 countDelta
            if streamingJustEnded && not wasAtBottom && not pendingHistoryAnchorIsSome then
                unreadSinceScrolledUp <- max 1 (unreadSinceScrolledUp + 1)
        previousRenderedMessageCount <- newCountTotal
        r

    member _.Scrolled(userUp: bool, nowAtBottom: bool) =
        let mutable stop = false
        if userUp then
            returningToLatest <- false
            stop <- true
        if nowAtBottom then returningToLatest <- false
        atBottom <- nowAtBottom || returningToLatest
        if nowAtBottom then
            unreadSinceScrolledUp <- 0
        stop

    member _.Wheel() =
        revealPinned <- false
        returningToLatest <- false

    member _.ReturnToLatest() =
        revealPinned <- false
        returningToLatest <- true
        atBottom <- true
        unreadSinceScrolledUp <- 0

    member _.Arrived() =
        atBottom <- true
        unreadSinceScrolledUp <- 0

    member _.LeaveForBeginning() =
        returningToLatest <- false
        atBottom <- false

    member _.Pin() =
        revealPinned <- true
        atBottom <- false

    member _.JumpedTo(isLast: bool) =
        atBottom <- isLast
        unreadSinceScrolledUp <- 0

    member _.Reset() =
        revealPinned <- false
        atBottom <- true
        returningToLatest <- false
        unreadSinceScrolledUp <- 0
        previousRenderedMessageCount <- 0

    member _.Button(overflows: bool, compactMode: bool) =
        if atBottom then
            unreadSinceScrolledUp <- 0
            { visible = false; text = "回到最新"; showLabel = false }
        else
            let visible = overflows
            if unreadSinceScrolledUp > 0 then
                let text = sprintf "%d 条新消息" unreadSinceScrolledUp
                { visible = visible; text = text; showLabel = not compactMode }
            else
                { visible = visible; text = "回到最新"; showLabel = false }
