namespace Wanxiang2.MessageCardView

open System
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch
open Wanxiang2.IconSet
open Wanxiang2.CopyButton
open Wanxiang2.MarkdownView
open Wanxiang2.ReadingLook
open Wanxiang2.RevealPane
open Wanxiang2.FoldHeader
open Wanxiang2.BusyRing
open Wanxiang2.ThinkingBlock
open Wanxiang2.ToolCallCard
open Wanxiang2.AttachmentChip
open Wanxiang2.ActionStrip
open Wanxiang2.ErrorPanel
open Wanxiang2.UserBubble
open Wanxiang2.StreamCaret
open Wanxiang2.UsageFoot

/// 消息卡用到的全部外观，都由主题管理者的活实例填写
type CardLooks =
    { Markdown: MarkdownLook
      Copy: CopyButtonLook
      Reveal: RevealPaneLook
      Fold: FoldHeaderLook
      Busy: BusyRingLook
      Thinking: ThinkingBlockLook
      Tool: ToolCallCardLook
      Chip: AttachmentChipLook
      Strip: ActionStripLook
      Error: ErrorPanelLook
      Bubble: UserBubbleLook
      Caret: StreamCaretLook
      Foot: UsageFootLook
      /// 组合者自己用的笔刷、度量、图标与表面反馈
      Brush: PaletteKey -> IBrush
      Metric: Metric -> IObservable<float>
      Space: Space -> IObservable<float>
      Timing: Timing -> IObservable<float>
      Icon: Glyph -> IBrush -> Control
      /// 图标按钮的样式类（旧 Ui.iconButton）
      DressIcon: Border -> unit
      Reduced: IObservable<bool> }

