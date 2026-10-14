namespace Wanxiang.UI

open System

/// 浮层（菜单、选择器）相对锚点的落点（旧 Controls/OverlayHost.fs 的 positionPopup）。
module PopupPlacement =

    /// 默认在锚点下方隔 gap 处、左对齐（alignRight 时右对齐）；下方放不下就翻到锚点上方；
    /// 最后把左上角钳在视口内、四周留 inset。锚点坐标为其左上角在视口里的位置。
    let place (anchorX: float) (anchorY: float) (anchorWidth: float) (anchorHeight: float) (width: float) (height: float)
              (viewportWidth: float) (viewportHeight: float) (inset: float) (gap: float) (alignRight: bool) : float * float =
        let belowY = anchorY + anchorHeight + gap
        let x = if alignRight then anchorX + anchorWidth - width else anchorX
        let clampedX = Math.Clamp(x, inset, max inset (viewportWidth - width - inset))
        let flipUp = belowY + height > viewportHeight - inset
        let candidateY = if flipUp then belowY - anchorHeight - height - gap * 2.0 else belowY
        let maxY = max inset (viewportHeight - height - inset)
        clampedX, Math.Clamp(candidateY, inset, maxY)
