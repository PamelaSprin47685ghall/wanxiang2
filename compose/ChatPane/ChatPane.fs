namespace Wanxiang2.ChatPane

open System
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Layout
open Wanxiang.UI
open Wanxiang2.ChatScroller
open Wanxiang2.ExchangeNav
open Wanxiang2.ChatEmpty
open Wanxiang2.SkeletonList

/// 组合者：对话区的滚动、空态、历史骨架与问答跳转（旧 Views/ChatView.fs 的对应部分）。
/// 贴底与未读走 ScrollFollow，平滑滚动曲线走 ScrollTween，骨架呼吸走 BreathPhase，空态文案走 ChatEmptyText，问答跳转走 ExchangeJump。
/// 消息卡片本身由宿主放进 Messages 插槽（卡片组合者 MessageCardView）；'K 是消息键。
type ChatPane<'K when 'K: equality>(looks: ChatPaneLooks) =
    let follow = ScrollFollow()
    let tween = ScrollTween()
    let phase = BreathPhase()
    let jump = ExchangeJump<'K>()
    let ease =
        { Start = fun now offset target reduced -> tween.Start(now, offset, target, reduced)
          Frame =
            fun now target ->
                match tween.Frame(now, target) with
                | ScrollFrame.Offset y -> EaseStep.Moved y
                | ScrollFrame.Done y -> EaseStep.Arrived y
          Cancel = tween.Cancel
          Pin = tween.Pin
          IsOwn = tween.IsOwnOffset }
    let scroller = ChatScroller(looks.Scroller, ease)
    let nav = ExchangeNav(looks.Nav)
    let empty = ChatEmpty(looks.Empty)
    let skeleton =
        SkeletonList(looks.Skeleton, { Begin = (fun () -> phase.Start(false, true) |> ignore); Step = phase.Step })
    let messages = StackPanel(Orientation = Orientation.Vertical)
    let column = StackPanel(Orientation = Orientation.Vertical)
    let emptyScroller =
        ScrollViewer(
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = empty.Host,
            IsVisible = false)
    let body = Grid()
    let mutable compact = false
    let mutable overflows = false
    let returnedFocus = Event<unit>()

    let refreshButton () =
        let v = follow.Button(overflows, compact)
        scroller.ShowLatest { Visible = v.visible; Text = v.text; Wide = v.showLabel }

    let toEnd () =
        let target () = max 0.0 (scroller.Extent - scroller.Viewport)
        follow.ReturnToLatest()
        if not (scroller.SmoothTo(target, refreshButton, fun _ -> follow.Arrived(); refreshButton ())) then
            scroller.ScrollToEndNow()
            follow.Arrived()
        refreshButton ()

    do
        column.Children.Add skeleton.Host
        column.Children.Add messages
        scroller.Content <- column
        body.Children.Add scroller.Host
        body.Children.Add emptyScroller
        nav.FailureBanner.VerticalAlignment <- VerticalAlignment.Top
        body.Children.Add nav.FailureBanner
        scroller.Scrolled.Add(fun note ->
            overflows <- note.Overflows
            if follow.Scrolled(note.UserUp, note.AtBottom) then scroller.StopSmooth()
            refreshButton ())
        scroller.Wheeled.Add(fun _ ->
            follow.Wheel()
            scroller.StopSmooth())
        scroller.LatestPressed.Add(fun focusWasOnButton ->
            toEnd ()
            if focusWasOnButton then returnedFocus.Trigger())

    member _.Host: Control = body
    member _.Scroller = scroller
    member _.Nav = nav
    member _.Empty = empty
    member _.Skeleton = skeleton
    member _.Follow = follow
    member _.Jumps = jump
    /// 消息卡片的插槽
    member _.Messages: StackPanel = messages
    /// 按「回到最新」时焦点在按钮上：宿主把焦点交回输入区
    member _.FocusReturned = returnedFocus.Publish

    member _.SetCompact(value: bool) =
        compact <- value
        scroller.SetCompact value
        refreshButton ()

    /// 一次渲染之后（newCount 含窗口外未挂载的）
    member _.Rendered(streaming: bool, newCount: int, prependInFlight: bool) =
        match follow.Rendered(streaming, newCount, prependInFlight) with
        | RenderFollow.ScrollToEnd -> scroller.ScrollToEndDeferred(fun () -> follow.RevealPinned)
        | RenderFollow.Keep -> ()
        refreshButton ()

    member _.ScrollToLatest() = toEnd ()

    /// 定位某条消息后钉住视口
    member _.PinReveal() = follow.Pin()

    /// 空态：文案取 ChatEmptyText；需要品牌标的状态用 logo
    member _.ShowEmpty(state: ChatEmptyState, logo: Control, primary: (string * (unit -> unit)) option) =
        let title, hint = ChatEmptyText.text state
        if ChatEmptyState.showsBrand state then empty.ShowBrand(logo, title, hint, primary)
        else empty.ShowPrompt(title, hint, primary)
        emptyScroller.IsVisible <- true
        scroller.Host.IsVisible <- false
        skeleton.Hide()
        if not follow.RevealPinned then
            follow.Reset()
            jump.Reset()
            nav.Reset()
        refreshButton ()

    /// 嵌入式空态（如首次引导卡）
    member _.ShowEmbedded(content: Control) =
        empty.ShowEmbedded content
        emptyScroller.IsVisible <- true
        scroller.Host.IsVisible <- false

    member _.HideEmpty() =
        empty.Hide()
        emptyScroller.IsVisible <- false
        scroller.Host.IsVisible <- true

    member _.IsEmptyShown = emptyScroller.IsVisible

    member _.ShowSkeleton() = skeleton.Show()
    member _.HideSkeleton() = skeleton.Hide()

    /// 换对话
    member _.Reset() =
        follow.Reset()
        jump.Reset()
        nav.Reset()
        scroller.StopSmooth()
        refreshButton ()

    /// 刷新上一轮 / 下一轮端点
    member _.RefreshJumps(mounted: 'K option[], cards: int list, viewportIndex: int, hasHidden: bool) =
        let prev, next = jump.NavState(mounted, cards, viewportIndex, follow.AtBottom, hasHidden)
        nav.SetJumpEnds(prev, next)

    /// 问答跳转；返回 ScrollTo 时宿主把那张卡滚进视口
    member _.Jump(direction: int, mounted: 'K option[], cards: int list, viewportIndex: int, hasHidden: bool, lastFirstCommit: uint64 option) =
        let step = jump.Jump(direction, mounted, cards, viewportIndex, hasHidden, lastFirstCommit)
        match step with
        | JumpStep.ScrollTo(_, atBottom) ->
            scroller.StopSmooth()
            follow.JumpedTo atBottom
            refreshButton ()
        | _ -> ()
        step

    member _.SetOlderAvailable(value: bool) = jump.SetOlderAvailable value

    /// 更早一页取失败：横幅带重试
    member _.ShowOlderFailure(text: string, onRetry: unit -> unit) =
        jump.OlderFailed()
        scroller.CancelPrependAnchor()
        nav.ShowOlderFailure(text, onRetry)

    member _.HideOlderFailure() = nav.HideOlderFailure()
