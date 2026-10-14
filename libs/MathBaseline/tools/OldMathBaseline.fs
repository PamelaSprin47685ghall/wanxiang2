namespace Wanxiang.UI

/// 旧 MathVisual 的 Metrics 与 Baseline/Render 里的基线选择照抄（盒子换成 (width, above, below) option）
module MathBaseline =
    let metrics (box: (float * float * float) option) =
        match box with
        | None -> 0.0, 0.0, 0.0
        | Some(width, above, below) -> width, above + below, above
    let drawBaseline (displayMode: bool) (ownBaseline: float) (hostBaseline: float option) (boundsY: float) =
        if displayMode then ownBaseline
        else
            match hostBaseline with
            | Some hostBaseline -> hostBaseline - boundsY
            | None -> ownBaseline
