namespace Wanxiang2.ChatScreen

open System
open System.Collections.Generic
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Avalonia.LogicalTree
open Wanxiang.Core
open Wanxiang.UI
open Wanxiang2.IconSet
open Wanxiang2.PopupMenu
open Wanxiang2.OverlayDesk
open Wanxiang2.ChatHeader
open Wanxiang2.ChatPane
open Wanxiang2.MessageCardView
open Wanxiang2.MarkdownView
open Wanxiang2.ComposerDesk
open Wanxiang2.ApprovalDesk

/// 一张已挂载卡片的身份（旧 ChatView.CardKey）：渲染相关的旗标都进身份，变了就重建那张卡
type ScreenKey =
    { commit: uint64
      role: string
      toolCalls: int
      toolResults: int
      attachments: int
      fontSize: float
      collapse: bool
      lastAssistant: bool
      usageTag: int
      missingTag: int
      siblingTag: int
      awaitingTools: bool
      approvalTag: int }

/// 一帧要画的全部内容（旧 RenderMessages 的参数）
type ScreenFrame =
    { Messages: MessageView list
      Streaming: MessageView option
      /// 生成错误与重试
      Error: (GenerationError * (unit -> unit)) option
      FontSize: float
      AutoCollapseReasoning: bool
      Usage: GenerationUsage option
      MissingAttachments: Set<string> }

/// 宿主给对话屏的动作
type ChatScreenActions =
    { Card: CardActions
      RenameTitle: string -> unit
      ToggleSidebar: unit -> unit
      OpenSessionSettings: unit -> unit
      ForkFromHere: unit -> unit
      ExportConversation: unit -> unit
      StopGeneration: unit -> unit
      /// 向主机要更早一页
      RequestOlderHistory: unit -> unit
      /// 焦点归宿（输入区）
      FocusHome: unit -> unit
      OpenParent: Guid -> unit
      /// 版本条选了另一个版本（对话 id）
      SiblingSelected: Guid -> unit
      /// 延时回调（计时器归宿主）：定位高亮停留用
      After: TimeSpan -> (unit -> unit) -> IDisposable }

/// 可点的图标按钮（版本条用）：自动化层报按钮并给 Invoke
type private Tap() as this =
    inherit Border()
    let mutable action: unit -> unit = ignore
    do
        this.PointerReleased.Add(fun e ->
            if not e.Handled && this.IsEnabled && e.InitialPressMouseButton = MouseButton.Left then
                e.Handled <- true
                action ())
        this.KeyDown.Add(fun e ->
            if not e.Handled && this.IsEnabled && (e.Key = Key.Enter || e.Key = Key.Space) then
                e.Handled <- true
                action ())
    override _.StyleKeyOverride = typeof<Border>
    member _.OnInvoke(f: unit -> unit) = action <- f
    member _.Invoke() = if this.IsEnabled then action ()
    override _.OnCreateAutomationPeer() = TapPeer(this) :> AutomationPeer

and private TapPeer(owner: Tap) =
    inherit ControlAutomationPeer(owner)
    override _.GetAutomationControlTypeCore() = AutomationControlType.Button
    interface IInvokeProvider with
        member _.Invoke() = Dispatcher.UIThread.Post(fun () -> owner.Invoke())

/// 根面：与旧 ChatView 一样是 Border 派生类型
type ScreenRoot() =
    inherit Border()

