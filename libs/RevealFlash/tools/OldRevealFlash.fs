namespace Wanxiang.UI

// 照抄旧 Views/ChatView.fs RevealAndHighlight / EndRevealHighlight 的记账；
// highlightTimer 与 restore 闭包换成「当前高亮项」，restore() 即 End
type RevealFlash() =
    let mutable lastFlashedCommit: uint64 option = None
    let mutable highlightedCommit: uint64 option = None
    let mutable highlightTimer: (uint64 * (unit -> unit)) option = None

    static member DwellMs = 1600.0
    member _.LastFlashed = lastFlashedCommit
    member _.Highlighted = highlightedCommit
    member _.Revealed(commitId: uint64) = lastFlashedCommit <- Some commitId

    member _.Begin(commitId: uint64) =
        let mutable restored = None
        highlightTimer |> Option.iter (fun (c, restore) -> restored <- Some c; restore ())
        highlightedCommit <- Some commitId
        let restore () =
            highlightedCommit <- None
            highlightTimer <- None
        highlightTimer <- Some(commitId, restore)
        restored

    member _.End() =
        let mutable restored = None
        highlightTimer |> Option.iter (fun (c, restore) -> restored <- Some c; restore ())
        restored
