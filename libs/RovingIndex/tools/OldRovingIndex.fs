namespace Wanxiang.UI

// 照抄旧 Controls/Menu.fs wireDirectionalNavigation 的落点计算与 Views/SettingsView.fs NavigateSection 的取模
[<RequireQualifiedAccess>]
type RovingKey = | Up | Down | Home | End | PageUp | PageDown

module RovingIndex =
    let menu (key: RovingKey) (current: int) (count: int) =
        let items = Array.zeroCreate<int> count
        if items.Length > 0 then
            let next =
                match key with
                | RovingKey.Home
                | RovingKey.PageUp -> 0
                | RovingKey.End
                | RovingKey.PageDown -> items.Length - 1
                | RovingKey.Up -> if current <= 0 then items.Length - 1 else current - 1
                | _ -> if current < 0 || current >= items.Length - 1 then 0 else current + 1
            Some next
        else None

    let wrap (idx: int) (direction: int) (count: int) =
        let raw = (idx + direction) % count
        if raw < 0 then raw + count else raw
