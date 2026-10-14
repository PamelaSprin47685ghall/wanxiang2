namespace Wanxiang.UI

/// 设置页「本机草稿」区与外壳之间的桥：清除动作与当前概况。
type LocalDraftControls() =
    let changed = Event<unit>()
    let mutable summary = ""

    /// 点「清除本地草稿」后触发；由 AppShell 执行实际清除。
    member val ClearRequested: unit -> unit = ignore with get, set

    [<CLIEvent>]
    member _.Changed = changed.Publish

    member _.Summary
        with get () = summary
        and set (value: string) =
            if summary <> value then
                summary <- value
                changed.Trigger()
