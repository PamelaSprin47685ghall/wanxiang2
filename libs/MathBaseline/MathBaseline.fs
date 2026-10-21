namespace Wanxiang.UI

/// 公式控件的尺寸与基线（旧 Rich/MathVisual.fs 的 Metrics、Baseline 与 Render 里的基线选择）。
module MathBaseline =

    /// 盒宽、盒高、盒内基线位置。盒子只按公式自身的墨迹范围算（above 为基线以上、below 为基线以下）；
    /// 排不出公式时全为零。
    let metrics (box: (float * float * float) option) : float * float * float =
        match box with
        | None -> 0.0, 0.0, 0.0
        | Some(width, above, below) -> width, above + below, above

    /// 绘制时基线到控件顶缘的距离：展示式用自身基线；行内式对到所在文本块的真实首行基线
    ///（hostBaseline 相对文本块顶缘，boundsY 是控件在文本块里的纵向偏移），问不到时退回自身基线。
    let drawBaseline (displayMode: bool) (ownBaseline: float) (hostBaseline: float option) (boundsY: float) : float =
        if displayMode then
            ownBaseline
        else
            match hostBaseline with
            | Some host -> host - boundsY
            | None -> ownBaseline
