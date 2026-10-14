namespace Wanxiang.UI

/// 行内「更多操作」键的呈现
type RowMoreButton = { visible: bool; fullOpacity: bool; interactive: bool }

/// 旧 Views/Sidebar.fs ApplyRowState（无摘要时的兜底分支）与 ApplyRowActionVisibility 的判定
module SidebarRowState =

    /// 当前对话或多选中的行高亮（底色、左缘、读屏状态共用）
    let highlighted (isActive: bool) (isSelected: bool) = isActive || isSelected

    /// 读屏条目状态
    let itemStatus (highlighted: bool) = if highlighted then "当前对话" else ""

    /// 多选模式下整批下线（不可见、不可点、不可聚焦、读屏不见）；否则常驻，悬停或聚焦时全不透明
    let moreButton (selectionActive: bool) (emphasized: bool) =
        if selectionActive then { visible = false; fullOpacity = false; interactive = false }
        else { visible = true; fullOpacity = emphasized; interactive = true }
