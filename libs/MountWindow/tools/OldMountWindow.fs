namespace Wanxiang.UI

// 照抄旧 Views/ChatView.fs 的 mountWindow / hiddenCommits / lastFirstCommit / lastMergedCount 记账：
// RenderMessages 的窗口段、ExpandMountWindow、EnsureMounted、ResetScrollState 的窗口复位（重绘与锚点换成返回值）
type MountWindow(initialMountWindow: int, mountWindowStep: int) =
    let mutable mountWindow = initialMountWindow
    let mutable hiddenCommits: uint64[] = [||]
    let mutable lastFirstCommit: uint64 option = None
    let mutable lastMergedCount = 0

    static member Initial = 40
    static member Step = 60
    member _.Window = mountWindow
    member _.Hidden = hiddenCommits
    member _.HiddenCount = hiddenCommits.Length
    member _.LastFirstCommit = lastFirstCommit

    member _.Plan(merged: uint64 option list, pendingHistoryAnchorIsSome: bool) =
        let mergedCount = List.length merged
        let firstCommit = merged |> List.tryHead |> Option.bind id
        if pendingHistoryAnchorIsSome && firstCommit <> lastFirstCommit && mergedCount > lastMergedCount && lastMergedCount > 0 then
            mountWindow <- max mountWindow (min mergedCount (lastMergedCount - hiddenCommits.Length + (mergedCount - lastMergedCount)))
        lastFirstCommit <- firstCommit
        lastMergedCount <- mergedCount
        let firstMounted = max 0 (mergedCount - mountWindow)
        hiddenCommits <- merged |> List.truncate firstMounted |> List.choose id |> Array.ofList
        firstMounted

    member _.Expand() =
        if hiddenCommits.Length = 0 then false
        else
            mountWindow <- mountWindow + mountWindowStep
            true

    member _.EnsureMounted(commitId: uint64) =
        match Array.tryFindIndex ((=) commitId) hiddenCommits with
        | Some index ->
            mountWindow <- mountWindow + (hiddenCommits.Length - index) + 4
            true
        | None -> false

    member _.Reset() = mountWindow <- initialMountWindow
