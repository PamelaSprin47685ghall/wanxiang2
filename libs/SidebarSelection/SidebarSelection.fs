namespace Wanxiang.UI

open System
open System.Collections.Generic

/// 批量操作条与侧栏外框的呈现
type SelectionChrome =
    { countText: string
      selectAllText: string
      selectAllHelp: string
      pinText: string
      selectAllEnabled: bool
      /// 置顶、归档、删除三个键
      batchEnabled: bool
      /// 选择头与批量条可见；品牌行与页脚相反
      barsVisible: bool }

/// 旧 Views/Sidebar.fs 的多选状态（EnterSelection、ToggleSelected、ExitSelection、ToggleSelectAllVisible、
/// SelectedIds、RefreshSelectionChrome 的文案与启用）。行视觉与按钮控件仍归 Sidebar。
type SidebarSelection() =
    let mutable active = false
    let selected = HashSet<Guid>()

    member _.Active = active
    /// 原始计数（含已离开列表的项）
    member _.Count = selected.Count
    member _.IsSelected(id: Guid) = active && selected.Contains id

    /// 进入模式并以 seed 为第一项；已在模式内则只并入
    member _.Enter(seed: Guid) =
        if not active then
            active <- true
            selected.Clear()
        selected.Add seed |> ignore

    member _.Exit() =
        active <- false
        selected.Clear()

    /// 切换一行；清空最后一项时退出模式
    member this.Toggle(id: Guid) =
        if not (selected.Remove id) then selected.Add id |> ignore
        if selected.Count = 0 then this.Exit()

    /// 可见行已全选则退出模式，否则选中全部可见行（原有的不可见项被清掉）
    member this.ToggleAllVisible(visible: Guid list) =
        if not (List.isEmpty visible) && visible |> List.forall selected.Contains then this.Exit()
        else
            active <- true
            selected.Clear()
            for id in visible do selected.Add id |> ignore

    /// 可见的已选项，按可见行次序
    member _.SelectedIds(visible: Guid list) = visible |> List.filter selected.Contains

    member this.Chrome(visible: Guid list, allPinned: Guid list -> bool) =
        let allVisible = not (List.isEmpty visible) && visible |> List.forall selected.Contains
        let usable = this.SelectedIds visible
        { countText = sprintf "已选 %d 项" selected.Count
          selectAllText = (if allVisible then "取消全选" else "全选")
          selectAllHelp = (if allVisible then "取消全选（保留多选状态）" else "全选当前可见对话")
          pinText = (if allPinned usable then "取消置顶" else "置顶")
          selectAllEnabled = not (List.isEmpty visible)
          batchEnabled = not (List.isEmpty usable)
          barsVisible = active }
