namespace Wanxiang.UI

// 照抄旧 Views/Sidebar.fs ApplyRowState 兜底分支与 ApplyRowActionVisibility（控件写入换成记录字段）
type RowMoreButton = { visible: bool; fullOpacity: bool; interactive: bool }

module SidebarRowState =
    let highlighted (isActive: bool) (isSelected: bool) = isActive || (isSelected)
    let itemStatus (highlighted: bool) = if highlighted then "当前对话" else ""
    let moreButton (selectionActive: bool) (visible: bool) =
        if selectionActive then
            { visible = false; fullOpacity = false; interactive = false }
        else
            { visible = true; fullOpacity = (if visible then true else false); interactive = true }
