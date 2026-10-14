namespace Wanxiang2.ThemeMetrics

/// 一项界面度量（字号、不透明度、结构尺寸、控件与内容尺寸）。名字取自旧代码，首字母大写。
[<RequireQualifiedAccess>]
type Metric =
    // ---- 旧 Design/Tokens.fs 的数值段 ----
    | FontMicro
    | FontCaption
    | FontSmall
    | FontBody
    | FontReading
    | FontTitle
    | FontHeading
    | FontDisplay
    | OpacityDisabled
    | OpacitySubtle
    | OpacityComposerDisabled
    | OpacityPressed
    | OpacityCaretBreathMin
    | OpacityCaretBreathMax
    | OpacityCopyConfirmFade
    | OpacityHoverDim
    | SkeletonOpacityBase
    | SkeletonOpacityBreathMin
    | SkeletonOpacityReduced
    | BlockPaddingX
    | BlockPaddingY
    | SidebarWidth
    | SidebarMinWidth
    | SidebarMaxWidth
    | ReadingWidth
    | BarHeight
    | IconButton
    | IconGlyph
    | IconStroke
    | ShellInset
    | LogoSplash
    | LogoRadiusRatio
    | LogoSidebar
    | LogoEmpty
    | LogoAvatar
    // ---- 旧 Design/Metrics.fs ControlMetrics ----
    | TextButtonMinHeight
    | TextButtonPaddingY
    | PendingActionMinWidth
    | TextFieldTextMinHeight
    | TextFieldPaddingY
    | FieldInsetX
    | FontSizeValueMinWidth
    | AboutKeyMinWidth
    | RowMinHeight
    | SidebarRowMinHeight
    | SettingsRowMinHeight
    | SidebarRowPaddingY
    | SidebarStatusLineHeight
    | SidebarStateSlotWidth
    | SidebarStateGlyphSize
    | SidebarRunningDotSize
    | StatusDotSize
    | ChipDotSize
    | AccentEdgeWidth
    | SidebarSelectedEdgeWidth
    | QuoteEdgeWidth
    | SidebarDividerWidth
    | BorderWidth
    | SidebarRowContentSpacing
    | ComposerInputMinHeight
    | ComposerMaxHeight
    | ComposerInputMaxHeight
    | ComposerShellPaddingX
    | ComposerShellPaddingY
    | ComposerAttachmentNameMaxWidth
    | ComposerAttachmentStateWidth
    | ComposerAttachmentIconSlot
    | ComposerModelMaxWidth
    | ComposerModelCompactMaxWidth
    | ChipPaddingX
    | ChipPaddingY
    | CompactChipPaddingX
    | CompactChipPaddingY
    | TagPaddingX
    | AttachmentRowPaddingY
    | AttachmentNameMaxWidth
    | MenuItemMinHeight
    | MenuItemPaddingY
    | MenuIconSlotWidth
    | MenuRightSlotMinWidth
    | ToastAccentWidth
    | SettingsNavWidth
    | SettingsNavMinHeight
    | SettingsContentMaxWidth
    | SelectButtonPaddingY
    | TextAreaMinHeight
    | TextAreaLongMinHeight
    | SpinnerSize
    | SpinnerCompactSize
    | SpinnerStrokeWidth
    | SpinnerCanvasSize
    | ToggleTrackWidth
    | ToggleTrackHeight
    | ToggleKnobSize
    | ToggleKnobInsetX
    | EmptyStateActionMinWidth
    | EmptyStateActionHeight
    | EmptyStateTitleMinWidth
    | ScrollToBottomMinWidth
    | RetryButtonMinWidth
    | CaretWidth
    | CaretHeight
    | CaretRadius
    | ListMarkerDotSize
    | ListMarkerDotRadius
    | TaskBoxSize
    | TaskBoxBorderWidth
    | TaskCheckGlyphSize
    | SkeletonAvatarSize
    // ---- 旧 Design/Metrics.fs ContentMetrics ----
    | MessageGap
    | MessageEndBreathing
    | ScrollBottomThreshold
    | ScrollBottomRevealThreshold
    | EmptyStateMaxWidth
    | ToastMaxWidth