/// 组合者：对话屏（旧 Views/ChatView.fs 的整体 + AppShell 里对话列的接线）。
/// 顶栏走 ChatHeader，滚动 / 空态 / 骨架 / 跳转走 ChatPane，卡片走 MessageCards + CardDeck，
/// 挂载窗口走 MountWindow，定位高亮记账走 RevealFlash，面包屑走 BreadcrumbAction；
/// 审批面板放进输入区附属位。本组合者拥有渲染次序：挂载窗口规划 → 卡片对账 → 跳转锚点续跳 → 贴底跟随。
type ChatScreen
    (looks: ChatScreenLooks,
     cards: MessageCards,
     composer: ComposerDesk,
     approvals: ApprovalDesk,
     overlay: OverlayDesk,
     brandLogo: float -> Control,
     actions: ChatScreenActions) =
    let header = ChatHeader(looks.Header)
    let pane = ChatPane<ScreenKey>(looks.Pane, brandLogo)
    let deck = CardDeck<ScreenKey>(pane.Messages)
    let mountWindow = MountWindow(MountWindow.Initial, MountWindow.Step)
    let flash = RevealFlash()
    let breadcrumb = BreadcrumbAction<Guid>()
    let root = ScreenRoot(Background = looks.Canvas)
    let layout = DockPanel()
    let mutable mounted: ScreenKey option[] = [||]
    let mutable last: ScreenFrame option = None
    let mutable streamSlot: (obj * ScreenKey * MarkdownStream) option = None
    let mutable streamGeneration = 0
    let mutable awaiting: Set<string> = Set.empty
    let mutable partial: (MessageView * (unit -> unit)) option = None
    let mutable generating = false
    let mutable canStop = false
    let mutable editable = false
    let mutable savePreset: (unit -> unit) option = None
    let mutable siblings: uint64 -> uint64 option -> SiblingVersions option = fun _ _ -> None
    let mutable revealTarget: Control option = None
    let mutable highlight: (IDisposable * (unit -> unit)) option = None
    let mutable top3xl = 0.0
    let mutable avatar = 0.0
    let mutable xs = 0.0
    let mutable caption = 0.0
    let mutable iconButton = 0.0

    let exchangeIndices () = ExchangeJump<ScreenKey>.Cards(mounted, fun k -> k.role = "user")

    /// 当前所在的那一轮 = 第一张「底边越过视口顶端」的用户卡；开场流式时退回首张
    let currentExchange () =
        let viewportTop = pane.Scroller.Offset
        let children = pane.Messages.Children
        let cardsAt = exchangeIndices ()
        cardsAt
        |> List.filter (fun i -> i < children.Count && children.[i].Bounds.Y + children.[i].Bounds.Height > viewportTop + 0.5)
        |> List.tryHead
        |> Option.orElseWith (fun () -> List.tryHead cardsAt)
        |> Option.defaultValue 0

    let syncNav () = pane.RefreshJumps(mounted, exchangeIndices (), currentExchange (), mountWindow.HiddenCount > 0)

    /// 把一张卡平滑带到视口顶（每帧按卡片当前位置取目标；减弱动效时直接写并记为自己写的）
    let smoothToCard (child: Control) =
        let target () = max 0.0 (child.Bounds.Y - top3xl)
        if not (pane.Scroller.SmoothTo(target, ignore, fun y -> pane.PinOffset y)) then pane.PinOffset(target ())

    let withSiblingBar (card: Control) (s: SiblingVersions) : Control =
        let go delta =
            let t = s.index + delta
            if t >= 0 && t < s.count then actions.SiblingSelected(List.item t s.targets)
        let button glyph (tip: string) enabled delta =
            let b = Tap(Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand), Focusable = true,
                        Width = iconButton, Height = iconButton, MinWidth = iconButton, MinHeight = iconButton,
                        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center)
            looks.DressIcon b
            let g = looks.Icon glyph looks.TextMuted
            g.HorizontalAlignment <- HorizontalAlignment.Center
            g.VerticalAlignment <- VerticalAlignment.Center
            b.Child <- g
            ToolTip.SetTip(b, tip)
            AutomationProperties.SetName(b, tip)
            b.IsEnabled <- enabled
            b.OnInvoke(fun () -> go delta)
            b :> Control
        let name = sprintf "这条消息的版本 %d / %d" (s.index + 1) s.count
        let label =
            TextBlock(Text = sprintf "%d/%d" (s.index + 1) s.count, FontSize = caption, Foreground = looks.TextMuted,
                      VerticalAlignment = VerticalAlignment.Center)
        AutomationProperties.SetName(label, name)
        ToolTip.SetTip(label, "同一条消息编辑后产生的版本（每个版本是一个分叉）")
        let bar = StackPanel(Orientation = Orientation.Horizontal, Spacing = xs, HorizontalAlignment = HorizontalAlignment.Right)
        bar.Children.Add(button Glyph.ChevronLeft "上一个版本" (s.index > 0) -1)
        bar.Children.Add label
        bar.Children.Add(button Glyph.ChevronRight "下一个版本" (s.index < s.count - 1) 1)
        AutomationProperties.SetName(bar, name)
        let stack = StackPanel(Spacing = xs)
        stack.Children.Add card
        stack.Children.Add bar
        stack :> Control

    let stack (a: Control) (b: Control) =
        let s = StackPanel(Spacing = xs)
        s.Children.Add a
        s.Children.Add b
        s :> Control

    let rec render (f: ScreenFrame) =
        last <- Some f
        pane.HideSkeleton()
        let merged = MessageView.mergeToolResults f.Messages
        let lastAssistant =
            merged |> List.mapi (fun i m -> i, m) |> List.filter (fun (_, m) -> not (MessageView.isUser m)) |> List.tryLast |> Option.map fst
        let ctxFor index streaming : CardContext =
            { FontSize = f.FontSize
              AutoCollapseReasoning = f.AutoCollapseReasoning
              Streaming = streaming
              IsLastAssistant = (f.Streaming.IsNone && lastAssistant = Some index)
              Usage = f.Usage
              MissingAttachments = f.MissingAttachments
              AwaitingApproval = awaiting
              BrandAvatar = fun () -> brandLogo avatar }
        let desired = ResizeArray<ScreenKey option * (unit -> Control)>()
        // 版本：前一条按原始顺序取（工具结果折进调用消息后前一条身份会错位）
        let previous =
            let map = Dictionary<uint64, uint64 option>()
            let mutable prev = None
            for m in f.Messages do
                match m.commitId with
                | Some c ->
                    map[c] <- prev
                    prev <- Some c
                | None -> ()
            map
        let siblingsOf (m: MessageView) =
            match m.commitId with
            | Some c when MessageView.isUser m ->
                siblings c (match previous.TryGetValue c with | true, p -> p | _ -> None)
            | _ -> None
        // 决策 293：只挂尾部窗口；分页回来的整页挂上
        let firstMounted = mountWindow.Plan(merged |> List.map _.commitId, pane.Scroller.PrependAnchorPending)
        merged
        |> List.iteri (fun index message ->
            if index >= firstMounted && MessageView.hasVisibleBody message then
                // 生成仍在进行时最后一条助手消息里没结果的工具调用是「执行中」
                let awaitingTools =
                    generating && f.Streaming.IsNone && lastAssistant = Some index
                    && message.toolCalls |> List.exists (fun c -> c.result.IsNone)
                let ctx = ctxFor index awaitingTools
                let sib = siblingsOf message
                let key =
                    message.commitId
                    |> Option.map (fun commit ->
                        { commit = commit
                          role = message.role
                          toolCalls = List.length message.toolCalls
                          toolResults = List.length message.toolResults
                          attachments = List.length message.attachments
                          fontSize = ctx.FontSize
                          collapse = ctx.AutoCollapseReasoning
                          lastAssistant = ctx.IsLastAssistant
                          usageTag = (if ctx.IsLastAssistant then hash ctx.Usage else 0)
                          missingTag = (if List.isEmpty message.attachments then 0 else hash ctx.MissingAttachments)
                          siblingTag = (match sib with Some s -> hash (s.index, s.count, s.targets) | None -> 0)
                          awaitingTools = awaitingTools
                          approvalTag = (if awaitingTools then hash awaiting else 0) })
                desired.Add(
                    key,
                    fun () ->
                        let card = fst (cards.Render(message, ctx, actions.Card, Some index))
                        match sib with
                        | Some s -> withSiblingBar card s
                        | None -> card))
        // 流式卡：外形不变时原位更新正文，身份沿用（卡片不重建）
        match f.Streaming with
        | Some s when MessageView.hasVisibleBody s ->
            let ctx = ctxFor -1 true
            let shape = box ({ s with text = "" }, String.IsNullOrWhiteSpace s.text, f.FontSize, f.AutoCollapseReasoning, awaiting)
            match streamSlot with
            | Some(prev, key, live) when prev = shape ->
                live.Update s.text
                desired.Add(Some key, fun () -> fst (cards.Render(s, ctx, actions.Card, None)))
            | _ ->
                streamGeneration <- streamGeneration + 1
                let key =
                    { commit = UInt64.MaxValue; role = "streaming"; toolCalls = 0; toolResults = 0; attachments = 0
                      fontSize = f.FontSize; collapse = f.AutoCollapseReasoning; lastAssistant = false; usageTag = 0
                      missingTag = 0; siblingTag = streamGeneration; awaitingTools = false; approvalTag = 0 }
                let card, live = cards.Render(s, ctx, actions.Card, None)
                streamSlot <- live |> Option.map (fun l -> shape, key, l)
                desired.Add(Some key, fun () -> card)
        | _ -> streamSlot <- None
        // 中途失败的半截回复（决策 230）
        let partialNow =
            match f.Error with
            | Some _ -> partial |> Option.filter (fun (p, _) -> f.Streaming.IsNone && MessageView.hasVisibleBody p)
            | None -> None
        match partialNow with
        | Some(p, _) ->
            desired.Add(None, fun () -> stack (fst (cards.Render(p, ctxFor -1 false, actions.Card, None))) (cards.IncompleteMarker()))
        | None -> ()
        match f.Error with
        | Some(err, retry) -> desired.Add(None, fun () -> cards.ErrorCard(err, retry, actions.Card, partialNow |> Option.map snd))
        | None -> ()
        deck.Show(List.ofSeq desired) |> ignore
        mounted <- [| for key, _ in desired -> key |]
        // 锚点指向的卡不在了就丢；分页回来要续跳就等前置锚点恢复后再跳
        let resume = pane.Jumps.Mounted(mounted, mountWindow.LastFirstCommit)
        syncNav ()
        if resume then Dispatcher.UIThread.Post((fun () -> jumpExchange -1), DispatcherPriority.ContextIdle)
        // 窗口外未挂载的也算条数（决策 293）
        pane.Rendered(f.Streaming.IsSome, desired.Count + firstMounted, pane.Scroller.PrependAnchorPending)

    and rerender () = last |> Option.iter render

    /// 把窗口向上扩一步：前置锚点包住重绘
    and expandMount () =
        if mountWindow.HiddenCount = 0 then false
        else
            pane.Scroller.BeginPrependAnchor()
            mountWindow.Expand() |> ignore
            rerender ()
            pane.Scroller.RestorePrependAnchorDeferred()
            true

    and jumpExchange (direction: int) =
        match pane.Jump(direction, mounted, exchangeIndices (), currentExchange (), mountWindow.HiddenCount > 0, mountWindow.LastFirstCommit) with
        | JumpStep.ExpandAndRetry -> if expandMount () then jumpExchange direction
        | JumpStep.RequestOlder -> actions.RequestOlderHistory()
        | JumpStep.ScrollTo(index, _) ->
            if index < pane.Messages.Children.Count then smoothToCard pane.Messages.Children.[index]
        | JumpStep.Stay -> ()
        syncNav ()

    let indexOf commit = mounted |> Array.tryFindIndex (function Some k -> k.commit = commit | None -> false)

    let scrollToMessage (commit: uint64) =
        if mountWindow.EnsureMounted commit then rerender ()
        match indexOf commit with
        | Some _ ->
            pane.PinReveal()
            // 前置锚点恢复会写回旧偏移，这里的目标优先
            pane.Scroller.CancelPrependAnchor()
            pane.RefreshLatestButton()
            Dispatcher.UIThread.Post(
                (fun () ->
                    match indexOf commit with
                    | Some i when i < pane.Messages.Children.Count ->
                        let child = pane.Messages.Children.[i]
                        if pane.Follow.RevealPinned then revealTarget <- Some child
                        smoothToCard child
                    | _ -> ()),
                DispatcherPriority.Background)
            true
        | None -> false

    let endHighlight () = highlight |> Option.iter (fun (t, restore) -> t.Dispose(); restore ())

    let rec showMenu (anchor: Control) =
        syncNav ()
        let icon g = fun (b: IBrush) -> looks.Icon g b
        let item label action glyph = PopupEntry.create label action |> PopupEntry.withIcon (icon glyph)
        let prev, next = pane.Jumps.NavState(mounted, exchangeIndices (), currentExchange (), pane.Follow.AtBottom, mountWindow.HiddenCount > 0)
        let conversation =
            [ if editable then item "重命名" header.BeginRename Glyph.Pencil
              item "对话上下文" actions.OpenSessionSettings Glyph.Sliders
              item "从此分叉" actions.ForkFromHere Glyph.Fork
              match savePreset with
              | Some save -> item "存为预设…" save Glyph.Sliders
              | None -> ()
              item "导出（Markdown / JSON）…" actions.ExportConversation Glyph.Download ]
        let reading =
            [ if prev then item "上一条提问" (fun () -> jumpExchange -1) Glyph.ChevronsUp
              if next then item "下一条提问" (fun () -> jumpExchange 1) Glyph.ChevronsDown
              if pane.Messages.Children.Count > 0 then PopupEntry.create "回到对话开头" (fun () -> smoothToBeginning ()) ]
        let groups =
            [ "当前对话", conversation
              if not (List.isEmpty reading) then "阅读位置", reading
              if generating && canStop then
                  "执行", [ item "停止生成" (fun () -> actions.StopGeneration(); actions.FocusHome()) Glyph.Stop ] ]
        overlay.ShowGroupedMenu(anchor, false, groups)

    /// 平滑回到开头（锚点作废；减弱动效时延迟滚到顶）
    and smoothToBeginning () =
        pane.Jumps.ClearAnchor()
        pane.Follow.LeaveForBeginning()
        pane.Scroller.StopSmooth()
        if not (pane.Scroller.SmoothTo((fun () -> 0.0), pane.RefreshLatestButton, fun _ -> pane.RefreshLatestButton())) then
            if pane.Scroller.Reduced then pane.Scroller.ScrollToHomeDeferred()
        pane.RefreshLatestButton()

    do
        looks.Space3xl |> Observable.add (fun v -> top3xl <- v)
        looks.LogoAvatar |> Observable.add (fun v -> avatar <- v)
        looks.SpaceXs |> Observable.add (fun v -> xs <- v)
        looks.FontCaption |> Observable.add (fun v -> caption <- v)
        looks.IconButton |> Observable.add (fun v -> iconButton <- v)
        DockPanel.SetDock(header.Host, Dock.Top)
        layout.Children.Add header.Host
        layout.Children.Add pane.Host
        root.Child <- layout
        // 顶栏与滚动区同源取档：一次读自身宽度，同时写两处水平留白
        root.PropertyChanged.Add(fun e ->
            if e.Property = Visual.BoundsProperty then header.SetInset(LayoutPolicy.horizontalInset root.Bounds.Width))
        header.Renamed.Add actions.RenameTitle
        header.SidebarPressed.Add actions.ToggleSidebar
        header.MorePressed.Add showMenu
        header.CrumbPressed.Add(fun () -> breadcrumb.Activate() |> Option.iter actions.OpenParent)
        pane.FocusReturned.Add(fun () -> actions.FocusHome())
        // 审批卡在输入框正上方附属位：与输入框同列同宽、占布局高度
        composer.SetAccessory approvals.View
        approvals.AwaitingChanged.Add(fun ids ->
            if ids <> awaiting then
                awaiting <- ids
                rerender ())
        pane.Scroller.Wheeled.Add(fun () -> revealTarget <- None)
        pane.Scroller.Scrolled.Add(fun note ->
            // 决策 460：钉住期间内容高度变了（不是用户在滚）→ 目标卡重新对齐到顶部；动画中由动画自己对齐
            match revealTarget with
            | Some child when pane.Follow.RevealPinned && not pane.Scroller.IsSmoothing && abs note.ExtentDelta > 0.1 ->
                if isNull child.Parent then revealTarget <- None
                else
                    let target = max 0.0 (child.Bounds.Y - top3xl)
                    if abs (pane.Scroller.Offset - target) > 1.0 then pane.PinOffset target
            | _ -> ()
            if not pane.Follow.RevealPinned then revealTarget <- None
            syncNav ()
            // 到顶：本机还有没挂的先挂（只认用户往上推）；否则向主机要更早一页（上一页刚失败时不静默重发）
            if note.AtTop && not pane.IsSkeletonShown then
                if mountWindow.HiddenCount > 0 then
                    if note.Upward then expandMount () |> ignore
                elif not pane.Nav.FailureBanner.IsVisible then actions.RequestOlderHistory())

    /// 放进布局的根
    member _.Host: Control = root
    member _.Header = header
    member _.Pane = pane
    member _.Deck = deck
    member _.Mounted = mounted
    member _.HiddenMessageCount = mountWindow.HiddenCount

    /// 重绘（旧 RenderMessages）：挂载窗口规划 → 卡片对账 → 跳转续跳 → 贴底跟随，同步完成
    member _.Render(frame: ScreenFrame) = render frame

    /// 丢弃卡片缓存（后台着色算完时），下一次全部重建
    member _.InvalidateCards() =
        deck.Clear()
        mounted <- [||]
        streamSlot <- None

    member _.SetTitle(text: string, canEdit: bool) =
        editable <- canEdit
        header.SetTitle(text, canEdit)

    member _.SetGenerating(value: bool, statusText: string) =
        generating <- value
        header.SetGenerating(value, statusText)

    member _.SetCanStop(value: bool) = canStop <- value
    member _.SetConversationChrome(value: bool) =
        header.SetConversationChrome value
        syncNav ()
    member _.SetSavePresetAction(action: (unit -> unit) option) = savePreset <- action
    member _.SetSiblingProvider(f: uint64 -> uint64 option -> SiblingVersions option) = siblings <- f
    member _.SetAwaitingApproval(ids: Set<string>) = awaiting <- ids
    member _.SetPartialReply(p: (MessageView * (unit -> unit)) option) = partial <- p

    member _.SetBreadcrumb(crumb: ForkBreadcrumb option) =
        let view = breadcrumb.Set(crumb |> Option.map (fun c -> c.text, c.description, c.parentAvailable, c.parentId))
        header.SetCrumb(if view.visible then Some { Text = view.text; Description = view.description; Clickable = view.clickable } else None)
    member _.BreadcrumbText = breadcrumb.Text

    member _.SetCompactMode(value: bool) =
        header.SetCompact value
        pane.SetCompact value

    member _.SetStartMode(value: bool) = pane.SetStartMode value

    /// 宿主菜单（快捷键打开）
    member _.ShowMenu() = showMenu (header.MoreButton :> Control)
    member _.JumpExchange(direction: int) = jumpExchange direction
    member _.ExpandMountWindow() = expandMount ()

    /// 滚到一条消息（窗口外先扩到它）；不在已加载范围时返回 false，由宿主先要更早历史
    member _.ScrollToMessage(commit: uint64) = scrollToMessage commit

    /// 滚到一条消息并短暂高亮（第 211 节）；停留计时归宿主（actions.After）
    member _.RevealAndHighlight(commit: uint64) =
        if scrollToMessage commit then
            flash.Revealed commit
            Dispatcher.UIThread.Post(
                (fun () ->
                    match indexOf commit with
                    | Some i when i < pane.Messages.Children.Count ->
                        let card = pane.Messages.Children.[i]
                        let isSurface (o: obj) =
                            match o with
                            | :? Border as b when b.Name = MessageCards.HighlightName -> Some b
                            | _ -> None
                        let surface =
                            isSurface card
                            |> Option.orElse (card.GetVisualDescendants() |> Seq.tryPick isSurface)
                            |> Option.orElse (card.GetLogicalDescendants() |> Seq.tryPick isSurface)
                        match surface with
                        | Some b ->
                            endHighlight ()
                            b.Background <- looks.Highlight
                            flash.Begin commit |> ignore
                            let restore () =
                                b.Background <- Brushes.Transparent
                                flash.End() |> ignore
                                highlight <- None
                            let timer = actions.After (TimeSpan.FromMilliseconds RevealFlash.DwellMs) restore
                            highlight <- Some(timer, restore)
                        | None -> ()
                    | _ -> ()),
                DispatcherPriority.Background)
            true
        else false

    member _.EndRevealHighlight() = endHighlight ()
    member _.HighlightedCommit = flash.Highlighted
    member _.LastFlashedCommit = flash.LastFlashed

    /// 平滑回到最新（锚点与钉住一并解除）
    member _.SmoothScrollToEnd() =
        revealTarget <- None
        pane.ScrollToLatest()
    member _.SmoothScrollToBeginning() = smoothToBeginning ()
    /// 用户意图：回到开头（锚点作废）
    member _.ScrollToBeginning() =
        pane.Jumps.ClearAnchor()
        pane.Scroller.ScrollToHomeDeferred()
    member _.ScrollToEndDeferred() = pane.Scroller.ScrollToEndDeferred(fun () -> pane.Follow.RevealPinned)

    /// 更早一页：取之前记几何，渲染后补偿（旧 Begin/RestoreHistoryPrependAnchor）
    member _.BeginHistoryPrependAnchor() = pane.Scroller.BeginPrependAnchor()
    member _.RestoreHistoryPrependAnchorDeferred() = pane.Scroller.RestorePrependAnchorDeferred()
    member _.CancelHistoryPrependAnchor() = pane.Scroller.CancelPrependAnchor()

    /// 换对话
    member _.ResetScrollState() =
        mountWindow.Reset()
        revealTarget <- None
        pane.Reset()
        syncNav ()
