namespace Wanxiang.UI

/// 一次跳转要控件做的事（限定访问）
[<RequireQualifiedAccess>]
type JumpStep =
    /// 没有可去的
    | Stay
    /// 本机已加载但未挂载：扩展挂载窗口，扩成了就再跳一次
    | ExpandAndRetry
    /// 向主机要更早一页，到达后再跳（见 Mounted 的返回）
    | RequestOlder
    /// 平滑滚到第 index 张卡；atBottom 为它是否最后一轮
    | ScrollTo of index: int * atBottom: bool

/// 旧 Views/ChatView.fs 的问答跳转（上一条 / 下一条）状态机：锚点、挂起的「上一轮」、
/// 主机上是否还有更早历史，以及端点可用态。'K 为卡身份（旧 CardKey）；mounted 与面板子项逐一对齐，
/// cards 为其中用户卡的下标（升序）。控件、滚动动画仍归 ChatView。
type ExchangeJump<'K when 'K: equality>() =
    let mutable anchor: 'K option = None
    let mutable pending = false
    let mutable pendingFirst: uint64 option = None
    let mutable olderOnHost = false

    let current (mounted: 'K option[]) (viewportIndex: int) =
        match anchor with
        | Some key -> mounted |> Array.tryFindIndex ((=) (Some key)) |> Option.defaultValue -1
        | None -> viewportIndex

    /// 用户卡里第一张底边越过视口顶端的；都没越过时退回首张，没有用户卡为 0
    static member ViewportIndex(cards: int list, pastTop: int -> bool) =
        cards |> List.tryFind pastTop |> Option.orElse (List.tryHead cards) |> Option.defaultValue 0

    /// 挂载卡的用户卡下标
    static member Cards(mounted: 'K option[], isUser: 'K -> bool) =
        mounted |> Array.indexed |> Array.choose (fun (i, k) -> match k with Some k when isUser k -> Some i | _ -> None) |> List.ofArray

    member _.Anchor = anchor
    member _.PendingPrevious = pending
    member _.OlderOnHost = olderOnHost

    /// 端点可用态：(上一轮, 下一轮)。贴底时没有下一轮；窗口外或主机上还有更早的，上一轮永远可用
    member _.NavState(mounted: 'K option[], cards: int list, viewportIndex: int, atBottom: bool, hasHidden: bool) =
        let at = current mounted viewportIndex
        let hasPrev = cards |> List.exists (fun i -> i < at)
        let hasNext = cards |> List.exists (fun i -> i > at) && not atBottom
        hasPrev || (not (List.isEmpty cards) && (hasHidden || olderOnHost)), hasNext

    /// 跳转。direction < 0 向上；锚点一旦设上，后续跳转跟着锚点走
    member _.Jump(direction: int, mounted: 'K option[], cards: int list, viewportIndex: int, hasHidden: bool, lastFirstCommit: uint64 option) =
        if List.isEmpty cards then JumpStep.Stay
        else
            let at = current mounted viewportIndex
            let target =
                if direction < 0 then cards |> List.filter (fun i -> i < at) |> List.tryLast
                else cards |> List.tryFind (fun i -> i > at)
            let pin () = if anchor.IsNone && at >= 0 then anchor <- mounted.[at]
            match target with
            | None when direction < 0 && hasHidden ->
                pin ()
                JumpStep.ExpandAndRetry
            | None when direction < 0 && olderOnHost ->
                pin ()
                pending <- true
                pendingFirst <- lastFirstCommit
                JumpStep.RequestOlder
            | Some index ->
                match mounted.[index] with
                | Some key -> anchor <- Some key
                | None -> ()
                JumpStep.ScrollTo(index, (index = List.max cards))
            | None -> JumpStep.Stay

    /// 渲染后：锚点卡不在了就丢；挂起的「上一轮」在首条提交变化（新页到了）时兑现，返回 true 表示要再跳一次上一轮
    member _.Mounted(mounted: 'K option[], lastFirstCommit: uint64 option) =
        match anchor with
        | Some key when not (Array.contains (Some key) mounted) -> anchor <- None
        | _ -> ()
        if pending && lastFirstCommit <> pendingFirst then
            pending <- false
            true
        else false

    /// 主机上是否还有更早一页；没有了就撤掉挂起
    member _.ApplyOlderAvailable(value: bool) =
        olderOnHost <- value
        if not value then pending <- false

    /// 更早一页取失败：撤掉挂起
    member _.OlderFailed() = pending <- false

    /// 回到开头（ScrollToBeginning / SmoothScrollToBeginning）：只丢锚点，挂起的「上一轮」保留
    member _.ClearAnchor() = anchor <- None

    /// 换对话：锚点与挂起一起丢（olderOnHost 由下一次 ApplyOlderAvailable 给）
    member _.Reset() =
        anchor <- None
        pending <- false
