namespace Wanxiang.UI

/// 设置页「本机草稿」区与外壳之间的桥：清除动作与当前概况。
type LocalDraftControls() =
    let changed = Event<unit>()
    let clearRequested = Event<unit>()
    let mutable summary = ""

    /// 点「清除本地草稿」后发出；宿主订阅后执行实际清除。
    [<CLIEvent>]
    member _.ClearRequested = clearRequested.Publish

    /// 设置页的清除按钮调用。
    member _.RequestClear() = clearRequested.Trigger()

    [<CLIEvent>]
    member _.Changed = changed.Publish

    member _.Summary
        with get () = summary
        and set (value: string) =
            if summary <> value then
                summary <- value
                changed.Trigger()