module CardLooks =

    /// 布局规则是常量（DesignRhythm），包成活值交给控件
    let private fixedValue (v: float) = LiveValue v :> IObservable<float>

    /// 用主题管理者与样式填全部外观
    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : CardLooks =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush
        let metric m = manager.Metric m :> IObservable<float>
        let space s = manager.Space s :> IObservable<float>
        let timing t = manager.Timing t :> IObservable<float>
        let span t = Live.map (fun (v: float) -> TimeSpan.FromMilliseconds v) (manager.Timing t)
        let icon g b = IconSet.image g b (manager.Metric Metric.IconGlyph)
        let reduced = ReadingLook.reduced manager
        let surface variant (b: Border) = styles.Surface(b, variant)
        let iconButton (b: Border) =
            surface StyleClass.hoverPlain b
            b.Classes.Add StyleClass.iconButton
        let textButton (cls: string) (b: Border) =
            surface StyleClass.hoverPlain b
            b.Classes.Add cls
        let bordered = surface StyleClass.hoverBordered
        let font = manager.FontFamily :> IObservable<FontFamily>
        let mono = manager.MonoFontFamily :> IObservable<FontFamily>
        let busy: BusyRingLook =
            { Ink = (function BusyInk.Track -> brush PaletteKey.Border | BusyInk.Arc -> brush PaletteKey.Accent)
              StrokeWidth = metric Metric.SpinnerStrokeWidth
              Frame = span Timing.BusySpinnerFrame
              Reduced = reduced }
        // 转圈尺寸是构造参数：取当前值
        let ring (m: Metric) = BusyRing(busy, (manager.Metric m).Value).Host
        { Markdown = ReadingLook.markdown manager styles
          Copy = ReadingLook.copy manager
          Reveal =
            { Gauge =
                function
                | RevealGauge.Gap -> space Space.Xs
                | RevealGauge.Gutter -> fixedValue Wanxiang.UI.LayoutPolicy.nestedScrollGutter
              Dress = textButton StyleClass.buttonGhost }
          Fold =
            { Muted = brush PaletteKey.TextMuted
              Gauge =
                function
                | FoldGauge.IconGlyph -> metric Metric.IconGlyph
                | FoldGauge.RowGap -> space Space.Md
                | FoldGauge.PadX -> space Space.Xs
                | FoldGauge.PadY -> space Space.Sm
                | FoldGauge.Radius -> space Space.RadiusSm
                | FoldGauge.BorderWidth -> metric Metric.BorderWidth
                | FoldGauge.CaptionSize -> metric Metric.FontCaption
                | FoldGauge.CaptionLineHeight -> fixedValue Wanxiang.UI.ReadingRhythm.captionLineHeight
                | FoldGauge.RotateMs -> timing Timing.DisclosureChevronRotate
              Chevron = icon Glyph.ChevronRight
              Dress = bordered
              Reduced = reduced }
          Busy = busy
          Thinking =
            { Ink =
                function
                | ThinkingInk.Surface -> brush PaletteKey.SurfaceContainer
                | ThinkingInk.Hairline -> brush PaletteKey.Hairline
                | ThinkingInk.Edge -> brush PaletteKey.HairlineStrong
                | ThinkingInk.Body -> brush PaletteKey.TextMuted
                | ThinkingInk.Selection -> brush PaletteKey.AccentSoft
              Gauge =
                function
                | ThinkingGauge.BorderWidth -> metric Metric.BorderWidth
                | ThinkingGauge.EdgeWidth -> metric Metric.AccentEdgeWidth
                | ThinkingGauge.PadTight -> space Space.Xs
                | ThinkingGauge.PadLoose -> space Space.Md
                | ThinkingGauge.After -> space Space.Xl
                | ThinkingGauge.Radius -> space Space.RadiusMd
              LineHeight = Wanxiang.UI.ReadingRhythm.secondaryLineHeight }
          Tool =
            { Ink =
                function
                | ToolInk.Text -> brush PaletteKey.Text
                | ToolInk.TextMuted -> brush PaletteKey.TextMuted
                | ToolInk.Accent -> brush PaletteKey.Accent
                | ToolInk.Warning -> brush PaletteKey.Warning
                | ToolInk.Success -> brush PaletteKey.Success
                | ToolInk.Danger -> brush PaletteKey.Danger
                | ToolInk.DangerSoft -> brush PaletteKey.DangerSoft
                | ToolInk.SurfaceSoft -> brush PaletteKey.SurfaceSoft
                | ToolInk.SurfaceContainer -> brush PaletteKey.SurfaceContainer
                | ToolInk.BorderSoft -> brush PaletteKey.BorderSoft
                | ToolInk.Hairline -> brush PaletteKey.Hairline
                | ToolInk.CodeBg -> brush PaletteKey.CodeBg
                | ToolInk.CodeBorder -> brush PaletteKey.CodeBorder
                | ToolInk.CodeText -> brush PaletteKey.CodeText
                | ToolInk.Selection -> brush PaletteKey.AccentSoft
              Gauge =
                function
                | ToolGauge.BorderWidth -> metric Metric.BorderWidth
                | ToolGauge.SpaceXXs -> space Space.XXs
                | ToolGauge.SpaceXs -> space Space.Xs
                | ToolGauge.SpaceMd -> space Space.Md
                | ToolGauge.SpaceXl -> space Space.Xl
                | ToolGauge.RadiusSm -> space Space.RadiusSm
                | ToolGauge.RadiusMd -> space Space.RadiusMd
                | ToolGauge.RadiusPill -> space Space.RadiusPill
                | ToolGauge.FontMicro -> metric Metric.FontMicro
                | ToolGauge.FontCaption -> metric Metric.FontCaption
                | ToolGauge.FontSmall -> metric Metric.FontSmall
                | ToolGauge.IconGlyph -> metric Metric.IconGlyph
                | ToolGauge.RotateMs -> timing Timing.DisclosureChevronRotate
              BodyFont = font
              MonoFont = mono
              Glyph =
                fun g b ->
                    match g with
                    | ToolGlyph.Spinner -> ring Metric.SpinnerSize
                    | ToolGlyph.SpinnerCompact -> ring Metric.SpinnerCompactSize
                    | ToolGlyph.Wrench -> icon Glyph.Wrench b
                    | ToolGlyph.Alert -> icon Glyph.Alert b
                    | ToolGlyph.Info -> icon Glyph.Info b
                    | ToolGlyph.Check -> icon Glyph.Check b
                    | ToolGlyph.Chevron -> icon Glyph.ChevronRight b
              Dress = bordered
              TechLineHeight = Wanxiang.UI.ReadingRhythm.technicalLineHeight
              Reduced = reduced }
          Chip =
            { Ink =
                function
                | ChipInk.Surface -> brush PaletteKey.Surface
                | ChipInk.Border -> brush PaletteKey.Border
                | ChipInk.Text -> brush PaletteKey.Text
                | ChipInk.TextMuted -> brush PaletteKey.TextMuted
                | ChipInk.TextFaint -> brush PaletteKey.TextFaint
              Gauge =
                function
                | ChipGauge.BorderWidth -> metric Metric.BorderWidth
                | ChipGauge.Radius -> space Space.RadiusMd
                | ChipGauge.PadX -> space Space.Xl
                | ChipGauge.PadY -> metric Metric.AttachmentRowPaddingY
                | ChipGauge.Gap -> space Space.Md
                | ChipGauge.Top -> space Space.Xs
                | ChipGauge.NameSize -> metric Metric.FontSmall
                | ChipGauge.MetaSize -> metric Metric.FontMicro
                | ChipGauge.NameMaxWidth -> metric Metric.AttachmentNameMaxWidth
              Glyph =
                fun g b ->
                    icon
                        (match g with
                         | ChipGlyph.Image -> Glyph.Image
                         | ChipGlyph.File -> Glyph.File
                         | ChipGlyph.Download -> Glyph.Download)
                        b
              Dress = surface StyleClass.hoverRaised }
          Strip =
            { Gauge =
                function
                | StripGauge.Gap -> space Space.Xs
                | StripGauge.Target -> fixedValue Wanxiang.UI.LayoutPolicy.inlineActionTarget
                | StripGauge.Subtle -> metric Metric.OpacitySubtle
                | StripGauge.FadeMs -> timing Timing.ControlRowFade
              Glyph =
                fun g b ->
                    icon
                        (match g with
                         | StripGlyph.Copy -> Glyph.Copy
                         | StripGlyph.Check -> Glyph.Check
                         | StripGlyph.Pencil -> Glyph.Pencil
                         | StripGlyph.Refresh -> Glyph.Refresh
                         | StripGlyph.Trash -> Glyph.Trash)
                        b
              Muted = brush PaletteKey.TextMuted
              Dress = iconButton
              Reduced = reduced }
          Error =
            { Ink =
                function
                | ErrorInk.Text -> brush PaletteKey.Text
                | ErrorInk.TextMuted -> brush PaletteKey.TextMuted
                | ErrorInk.Warning -> brush PaletteKey.Warning
                | ErrorInk.Success -> brush PaletteKey.Success
                | ErrorInk.Danger -> brush PaletteKey.Danger
                | ErrorInk.SurfaceContainer -> brush PaletteKey.SurfaceContainer
                | ErrorInk.Hairline -> brush PaletteKey.Hairline
                | ErrorInk.CodeBg -> brush PaletteKey.CodeBg
                | ErrorInk.CodeBorder -> brush PaletteKey.CodeBorder
                | ErrorInk.CodeMuted -> brush PaletteKey.CodeMuted
                | ErrorInk.Selection -> brush PaletteKey.AccentSoft
              Gauge =
                function
                | ErrorGauge.BorderWidth -> metric Metric.BorderWidth
                | ErrorGauge.SpaceXs -> space Space.Xs
                | ErrorGauge.SpaceSm -> space Space.Sm
                | ErrorGauge.SpaceMd -> space Space.Md
                | ErrorGauge.SpaceXl -> space Space.Xl
                | ErrorGauge.Space2xl -> space Space.X2l
                | ErrorGauge.RadiusSm -> space Space.RadiusSm
                | ErrorGauge.RadiusMd -> space Space.RadiusMd
                | ErrorGauge.FontBody -> metric Metric.FontBody
                | ErrorGauge.FontCaption -> metric Metric.FontCaption
                | ErrorGauge.FontSmall -> metric Metric.FontSmall
                | ErrorGauge.FontMicro -> metric Metric.FontMicro
                | ErrorGauge.TextButtonPadY -> metric Metric.TextButtonPaddingY
                | ErrorGauge.RetryMinWidth -> metric Metric.RetryButtonMinWidth
                | ErrorGauge.IconGlyph -> metric Metric.IconGlyph
                | ErrorGauge.RotateMs -> timing Timing.DisclosureChevronRotate
                | ErrorGauge.ReadingWidth -> metric Metric.ReadingWidth
                | ErrorGauge.TickMs -> fixedValue CountdownTicker.IntervalMs
              BodyFont = font
              MonoFont = mono
              Glyph = fun g b -> icon (match g with ErrorGlyph.Alert -> Glyph.Alert | ErrorGlyph.Chevron -> Glyph.ChevronRight) b
              Dress = fun secondary b -> textButton (if secondary then StyleClass.buttonSecondary else StyleClass.buttonGhost) b
              DressFold = bordered
              TechLineHeight = Wanxiang.UI.ReadingRhythm.technicalLineHeight
              HelperLineHeight = Wanxiang.UI.ReadingRhythm.helperLineHeight
              Reduced = reduced }
          Bubble =
            { Ink =
                function
                | BubbleInk.Fill -> brush PaletteKey.UserBubble
                | BubbleInk.Edge -> brush PaletteKey.HairlineStrong
                | BubbleInk.Text -> brush PaletteKey.UserBubbleText
                | BubbleInk.Selection -> brush PaletteKey.UserBubbleSelection
                | BubbleInk.AvatarFill -> brush PaletteKey.AccentSoft
                | BubbleInk.AvatarText -> brush PaletteKey.Accent
              Gauge =
                function
                | BubbleGauge.Stroke -> space Space.StrokeThin
                | BubbleGauge.RadiusLg -> space Space.RadiusLg
                | BubbleGauge.RadiusSm -> space Space.RadiusSm
                | BubbleGauge.RadiusPill -> space Space.RadiusPill
                | BubbleGauge.PadX -> space Space.X2l
                | BubbleGauge.PadY -> space Space.Xl
                | BubbleGauge.MaxWidth -> fixedValue Wanxiang.UI.LayoutPolicy.userMessageMaxWidth
                | BubbleGauge.Avatar -> metric Metric.LogoAvatar
                | BubbleGauge.AvatarFont -> metric Metric.FontMicro
                | BubbleGauge.Gap -> space Space.Xl
              Dress = surface StyleClass.hoverPlain
              LineHeight = Wanxiang.UI.ReadingRhythm.proseLineHeight }
          Caret =
            { Ink = brush PaletteKey.Accent
              Gauge =
                function
                | CaretGauge.Width -> metric Metric.CaretWidth
                | CaretGauge.Height -> metric Metric.CaretHeight
                | CaretGauge.Radius -> metric Metric.CaretRadius
                | CaretGauge.OpacityMax -> metric Metric.OpacityCaretBreathMax
              Frame = span Timing.CaretBreathFrame
              Reduced = reduced }
          Foot =
            { Ink =
                function
                | FootInk.Text -> brush PaletteKey.Text
                | FootInk.TextMuted -> brush PaletteKey.TextMuted
                | FootInk.TextFaint -> brush PaletteKey.TextFaint
              Gauge = (function FootGauge.Font -> metric Metric.FontMicro | FootGauge.Gap -> space Space.Xs)
              MonoFont = mono }
          Brush = brush
          Metric = metric
          Space = space
          Timing = timing
          Icon = icon
          DressIcon = iconButton
          Reduced = reduced }
