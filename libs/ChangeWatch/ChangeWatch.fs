namespace Wanxiang.UI

/// 轮询值的变化检测（迁自旧 AppShell.WatchHostViewport：CSS 视口宽度、页面恢复代号）。
/// 宿主每次轮询把读到的值交进来；与上次记下的不同才算变化，并记下新值。
type ChangeWatch<'value when 'value: equality>(initial: 'value option) =
    let mutable last = initial

    new() = ChangeWatch<'value>(None)

    /// 读到新值：变了返回 true 并记下；没变返回 false。
    member _.Observe(value: 'value) : bool =
        if last = Some value then false
        else
            last <- Some value
            true

    member _.Last = last
