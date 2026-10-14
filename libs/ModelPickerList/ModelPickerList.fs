namespace Wanxiang.UI

open System

/// 列表里方向键的去处（限定访问）
[<RequireQualifiedAccess>]
type PickerMove =
    | ToSearch
    | Focus of index: int

/// 旧 Views/ModelSwitcherView.fs 的列表呈现：按模型服务分组、组头只在无查询且多组时显示，
/// 回车选第一条，列表内上下移动（不循环，顶端上移回搜索框）。筛选本身在 ModelSwitcher
module ModelPickerList =

    /// (组头, 项)；组头 None 表示不显示
    let groups (providerOf: 'T -> string) (query: string) (shown: 'T list) =
        let grouped = shown |> List.groupBy providerOf
        let showHeaders = String.IsNullOrWhiteSpace query && grouped.Length > 1
        grouped |> List.map (fun (label, items) -> (if showHeaders then Some label else None), items)

    /// 回车：第一条命中
    let enterPick (shown: 'T list) = List.tryHead shown

    /// 当前项再点不切换
    let shouldPick (isCurrent: bool) = not isCurrent

    /// 列表内上下：current 为 -1 表示没有焦点项；空列表 None
    let move (up: bool) (current: int) (count: int) =
        if count <= 0 then None
        elif up && current <= 0 then Some PickerMove.ToSearch
        else Some(PickerMove.Focus(if up then current - 1 else min (count - 1) (current + 1)))

    let emptyText = "没有匹配的模型。"
    let searchHelp = "输入筛选，回车选第一条，下方向键进入列表，Esc 关闭"
