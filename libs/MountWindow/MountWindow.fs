namespace Wanxiang.UI

/// 旧 Views/ChatView.fs 的挂载窗口记账（决策 293）：长对话只挂尾部 window 张卡，
/// 往上滚到顶或跳转、定位到窗口外时再扩。卡片、prepend 锚点与重绘仍归 ChatView。
type MountWindow(initial: int, step: int) =
    let mutable window = initial
    let mutable hidden: uint64[] = [||]
    let mutable lastFirst: uint64 option = None
    let mutable lastCount = 0

    /// 旧 ChatView.InitialMountWindow / MountWindowStep
    static member Initial = 40
    static member Step = 60

    member _.Window = window
    /// 本帧已加载但未挂载的提交（按顺序）
    member _.Hidden = hidden
    member _.HiddenCount = hidden.Length
    member _.LastFirstCommit = lastFirst

    /// 每次渲染：commits 为合并后的消息列表（流式卡等没有提交号的为 None）。
    /// prependInFlight 为分页前置锚点在途；更早一页到达时整页挂上。返回第一张挂载的下标
    member _.Plan(commits: uint64 option list, prependInFlight: bool) =
        let count = List.length commits
        let first = commits |> List.tryHead |> Option.flatten
        if prependInFlight && first <> lastFirst && count > lastCount && lastCount > 0 then
            window <- max window (min count (lastCount - hidden.Length + (count - lastCount)))
        lastFirst <- first
        lastCount <- count
        let firstMounted = max 0 (count - window)
        hidden <- commits |> List.truncate firstMounted |> List.choose id |> Array.ofList
        firstMounted

    /// 向上扩一步；没有隐藏的返回 false。true 时调用方挂 prepend 锚点并重绘
    member _.Expand() =
        if hidden.Length = 0 then false
        else
            window <- window + step
            true

    /// 确保某条已挂载：在窗口外就扩到它（多留 4 张）；true 时调用方重绘
    member _.EnsureMounted(commit: uint64) =
        match Array.tryFindIndex ((=) commit) hidden with
        | Some index ->
            window <- window + (hidden.Length - index) + 4
            true
        | None -> false

    /// 换对话：窗口回到初始（隐藏表与首条记录等下一次 Plan 重算）
    member _.Reset() = window <- initial
