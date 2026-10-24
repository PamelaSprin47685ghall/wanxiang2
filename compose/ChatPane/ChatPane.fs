namespace Wanxiang2.ChatPane

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Controls.Templates
open Avalonia.Layout
open Wanxiang.UI
open Wanxiang2.ChatScroller
open Wanxiang2.ExchangeNav
open Wanxiang2.ChatEmpty
open Wanxiang2.SkeletonList
open Wanxiang2.WorkspaceState

/// 组合者：对话区的滚动、空态、历史骨架与问答跳转（旧 Views/ChatView.fs 的对应部分）。
/// 消息列表使用原生 ItemsControl + VirtualizingStackPanel 实现真虚拟化；
/// 废除 Look 注入（W7 外观退役）：主题浮点值在用量时刻从应用资源现读（改值即生效，无订阅），
/// 空态与骨架收敛到 Render(ChatVS)。
/// onStarter：起手式芯片点选回调（None 则不画起手式行）；起手式表取自 ChatStarters。
type ChatPane<'K when 'K: equality>(brandLogo: float -> Control, onStarter: (string -> unit) option) =

    let follow = ScrollFollow()
    let mutable lastAnimatedOffset = Double.NaN
    let jump = ExchangeJump<'K>()

    let ease =
        {
            Start =
                fun _now offset target reduced ->
                    if reduced || abs (target - offset) < 5.0 then
                        false
                    else
                        true
            Frame = fun _now target -> EaseStep.Arrived target
            Cancel = fun () -> ()
            Pin = fun y -> lastAnimatedOffset <- y
            IsOwn = fun y -> not (Double.IsNaN lastAnimatedOffset) && abs (y - lastAnimatedOffset) < 1.0
        }

    let scroller = ChatScroller(ease)
    let nav = ExchangeNav()
    let empty = ChatEmpty(ChatStarters.items, onStarter, brandLogo = brandLogo)
    let skeleton = SkeletonList()
    // 原生 ItemsControl + VirtualizingStackPanel 替代普通 StackPanel 消息容器
    let itemsControl = ItemsControl()

    do
        itemsControl.ItemsPanel <- FuncTemplate<Panel>(fun () -> VirtualizingStackPanel() :> Panel)
        itemsControl.HorizontalAlignment <- HorizontalAlignment.Stretch
        itemsControl.MaxWidth <- 748.0
        itemsControl.Margin <- Thickness(0.0, 0.0, 0.0, 48.0)

    let messages =
        StackPanel(
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 748.0,
            Spacing = 24.0
        )

    let emptyScroller =
        ScrollViewer(
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = empty.Host,
            IsVisible = false
        )

    let body = Grid()
    let mutable compact = false
    let mutable overflows = false
    let returnedFocus = Event<unit>()
    let mutable startMode: bool option = None

    /// 主题资源字典的浮点值现读：未安装主题时用旧初值兜底。
    let resFloat (key: string) (fallback: float) =
        match Application.Current with
        | null -> fallback
        | app ->
            match app.TryFindResource(box key) with
            | true, v -> unbox v
            | _ -> fallback

    let applyStart () =
        match startMode with
        | Some v -> empty.ApplyBottomAligned v
        | None -> ()

    let hideSkeleton () =
        if skeleton.IsShown then
            skeleton.Hide()
            scroller.Content <- messages
            messages.IsVisible <- true

    let refreshButton () =
        let v = follow.Button(overflows, compact)

        scroller.ShowLatest
            {
                Visible = v.visible
                Text = v.text
                Wide = v.showLabel
            }

    let toEnd () =
        let target () =
            max 0.0 (scroller.Extent - scroller.Viewport)

        follow.ReturnToLatest()

        if
            not (
                scroller.SmoothTo(
                    target,
                    refreshButton,
                    fun _ ->
                        follow.Arrived()
                        refreshButton ()
                )
            )
        then
            scroller.ScrollToEndNow()
            follow.Arrived()

        refreshButton ()

    /// 水平留白按自身宽度取档（旧 ChatView.ApplyHorizontalInsets 的滚动区部分）
    let mutable top = 0.0

    let applyInsets () =
        // 阅读列节奏对齐 react thread：顶 Space.X4l 24、底 Space.Md 8（与输入区顶 8 相接）
        top <- resFloat "Space.X4l" Wanxiang.UI.Spacing.space4xl
        let inset = LayoutPolicy.horizontalInset body.Bounds.Width
        let bottomPad = resFloat "Space.Md" Wanxiang.UI.Spacing.spaceMd
        scroller.Viewer.Padding <- Thickness(inset, top, inset, bottomPad)
        // 空态的水平留白归这里（react .starter 的 padding-inline），ChatEmpty 自身不再带侧边距
        emptyScroller.Padding <- Thickness(inset, 0.0, inset, 0.0)

    do
        // 旧主体的子项序：滚动区、空态滚动区、「回到最新」、更早一页失败横幅。ChatScroller 的叠放层拆开放进主体
        let layer = scroller.Host :?> Panel
        layer.Children.Clear()
        scroller.Content <- messages
        body.Children.Add scroller.Viewer

        body.Children.Add emptyScroller
        body.Children.Add scroller.LatestButton
        body.Children.Add nav.FailureBanner

        body.PropertyChanged.Add(fun e ->
            if e.Property = Visual.BoundsProperty then
                applyInsets ())
        // 主题资源改值会顶开一次重排：借 Bounds 变化之外的口子在可见性变化时重取留白
        empty.Host.PropertyChanged.Add(fun e ->
            if e.Property = Visual.IsVisibleProperty && empty.Host.IsVisible then
                applyStart ())

        empty.Host.PropertyChanged.Add(fun e ->
            if e.Property = Visual.IsVisibleProperty then
                emptyScroller.IsVisible <- empty.Host.IsVisible)

        scroller.Scrolled.Add(fun note ->
            overflows <- note.Overflows

            if follow.Scrolled(note.UserUp, note.AtBottom) then
                scroller.StopSmooth()

            refreshButton ())

        scroller.Wheeled.Add(fun _ ->
            follow.Wheel()
            scroller.StopSmooth())

        scroller.LatestPressed.Add(fun focusWasOnButton ->
            toEnd ()

            if focusWasOnButton then
                returnedFocus.Trigger())

    member _.Host: Control = body
    member _.Scroller = scroller
    member _.Nav = nav
    member _.Empty = empty
    member _.Skeleton = skeleton
    member _.Follow = follow
    member _.Jumps = jump
    /// 消息卡片插槽（兼容旧插槽面板；新虚拟化可通过 ItemsControl 驱动）
    member _.Messages: StackPanel = messages
    /// 原生虚拟化列表控件
    member _.ItemsControl: ItemsControl = itemsControl
    /// 按「回到最新」时焦点在按钮上：宿主把焦点交回输入区
    member _.FocusReturned = returnedFocus.Publish

    /// 宿主改了跟随状态之后刷新「回到最新」按钮
    member _.RefreshLatestButton() = refreshButton ()

    /// 立即写偏移并记为自己写的（旧 tween.Pin）：钉住期间重新对齐不被当成用户上滚
    member _.PinOffset(y: float) = scroller.PinOffset y

    /// 起步模式（插槽/非每帧投影）：空态贴近输入区；只改布局
    member _.WireStartMode(value: bool) =
        startMode <- Some value
        applyStart ()

    member _.ApplyCompact(value: bool) =
        compact <- value
        scroller.ApplyCompact value
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
    member _.ShowEmpty(state: ChatEmptyState, primary: (string * (unit -> unit)) option) =
        let title, hint = ChatEmptyText.text state

        if ChatEmptyState.showsBrand state then
            let logo = brandLogo (resFloat "Metric.LogoEmpty" 68.0)
            logo.HorizontalAlignment <- HorizontalAlignment.Center
            empty.ShowBrand(logo, title, hint, primary)
        else
            empty.ShowPrompt(title, hint, primary)

        scroller.Viewer.IsVisible <- false
        hideSkeleton ()
        itemsControl.IsVisible <- false
        messages.IsVisible <- false

        if not follow.RevealPinned then
            follow.Reset()
            jump.Reset()
            nav.Reset()

        refreshButton ()

    /// 嵌入式空态（如首次引导卡）
    member _.ShowEmbedded(content: Control) =
        empty.ShowEmbedded content
        scroller.Viewer.IsVisible <- false
        hideSkeleton ()
        itemsControl.IsVisible <- false
        messages.IsVisible <- false

    member this.HideEmpty() =
        empty.Hide()
        // 定位钉住时不复位（旧 HideEmpty）；换对话由宿主显式 Reset
        if not follow.RevealPinned then
            this.Reset()

        scroller.Viewer.IsVisible <- true
        itemsControl.IsVisible <- true
        messages.IsVisible <- true

    member _.IsEmptyShown = emptyScroller.IsVisible

    /// 历史骨架：滚动区内容换成骨架（旧 ShowSkeletonLoading）
    member _.ShowSkeleton() =
        itemsControl.IsVisible <- false
        skeleton.Show()
        scroller.Content <- skeleton.Host

    member _.HideSkeleton() = hideSkeleton ()
    member _.IsSkeletonShown = skeleton.IsShown

    /// 换对话
    member _.Reset() =
        follow.Reset()
        jump.Reset()
        nav.Reset()
        nav.HideOlderFailure()
        scroller.StopSmooth()
        refreshButton ()

    /// 刷新上一轮 / 下一轮端点
    member _.RefreshJumps(mounted: 'K option[], cards: int list, viewportIndex: int, hasHidden: bool) =
        let prev, next =
            jump.NavState(mounted, cards, viewportIndex, follow.AtBottom, hasHidden)

        nav.ApplyJumpEnds(prev, next)

    /// 问答跳转；返回 ScrollTo 时宿主把那张卡滚进视口
    member _.Jump
        (
            direction: int,
            mounted: 'K option[],
            cards: int list,
            viewportIndex: int,
            hasHidden: bool,
            lastFirstCommit: uint64 option
        ) =
        let step =
            jump.Jump(direction, mounted, cards, viewportIndex, hasHidden, lastFirstCommit)

        match step with
        | JumpStep.ScrollTo(_, atBottom) ->
            scroller.StopSmooth()
            follow.JumpedTo atBottom
            refreshButton ()
        | _ -> ()

        step

    member _.ApplyOlderAvailable(value: bool) = jump.ApplyOlderAvailable value

    /// 更早一页取失败：横幅带重试
    member _.ShowOlderFailure(text: string, onRetry: unit -> unit) =
        jump.OlderFailed()
        nav.ShowOlderFailure(text, onRetry)

    member _.HideOlderFailure() = nav.HideOlderFailure()

    /// 单入口：根据 ChatVS 幂等投影呈现
    member this.Render(vs: ChatVS, ?onPrimary: string * (unit -> unit)) =
        match vs.Phase with
        | ChatPhase.Skeleton ->
            this.ShowSkeleton()
            scroller.Viewer.IsVisible <- true
            emptyScroller.IsVisible <- false
        | ChatPhase.Empty _ ->
            empty.Render(vs.Phase, ?onPrimary = onPrimary)
            hideSkeleton ()
            scroller.Viewer.IsVisible <- false
            emptyScroller.IsVisible <- true
            itemsControl.IsVisible <- false
        | ChatPhase.Messages msgs ->
            this.HideEmpty()
            hideSkeleton ()
            itemsControl.ItemsSource <- msgs
            scroller.Viewer.IsVisible <- true
            emptyScroller.IsVisible <- false
            itemsControl.IsVisible <- true
