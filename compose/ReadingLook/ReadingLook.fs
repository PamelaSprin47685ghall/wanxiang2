namespace Wanxiang2.ReadingLook

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
open Wanxiang2.MathFigure
open Wanxiang2.MarkdownView

/// 正文与公式的外观由主题管理者的活实例填写（旧 Tokens / ControlMetrics / Spacing / MotionLedger 的同名值）。
/// 共享外观（图标按钮、幽灵按钮、表面状态反馈）走 ThemeStyles 安装的样式类。
module ReadingLook =

    let private ofMetric (manager: ThemeManager) m = manager.Metric m :> IObservable<float>
    let private ofSpace (manager: ThemeManager) s = manager.Space s :> IObservable<float>

    let private ink (manager: ThemeManager) (slot: MarkdownInk) : IBrush =
        let key =
            match slot with
            | MarkdownInk.Text -> PaletteKey.Text
            | MarkdownInk.TextMuted -> PaletteKey.TextMuted
            | MarkdownInk.TextFaint -> PaletteKey.TextFaint
            | MarkdownInk.TextOnAccent -> PaletteKey.TextOnAccent
            | MarkdownInk.Accent -> PaletteKey.Accent
            | MarkdownInk.AccentSoft -> PaletteKey.AccentSoft
            | MarkdownInk.Success -> PaletteKey.Success
            | MarkdownInk.Line -> PaletteKey.Line
            | MarkdownInk.BorderSoft -> PaletteKey.BorderSoft
            | MarkdownInk.Hairline -> PaletteKey.Hairline
            | MarkdownInk.HairlineStrong -> PaletteKey.HairlineStrong
            | MarkdownInk.SurfaceContainer -> PaletteKey.SurfaceContainer
            | MarkdownInk.InlineCode -> PaletteKey.InlineCode
            | MarkdownInk.TableHeader -> PaletteKey.TableHeader
            | MarkdownInk.TableStripe -> PaletteKey.TableStripe
            | MarkdownInk.CodeBg -> PaletteKey.CodeBg
            | MarkdownInk.CodeHeaderBg -> PaletteKey.CodeHeaderBg
            | MarkdownInk.CodeBorder -> PaletteKey.CodeBorder
            | MarkdownInk.CodeText -> PaletteKey.CodeText
            | MarkdownInk.CodeMuted -> PaletteKey.CodeMuted
            | MarkdownInk.CodeKeyword -> PaletteKey.CodeKeyword
            | MarkdownInk.CodeString -> PaletteKey.CodeString
            | MarkdownInk.CodeComment -> PaletteKey.CodeComment
            | MarkdownInk.CodeNumber -> PaletteKey.CodeNumber
            | MarkdownInk.CodeType -> PaletteKey.CodeType
            | MarkdownInk.CodeFunction -> PaletteKey.CodeFunction
            | MarkdownInk.CodeMeta -> PaletteKey.CodeMeta
            | MarkdownInk.CodeVariable -> PaletteKey.CodeVariable
            | MarkdownInk.CodeOperator -> PaletteKey.CodeOperator
            | MarkdownInk.CodeAddition -> PaletteKey.CodeAddition
            | MarkdownInk.CodeDeletion -> PaletteKey.CodeDeletion
        manager.Brush key :> IBrush

    /// 字距档没有对应的度量项：取 SpacingRamp 的强调档作为本外观自己的活值
    let private tracking = LiveValue(Spacing.Tracking.emphasis)

    let private gauge (manager: ThemeManager) (slot: MarkdownGauge) : IObservable<float> =
        match slot with
        | MarkdownGauge.FontMicro -> ofMetric manager Metric.FontMicro
        | MarkdownGauge.BorderWidth -> ofMetric manager Metric.BorderWidth
        | MarkdownGauge.QuoteEdgeWidth -> ofMetric manager Metric.QuoteEdgeWidth
        | MarkdownGauge.BlockPaddingX -> ofMetric manager Metric.BlockPaddingX
        | MarkdownGauge.BlockPaddingY -> ofMetric manager Metric.BlockPaddingY
        | MarkdownGauge.SpaceXXs -> ofSpace manager Space.XXs
        | MarkdownGauge.SpaceXs -> ofSpace manager Space.Xs
        | MarkdownGauge.SpaceSm -> ofSpace manager Space.Sm
        | MarkdownGauge.SpaceMd -> ofSpace manager Space.Md
        | MarkdownGauge.RadiusXs -> ofSpace manager Space.RadiusXs
        | MarkdownGauge.RadiusSm -> ofSpace manager Space.RadiusSm
        | MarkdownGauge.RadiusMd -> ofSpace manager Space.RadiusMd
        | MarkdownGauge.ListMarkerDotSize -> ofMetric manager Metric.ListMarkerDotSize
        | MarkdownGauge.ListMarkerDotRadius -> ofMetric manager Metric.ListMarkerDotRadius
        | MarkdownGauge.TaskBoxSize -> ofMetric manager Metric.TaskBoxSize
        | MarkdownGauge.TaskBoxBorderWidth -> ofMetric manager Metric.TaskBoxBorderWidth
        | MarkdownGauge.TaskCheckGlyphSize -> ofMetric manager Metric.TaskCheckGlyphSize
        | MarkdownGauge.TrackingEmphasis -> tracking :> IObservable<float>
        | MarkdownGauge.CopyFadeMs -> manager.Timing Timing.CopyConfirmationFade :> IObservable<float>

    /// 减弱动效开关的活值（MotionSwitch 实例改了就推送）
    let reduced (manager: ThemeManager) : IObservable<bool> =
        let v = LiveValue(manager.Motion.IsReduced)
        manager.Motion.Changed.Add(fun _ -> v.Set manager.Motion.IsReduced)
        v :> IObservable<bool>

    /// 公式外观
    let math (manager: ThemeManager) : MathFigureMetrics =
        { Ink = manager.Brush PaletteKey.Text
          BodyFont = manager.FontFamily
          BlockPadding = ofSpace manager Space.Md }

    /// 复制确认外观
    let copy (manager: ThemeManager) : CopyButtonAppearance =
        let ms t = Live.map (fun (v: float) -> TimeSpan.FromMilliseconds v) (manager.Timing t)
        { FadeOpacity = ofMetric manager Metric.OpacityCopyConfirmFade
          Hold = ms Timing.CopyConfirmationHold
          Fade = ms Timing.CopyConfirmationFade }

    /// Markdown 正文外观
    let markdown (manager: ThemeManager) (styles: ThemeStyles) : MarkdownMetrics =
        { Ink = ink manager
          Gauge = gauge manager
          BodyFont = manager.FontFamily
          MonoFont = manager.MonoFontFamily
          Glyph =
            fun glyph brush ->
                let g =
                    match glyph with
                    | MarkdownGlyph.Copy -> Glyph.Copy
                    | MarkdownGlyph.Check -> Glyph.Check
                    | MarkdownGlyph.TextWrap -> Glyph.TextWrap
                IconSet.image g brush (manager.Metric Metric.IconGlyph)
          Dress =
            fun role (button: Button) ->
                styles.Surface(button, StyleClass.hoverPlain)
                button.Classes.Add(
                    match role with
                    | MarkdownButton.Icon -> StyleClass.iconButton
                    | MarkdownButton.Ghost -> StyleClass.buttonGhost)
          Reduced = reduced manager
          Copy = copy manager
          Math = math manager }
