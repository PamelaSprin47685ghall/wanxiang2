namespace Wanxiang.UI

open System

/// 旧 OverlayHost.positionPopup 的坐标计算照抄（控件量换成参数）
module PopupPlacement =
    let place (anchorX: float) (anchorY: float) (anchorWidth: float) (anchorHeight: float) (width: float) (height: float)
              (viewportWidth: float) (viewportHeight: float) (viewportInset: float) (popupGap: float) (popupAlignRight: bool) =
        let p = {| X = anchorX; Y = anchorY + anchorHeight + popupGap |}
        let x = if popupAlignRight then p.X + anchorWidth - width else p.X
        let clampedX = Math.Clamp(x, viewportInset, max viewportInset (viewportWidth - width - viewportInset))
        let flipUp = p.Y + height > viewportHeight - viewportInset
        let candidateY =
            if flipUp then p.Y - anchorHeight - height - popupGap * 2.0 else p.Y
        let maxY = max viewportInset (viewportHeight - height - viewportInset)
        let clampedY = Math.Clamp(candidateY, viewportInset, maxY)
        clampedX, clampedY
