namespace Wanxiang.UI

// 照抄旧 Views/ChatView.fs：exchangeIndices、currentExchangeIndex、syncExchangeNavState、JumpExchange、
// RenderMessages 末尾的锚点与挂起处理、SetOlderAvailable、ShowOlderPageFailure、换对话的重置。
// 控件与滚动换成返回值；扩窗的递归换成 ExpandAndRetry
[<RequireQualifiedAccess>]
type JumpStep =
    | Stay
    | ExpandAndRetry
    | RequestOlder
    | ScrollTo of index: int * atBottom: bool

type ExchangeJump<'K when 'K: equality>() =
    let mutable exchangeAnchor: 'K option = None
    let mutable pendingJumpPrevious = false
    let mutable pendingJumpFirst: uint64 option = None
    let mutable olderOnHost = false

    static member ViewportIndex(cards: int list, pastTop: int -> bool) =
        cards
        |> List.filter pastTop
        |> List.tryHead
        |> Option.orElseWith (fun () -> List.tryHead cards)
        |> Option.defaultValue 0

    static member Cards(mountedKeys: 'K option[], isUser: 'K -> bool) =
        mountedKeys
        |> Array.mapi (fun index key ->
            match key with
            | Some k when isUser k -> Some index
            | _ -> None)
        |> Array.choose id
        |> Array.toList

    member _.Anchor = exchangeAnchor
    member _.PendingPrevious = pendingJumpPrevious
    member _.OlderOnHost = olderOnHost

    member _.NavState(mountedKeys: 'K option[], cards: int list, viewportIndex: int, atBottom: bool, hasHidden: bool) =
        let anchorIndex =
            match exchangeAnchor with
            | Some key -> mountedKeys |> Array.tryFindIndex ((=) (Some key)) |> Option.defaultValue -1
            | None -> viewportIndex
        let hasPrev = cards |> List.exists (fun index -> index < anchorIndex)
        let hasNext = cards |> List.exists (fun index -> index > anchorIndex)
        let hasNext = hasNext && not atBottom
        let hasPrev = hasPrev || (not (List.isEmpty cards) && (hasHidden || olderOnHost))
        hasPrev, hasNext

    member _.Jump(direction: int, mountedKeys: 'K option[], cards: int list, viewportIndex: int, hasHidden: bool, lastFirstCommit: uint64 option) =
        let mutable step = JumpStep.Stay
        if not (List.isEmpty cards) then
            let current =
                match exchangeAnchor with
                | Some key ->
                    mountedKeys
                    |> Array.tryFindIndex ((=) (Some key))
                    |> Option.defaultValue -1
                | None -> viewportIndex
            let target =
                if direction < 0 then
                    cards
                    |> List.filter (fun index -> index < current)
                    |> List.tryLast
                else
                    cards |> List.tryFind (fun index -> index > current)
            match target with
            | None when direction < 0 && hasHidden ->
                if exchangeAnchor.IsNone && current >= 0 then exchangeAnchor <- mountedKeys.[current]
                step <- JumpStep.ExpandAndRetry
            | None when direction < 0 && olderOnHost ->
                if exchangeAnchor.IsNone && current >= 0 then exchangeAnchor <- mountedKeys.[current]
                pendingJumpPrevious <- true
                pendingJumpFirst <- lastFirstCommit
                step <- JumpStep.RequestOlder
            | Some index ->
                match mountedKeys.[index] with
                | Some key -> exchangeAnchor <- Some key
                | None -> ()
                step <- JumpStep.ScrollTo(index, (index = List.max cards))
            | None -> ()
        step

    member _.Mounted(mountedKeys: 'K option[], lastFirstCommit: uint64 option) =
        match exchangeAnchor with
        | Some key when not (Array.contains (Some key) mountedKeys) -> exchangeAnchor <- None
        | _ -> ()
        if pendingJumpPrevious && lastFirstCommit <> pendingJumpFirst then
            pendingJumpPrevious <- false
            true
        else false

    member _.SetOlderAvailable(value: bool) =
        olderOnHost <- value
        if not value then pendingJumpPrevious <- false

    member _.OlderFailed() = pendingJumpPrevious <- false

    // 照抄旧 ScrollToBeginning / SmoothScrollToBeginning：exchangeAnchor <- None
    member _.ClearAnchor() = exchangeAnchor <- None

    member _.Reset() =
        exchangeAnchor <- None
        pendingJumpPrevious <- false
