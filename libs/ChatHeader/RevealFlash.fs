namespace Wanxiang.UI

/// 旧 Views/ChatView.fs RevealAndHighlight 的高亮记账：最近闪过的消息、正在高亮的消息。
/// 计时器（1600 毫秒）与背景刷仍归 ChatView：Begin 返回要先复原的上一条，计时到点或提前结束调 End。
type RevealFlash() =
    let mutable lastFlashed: uint64 option = None
    let mutable highlighted: uint64 option = None

    /// 高亮停留时长（毫秒）
    static member DwellMs = 1600.0

    member _.LastFlashed = lastFlashed
    member _.Highlighted = highlighted

    /// 滚动定位成功（还没找到承载面也算闪过）
    member _.Revealed(commit: uint64) = lastFlashed <- Some commit

    /// 找到承载面、开始高亮；返回仍在高亮、需先复原的上一条
    member _.Begin(commit: uint64) =
        let previous = highlighted
        highlighted <- Some commit
        previous

    /// 结束高亮（到点或提前）；返回被复原的那条
    member _.End() =
        let previous = highlighted
        highlighted <- None
        previous
