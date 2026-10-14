namespace Wanxiang.UI

// 照抄旧 Views/Sidebar.fs 的多选成员（Dictionary 与 visibleRowIds 数组原样），按钮写入换成记录字段
open System

type SelectionChrome =
    { countText: string; selectAllText: string; selectAllHelp: string; pinText: string
      selectAllEnabled: bool; batchEnabled: bool; barsVisible: bool }

type SidebarSelection() =
    let mutable selectionMode = false
    let selectedIds = System.Collections.Generic.Dictionary<Guid, unit>()
    let mutable visibleRowIds: Guid[] = [||]

    member _.Active = selectionMode
    member _.Count = selectedIds.Count
    member _.IsSelected(id: Guid) = selectionMode && selectedIds.ContainsKey id

    member this.Enter(seedId: Guid) =
        if not selectionMode then
            selectionMode <- true
            selectedIds.Clear()
            selectedIds.[seedId] <- ()
        elif not (selectedIds.ContainsKey seedId) then
            selectedIds.[seedId] <- ()

    member this.Toggle(id: Guid) =
        if selectedIds.ContainsKey id then selectedIds.Remove id |> ignore
        else selectedIds.[id] <- ()
        if selectedIds.Count = 0 then this.Exit()

    member this.Exit() =
        selectionMode <- false
        selectedIds.Clear()

    member this.ToggleAllVisible(visible: Guid list) =
        visibleRowIds <- Array.ofList visible
        let allSelected =
            visibleRowIds.Length > 0
            && visibleRowIds |> Array.forall selectedIds.ContainsKey
        if allSelected then this.Exit()
        else
            if not selectionMode then selectionMode <- true
            selectedIds.Clear()
            for id in visibleRowIds do selectedIds.[id] <- ()

    member _.SelectedIds(visible: Guid list) : Guid list =
        visibleRowIds <- Array.ofList visible
        visibleRowIds
        |> Array.filter selectedIds.ContainsKey
        |> Array.toList

    member this.Chrome(visible: Guid list, selectionAllPinned: Guid list -> bool) =
        visibleRowIds <- Array.ofList visible
        let count = selectedIds.Count
        let usableCount = (this.SelectedIds visible).Length
        { countText = sprintf "已选 %d 项" count
          selectAllText = (if visibleRowIds.Length > 0 && visibleRowIds |> Array.forall selectedIds.ContainsKey then "取消全选" else "全选")
          pinText = (if selectionAllPinned (this.SelectedIds visible) then "取消置顶" else "置顶")
          selectAllEnabled = (visibleRowIds.Length > 0)
          batchEnabled = (usableCount > 0)
          selectAllHelp =
            (if visibleRowIds.Length > 0 && visibleRowIds |> Array.forall selectedIds.ContainsKey then
                "取消全选（保留多选状态）"
             else "全选当前可见对话")
          barsVisible = selectionMode }
