namespace Wanxiang.UI

/// 菜单方向键（限定访问）
[<RequireQualifiedAccess>]
type RovingKey =
    | Up
    | Down
    | Home
    | End
    | PageUp
    | PageDown

/// 列表内的键盘移动：旧 Controls/Menu.fs wireDirectionalNavigation 与 Views/SettingsView.fs NavigateSection
module RovingIndex =

    /// 菜单：current 为 -1 表示没有焦点项；空列表返回 None
    let menu (key: RovingKey) (current: int) (count: int) =
        if count <= 0 then None
        else
            Some(
                match key with
                | RovingKey.Home
                | RovingKey.PageUp -> 0
                | RovingKey.End
                | RovingKey.PageDown -> count - 1
                | RovingKey.Up -> if current <= 0 then count - 1 else current - 1
                | RovingKey.Down -> if current < 0 || current >= count - 1 then 0 else current + 1)

    /// 设置分区：按方向循环（direction 可正可负，绝对值不超过 count）
    let wrap (index: int) (direction: int) (count: int) =
        let raw = (index + direction) % count
        if raw < 0 then raw + count else raw
