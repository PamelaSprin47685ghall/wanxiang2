namespace Wanxiang.UI

// 照抄旧 Views/ModelSwitcherView.fs build 里 render 的分组、回车与列表 KeyDown 的落点（控件换成返回值）
open System

[<RequireQualifiedAccess>]
type PickerMove =
    | ToSearch
    | Focus of index: int

module ModelPickerList =
    let groups (providerLabel: 'T -> string) (query: string) (shown: 'T list) =
        let grouped = shown |> List.groupBy (fun o -> providerLabel o)
        let showHeaders = String.IsNullOrWhiteSpace query && grouped.Length > 1
        [ for (label, items) in grouped do
            yield ((if showHeaders then Some label else None), items) ]

    let enterPick (shown: 'T list) =
        match shown with
        | first :: _ -> Some first
        | [] -> None

    let shouldPick (isCurrent: bool) = if not isCurrent then true else false

    let move (up: bool) (current: int) (count: int) =
        if count > 0 then
            if up && current <= 0 then Some PickerMove.ToSearch
            else
                let next = if up then current - 1 else min (count - 1) (current + 1)
                Some(PickerMove.Focus next)
        else None

    let emptyText = "没有匹配的模型。"
    let searchHelp = "输入筛选，回车选第一条，下方向键进入列表，Esc 关闭"
