namespace Wanxiang.UI

open System

/// 旧 Views/Sidebar.fs 的多选状态。
///
/// **多选的事实已收进状态树**（`libs/WorkspaceState` 的 `Chrome.BatchMode` / `Chrome.SelectedRowIds`，
/// 经 `SidebarVS.SelectedRowIds` 投影给侧栏）。这里不再保留第二份记账——
///
/// 两份记账会漂：状态树说选了 A、B，侧栏自己那份说只选了 A，批量条与行高亮就对不上，
/// 而且没有任何一处能解释谁对。所以本类型只保留**不产生第二份事实**的部分：
/// 「可见的已选项」这种按可见行过滤的派生读法。
///
/// 「是否处于多选模式」也不再自持：它就是 `Chrome.BatchMode`，由投影给出。
type SidebarSelection() =

    /// 可见的已选项，按可见行次序。
    ///
    /// 传入的 `selected` 是**投影里的选中集合**（唯一事实），
    /// 这里只按可见行过滤与排序，不保存任何状态。
    static member SelectedIds (visible: Guid list) (selected: Set<Guid>) : Guid list =
        visible |> List.filter (fun id -> selected |> Set.contains id)