/// 度量的初始值：照抄旧代码，间距引用已换成 SpacingRamp 的数值。只有数据；活的可变值归主题管理者。
module ThemeMetrics =

    /// 全部度量，按旧代码顺序
    let all : Metric list =
        [ Metric.FontMicro
          Metric.FontCaption
          Metric.FontSmall
          Metric.FontBody
          Metric.FontReading
          Metric.FontTitle
          Metric.FontHeading
          Metric.FontDisplay
          Metric.OpacityDisabled
          Metric.OpacitySubtle
          Metric.OpacityComposerDisabled
          Metric.OpacityPressed
          Metric.OpacityCaretBreathMin
          Metric.OpacityCaretBreathMax
          Metric.OpacityCopyConfirmFade
          Metric.OpacityHoverDim
          Metric.SkeletonOpacityBase
          Metric.SkeletonOpacityBreathMin
          Metric.SkeletonOpacityReduced
          Metric.BlockPaddingX
          Metric.BlockPaddingY
          Metric.SidebarWidth
          Metric.SidebarMinWidth
          Metric.SidebarMaxWidth
          Metric.ReadingWidth
          Metric.BarHeight
          Metric.IconButton
          Metric.IconGlyph
          Metric.IconStroke
          Metric.ShellInset
          Metric.LogoSplash
          Metric.LogoRadiusRatio
          Metric.LogoSidebar
          Metric.LogoEmpty
          Metric.LogoAvatar
          Metric.TextButtonMinHeight
          Metric.TextButtonPaddingY
          Metric.PendingActionMinWidth
          Metric.TextFieldTextMinHeight
          Metric.TextFieldPaddingY
          Metric.FieldInsetX
          Metric.FontSizeValueMinWidth
          Metric.AboutKeyMinWidth
          Metric.RowMinHeight
          Metric.SidebarRowMinHeight
          Metric.SettingsRowMinHeight
          Metric.SidebarRowPaddingY
          Metric.SidebarStatusLineHeight
          Metric.SidebarStateSlotWidth
          Metric.SidebarStateGlyphSize
          Metric.SidebarRunningDotSize
          Metric.StatusDotSize
          Metric.ChipDotSize
          Metric.AccentEdgeWidth
          Metric.SidebarSelectedEdgeWidth
          Metric.QuoteEdgeWidth
          Metric.SidebarDividerWidth
          Metric.BorderWidth
          Metric.SidebarRowContentSpacing
          Metric.ComposerInputMinHeight
          Metric.ComposerMaxHeight
          Metric.ComposerInputMaxHeight
          Metric.ComposerShellPaddingX
          Metric.ComposerShellPaddingY
          Metric.ComposerAttachmentNameMaxWidth
          Metric.ComposerAttachmentStateWidth
          Metric.ComposerAttachmentIconSlot
          Metric.ComposerModelMaxWidth
          Metric.ComposerModelCompactMaxWidth
          Metric.ChipPaddingX
          Metric.ChipPaddingY
          Metric.CompactChipPaddingX
          Metric.CompactChipPaddingY
          Metric.TagPaddingX
          Metric.AttachmentRowPaddingY
          Metric.AttachmentNameMaxWidth
          Metric.MenuItemMinHeight
          Metric.MenuItemPaddingY
          Metric.MenuIconSlotWidth
          Metric.MenuRightSlotMinWidth
          Metric.ToastAccentWidth
          Metric.SettingsNavWidth
          Metric.SettingsNavMinHeight
          Metric.SettingsContentMaxWidth
          Metric.SelectButtonPaddingY
          Metric.TextAreaMinHeight
          Metric.TextAreaLongMinHeight
          Metric.SpinnerSize
          Metric.SpinnerCompactSize
          Metric.SpinnerStrokeWidth
          Metric.SpinnerCanvasSize
          Metric.ToggleTrackWidth
          Metric.ToggleTrackHeight
          Metric.ToggleKnobSize
          Metric.ToggleKnobInsetX
          Metric.EmptyStateActionMinWidth
          Metric.EmptyStateActionHeight
          Metric.EmptyStateTitleMinWidth
          Metric.ScrollToBottomMinWidth
          Metric.RetryButtonMinWidth
          Metric.CaretWidth
          Metric.CaretHeight
          Metric.CaretRadius
          Metric.ListMarkerDotSize
          Metric.ListMarkerDotRadius
          Metric.TaskBoxSize
          Metric.TaskBoxBorderWidth
          Metric.TaskCheckGlyphSize
          Metric.SkeletonAvatarSize
          Metric.MessageGap
          Metric.MessageEndBreathing
          Metric.ScrollBottomThreshold
          Metric.ScrollBottomRevealThreshold
          Metric.EmptyStateMaxWidth
          Metric.ToastMaxWidth ]

    /// 某项度量的初始值
    let initial (metric: Metric) : float =
        match metric with
        | Metric.FontMicro -> 10.5
        | Metric.FontCaption -> 11.5
        | Metric.FontSmall -> 12.5
        | Metric.FontBody -> 13.5
        | Metric.FontReading -> 14.5
        | Metric.FontTitle -> 16.0
        | Metric.FontHeading -> 19.0
        | Metric.FontDisplay -> 25.0
        | Metric.OpacityDisabled -> 0.5
        | Metric.OpacitySubtle -> 0.62
        | Metric.OpacityComposerDisabled -> 0.72
        | Metric.OpacityPressed -> 0.78
        | Metric.OpacityCaretBreathMin -> 0.55
        | Metric.OpacityCaretBreathMax -> 1.0
        | Metric.OpacityCopyConfirmFade -> 0.6
        | Metric.OpacityHoverDim -> 0.9
        | Metric.SkeletonOpacityBase -> 0.65
        | Metric.SkeletonOpacityBreathMin -> 0.55
        | Metric.SkeletonOpacityReduced -> 0.85
        | Metric.BlockPaddingX -> 12.0
        | Metric.BlockPaddingY -> 8.0
        | Metric.SidebarWidth -> 284.0
        | Metric.SidebarMinWidth -> 232.0
        | Metric.SidebarMaxWidth -> 420.0
        | Metric.ReadingWidth -> 748.0
        | Metric.BarHeight -> 52.0
        | Metric.IconButton -> 30.0
        | Metric.IconGlyph -> 16.0
        | Metric.IconStroke -> 1.6
        | Metric.ShellInset -> 16.0
        | Metric.LogoSplash -> 76.0
        | Metric.LogoRadiusRatio -> 0.2894736842105263
        | Metric.LogoSidebar -> 26.0
        | Metric.LogoEmpty -> 68.0
        | Metric.LogoAvatar -> 26.0
        | Metric.TextButtonMinHeight -> 34.0
        | Metric.TextButtonPaddingY -> 6.0
        | Metric.PendingActionMinWidth -> 96.0
        | Metric.TextFieldTextMinHeight -> 22.0
        | Metric.TextFieldPaddingY -> 8.0
        | Metric.FieldInsetX -> 2.0
        | Metric.FontSizeValueMinWidth -> 56.0
        | Metric.AboutKeyMinWidth -> 112.0
        | Metric.RowMinHeight -> 44.0
        | Metric.SidebarRowMinHeight -> 44.0
        | Metric.SettingsRowMinHeight -> 44.0
        | Metric.SidebarRowPaddingY -> 6.0
        | Metric.SidebarStatusLineHeight -> 18.0
        | Metric.SidebarStateSlotWidth -> 20.0
        | Metric.SidebarStateGlyphSize -> 11.0
        | Metric.SidebarRunningDotSize -> 6.0
        | Metric.StatusDotSize -> 6.0
        | Metric.ChipDotSize -> 5.0
        | Metric.AccentEdgeWidth -> 2.0
        | Metric.SidebarSelectedEdgeWidth -> 2.0
        | Metric.QuoteEdgeWidth -> 3.0
        | Metric.SidebarDividerWidth -> 1.0
        | Metric.BorderWidth -> 1.0
        | Metric.SidebarRowContentSpacing -> 0.0
        | Metric.ComposerInputMinHeight -> 30.0
        | Metric.ComposerMaxHeight -> 240.0
        | Metric.ComposerInputMaxHeight -> 240.0
        | Metric.ComposerShellPaddingX -> 12.0
        | Metric.ComposerShellPaddingY -> 8.0
        | Metric.ComposerAttachmentNameMaxWidth -> 160.0
        | Metric.ComposerAttachmentStateWidth -> 56.0
        | Metric.ComposerAttachmentIconSlot -> 16.0
        | Metric.ComposerModelMaxWidth -> 240.0
        | Metric.ComposerModelCompactMaxWidth -> 150.0
        | Metric.ChipPaddingX -> 8.0
        | Metric.ChipPaddingY -> 2.0
        | Metric.CompactChipPaddingX -> 8.0
        | Metric.CompactChipPaddingY -> 2.0
        | Metric.TagPaddingX -> 6.0
        | Metric.AttachmentRowPaddingY -> 6.0
        | Metric.AttachmentNameMaxWidth -> 260.0
        | Metric.MenuItemMinHeight -> 34.0
        | Metric.MenuItemPaddingY -> 6.0
        | Metric.MenuIconSlotWidth -> 20.0
        | Metric.MenuRightSlotMinWidth -> 16.0
        | Metric.ToastAccentWidth -> 2.0
        | Metric.SettingsNavWidth -> 196.0
        | Metric.SettingsNavMinHeight -> 38.0
        | Metric.SettingsContentMaxWidth -> 980.0
        | Metric.SelectButtonPaddingY -> 6.0
        | Metric.TextAreaMinHeight -> 96.0
        | Metric.TextAreaLongMinHeight -> 160.0
        | Metric.SpinnerSize -> 14.0
        | Metric.SpinnerCompactSize -> 11.0
        | Metric.SpinnerStrokeWidth -> 1.8
        | Metric.SpinnerCanvasSize -> 16.0
        | Metric.ToggleTrackWidth -> 34.0
        | Metric.ToggleTrackHeight -> 20.0
        | Metric.ToggleKnobSize -> 14.0
        | Metric.ToggleKnobInsetX -> 3.0
        | Metric.EmptyStateActionMinWidth -> 136.0
        | Metric.EmptyStateActionHeight -> 38.0
        | Metric.EmptyStateTitleMinWidth -> 120.0
        | Metric.ScrollToBottomMinWidth -> 96.0
        | Metric.RetryButtonMinWidth -> 80.0
        | Metric.CaretWidth -> 7.0
        | Metric.CaretHeight -> 15.0
        | Metric.CaretRadius -> 1.5
        | Metric.ListMarkerDotSize -> 4.0
        | Metric.ListMarkerDotRadius -> 2.0
        | Metric.TaskBoxSize -> 14.0
        | Metric.TaskBoxBorderWidth -> 1.4
        | Metric.TaskCheckGlyphSize -> 10.0
        | Metric.SkeletonAvatarSize -> 28.0
        | Metric.MessageGap -> 24.0
        | Metric.MessageEndBreathing -> 48.0
        | Metric.ScrollBottomThreshold -> 48.0
        | Metric.ScrollBottomRevealThreshold -> 120.0
        | Metric.EmptyStateMaxWidth -> 480.0
        | Metric.ToastMaxWidth -> 560.0

    /// 正文与等宽字体（旧 Tokens.fontFamily / monoFontFamily 的资源地址）
    let fontFamily = "avares://Wanxiang.UI/Assets/Fonts/#Sarasa Gothic SC"
    let monoFontFamily = "avares://Wanxiang.UI/Assets/Fonts/#Sarasa Term SC, Sarasa Gothic SC"
