namespace Wanxiang.UI

open System

/// 删除后焦点落到哪里。
[<RequireQualifiedAccess>]
type RowFocusTarget =
    /// 聚焦可见顺序里这一行（下标, id）
    | Row of index: int * id: Guid
    /// 没有行了：回到搜索框
    | Search

/// 侧栏行焦点（迁自旧 Views/Sidebar.fs 的 visibleRowIds、focusedRowId、focusedRowIndex、pendingFocusId，
/// 及 NeighborIndexAfterDelete、FocusAfterDelete、Rebuild 的焦点还原）。
/// 「删除后打开谁」与「删除后焦点落谁」读同一个答案。控件聚焦归宿主。
type RowFocus() =
    let mutable visible: Guid array = [||]
    let mutable focusedId: Guid option = None
    let mutable focusedIndex = -1
    let mutable pending: Guid option = None

    /// 列表重建后的可见行顺序。
    member _.ApplyVisible(ids: Guid array) = visible <- Array.copy ids

    member _.Visible: Guid list = List.ofArray visible

    member _.FocusedId = focusedId

    /// 某行拿到焦点：记下 id 与它的下标（不在可见行里时保留旧下标）。
    member _.Focused(id: Guid) =
        focusedId <- Some id
        focusedIndex <- visible |> Array.tryFindIndex ((=) id) |> Option.defaultValue focusedIndex

    /// 邻位下标：被删行还在则下一行优先，其次上一行；已不在则按上次行焦点位置钳制。没有行为 None。
    member _.NeighborIndexAfterDelete(removedId: string) : int option =
        if visible.Length = 0 then None
        else
            let clamp () = Some(min (max focusedIndex 0) (visible.Length - 1))
            match Guid.TryParse removedId with
            | true, removed ->
                match visible |> Array.tryFindIndex ((=) removed) with
                | Some idx ->
                    if idx + 1 < visible.Length then Some(idx + 1)
                    elif idx - 1 >= 0 then Some(idx - 1)
                    else None
                | None -> clamp ()
            | _ -> clamp ()

    member this.NeighborAfterDelete(removedId: string) : Guid option =
        this.NeighborIndexAfterDelete removedId |> Option.map (fun idx -> visible[idx])

    /// 删除时：瞄准邻位并记下意图（下次重建后补聚焦）；没有邻位则回搜索框。
    member this.FocusAfterDelete(removedId: string) : RowFocusTarget =
        match this.NeighborIndexAfterDelete removedId with
        | Some idx ->
            let id = visible[idx]
            pending <- Some id
            focusedId <- Some id
            focusedIndex <- idx
            RowFocusTarget.Row(idx, id)
        | None -> RowFocusTarget.Search

    /// 重建后该聚焦哪一行（在 ApplyVisible 之后调用）。删除流记下的意图优先，且只消费一次；
    /// 否则还给重建前正聚焦、仍可见的那一行（restoreId 由宿主按控件焦点给出）。
    member _.RestoreAfterRebuild(restoreId: Guid option) : Guid option =
        let intent =
            match pending with
            | Some id ->
                pending <- None
                if visible |> Array.contains id then Some id else None
            | None -> None
        match intent, restoreId with
        | Some id, _ -> Some id
        | None, Some id when visible |> Array.contains id -> Some id
        | _ -> None
