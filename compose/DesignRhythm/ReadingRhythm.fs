namespace Wanxiang.UI

/// 精品阶段的“语义尺寸”，只定义设计节奏，不承担布局。
/// Avalonia 仍负责测量、换行、排列、滚动和虚拟化；这里避免不同 View
/// 为同一语义各自发明 17 / 18 / 19 之类的近似值。
module ReadingRhythm =

    /// 长文正文：适合 CJK + 拉丁混排的持续阅读节奏。
    let proseLineHeight (fontSize: float) = fontSize * 1.62

    /// 思考过程与次级长文本略紧于正文，保持“辅助材料”层级。
    let secondaryLineHeight (fontSize: float) = fontSize * 1.56

    /// Mono / 工具详情强调扫描效率，不应像正文一样松。
    let technicalLineHeight (fontSize: float) = fontSize * 1.52

    let uiBodyLineHeight = 20.0
    let captionLineHeight = 17.0
    let validationLineHeight = 16.0
    let helperLineHeight = 18.0
    let emptyStateLineHeight = 21.0
    let headingLineHeight = 26.0

    /// Markdown 标题字号也属于阅读节奏，而不是组件临时放大的视觉特效。
    let headingFontSize (baseSize: float) (level: int) =
        baseSize
        + match level with
          | 1 -> 10.0
          | 2 -> 6.5
          | 3 -> 4.0
          | 4 -> 2.0
          | _ -> 1.0

    let paragraphGap = Spacing.spaceMd
    let listItemGap = Spacing.spaceXs
    let listBlockGap = Spacing.spaceMd
    let blockGap = Spacing.spaceMd

    let headingBefore (level: int) =
        match level with
        | 1 -> Spacing.space4xl
        | 2 -> Spacing.space3xl
        | 3 -> Spacing.space2xl
        | _ -> Spacing.spaceXl

    let headingAfter (level: int) =
        if level <= 2 then Spacing.spaceMd else Spacing.spaceXs

    let listIndentStep = Spacing.space2xl
    let listMarkerWidth = Spacing.space3xl
