namespace Wanxiang.UI

open System

// 原样摘自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs：NeighborExcluding（activeConvId、sidebar.GetVisibleOrder() 换成参数）
// 与 ScheduleDelete 撤销回调里的提示文案。
module PendingDeletion =
    let neighborAfter (activeConvId: Guid option) (removedIds: Guid list) (visibleOrder: string list) : Guid option =
        match activeConvId with
        | Some currentId when List.contains currentId removedIds ->
            let removed = Set.ofList removedIds
            visibleOrder
            |> List.tryPick (fun id ->
                match Guid.TryParse id with
                | true, g when not (removed.Contains g) -> Some g
                | _ -> None)
        | _ -> None

    let restoredText (count: int) =
        let ids = List.replicate count ()
        (if ids.Length = 1 then "已恢复对话" else sprintf "已恢复 %d 个对话" ids.Length)
