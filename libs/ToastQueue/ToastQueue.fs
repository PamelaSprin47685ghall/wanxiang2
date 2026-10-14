namespace Wanxiang.UI

/// 提示条的去重与条数上限（旧 Controls/OverlayHost.fs 的 toastEntries 与 ToastWith 入条步骤）。
///
/// 同文只留最新一条：新条进来先撤掉同文旧条；然后挤掉最旧的，直到空出位置。
/// 返回要撤下的提示（按撤下次序），调用方撤控件；撤下也会从队列里移除同文的全部条目。
type ToastQueue(capacity: int) =
    let entries = ResizeArray<string>()

    new() = ToastQueue(3)

    /// 当前在显示的提示，最旧在前。
    member _.Showing = List.ofSeq entries

    /// 撤下一条（点击、Esc、到时）：同文条目一并移除。
    member _.Remove(message: string) = entries.RemoveAll(fun t -> t = message) |> ignore

    /// 新提示入队；返回因去重或超限而要撤下的提示。
    member this.Admit(message: string) : string list =
        let dismissed = ResizeArray<string>()
        if entries.Contains message then
            dismissed.Add message
            this.Remove message
        while entries.Count >= capacity do
            let oldest = entries[0]
            dismissed.Add oldest
            this.Remove oldest
        entries.Add message
        List.ofSeq dismissed
