namespace Wanxiang2.ChatPane

open System
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch
open Wanxiang2.IconSet
open Wanxiang2.ChatScroller
open Wanxiang2.ExchangeNav
open Wanxiang2.ChatEmpty
open Wanxiang2.SkeletonList

/// 对话区四件的外观，都由主题管理者的活实例填
type ChatPaneLooks =
    { Scroller: ChatScrollerLook
      Nav: ExchangeNavLook
      Empty: ChatEmptyLook
      Skeleton: SkeletonListLook
      /// 滚动区顶部留白（旧 Spacing.space3xl）
      Space3xl: IObservable<float>
      /// 起步模式空态留白（旧 Spacing.space2xl）
      Space2xl: IObservable<float>
      /// 空态品牌标边长（旧 Tokens.logoEmpty；构造参数，取当前值）
      LogoEmpty: LiveValue<float> }

module ChatPaneLooks =

    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : ChatPaneLooks =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush
        let metric m = manager.Metric m :> IObservable<float>
        let space s = manager.Space s :> IObservable<float>
        let font = manager.FontFamily :> IObservable<FontFamily>
        let span t = Live.map (fun (v: float) -> TimeSpan.FromMilliseconds v) (manager.Timing t)
        let surface variant (b: Avalonia.Controls.Border) = styles.Surface(b, variant)
        let reduced =
            let v = LiveValue(manager.Motion.IsReduced)
            manager.Motion.Changed.Add(fun _ -> v.Set manager.Motion.IsReduced)
            v :> IObservable<bool>
        let tracking = LiveValue(Spacing.Tracking.emphasis) :> IObservable<float>
        { Scroller =
            { Ink =
                function
                | ScrollerInk.Accent -> brush PaletteKey.Accent
                | ScrollerInk.OnAccent -> brush PaletteKey.TextOnAccent
              Gauge =
                function
                | ScrollerGauge.BottomThreshold -> metric Metric.ScrollBottomThreshold
                | ScrollerGauge.RevealThreshold -> metric Metric.ScrollBottomRevealThreshold
                | ScrollerGauge.IconButton -> metric Metric.IconButton
                | ScrollerGauge.CompactTarget -> LiveValue LayoutPolicy.compactActionTarget :> IObservable<float>
                | ScrollerGauge.WideMinWidth -> metric Metric.ScrollToBottomMinWidth
                | ScrollerGauge.FontSmall -> metric Metric.FontSmall
                | ScrollerGauge.SpaceMd -> space Space.Md
                | ScrollerGauge.SpaceXl -> space Space.Xl
                | ScrollerGauge.Space2xl -> space Space.X2l
                | ScrollerGauge.Space3xl -> space Space.X3l
              Glyph = (fun _ b -> IconSet.image Glyph.ArrowDown b (manager.Metric Metric.IconGlyph))
              Frame = span Timing.SmoothScrollFrame
              Reduced = reduced
              DressLatest = (fun b -> surface StyleClass.hoverAccent b; b.Classes.Add StyleClass.iconButtonAccent)
              Shadow = manager.ShadowPopup }
          Nav =
            { Ink =
                function
                | NavInk.Text -> brush PaletteKey.Text
                | NavInk.SurfaceRaised -> brush PaletteKey.SurfaceRaised
                | NavInk.Warning -> brush PaletteKey.Warning
              Gauge =
                function
                | NavGauge.SpaceMd -> space Space.Md
                | NavGauge.SpaceXl -> space Space.Xl
                | NavGauge.Space2xl -> space Space.X2l
                | NavGauge.RadiusLg -> space Space.RadiusLg
                | NavGauge.BorderWidth -> metric Metric.BorderWidth
                | NavGauge.FontSmall -> metric Metric.FontSmall
                | NavGauge.ReadingWidth -> metric Metric.ReadingWidth
                | NavGauge.TextButtonPadY -> metric Metric.TextButtonPaddingY
                | NavGauge.TextButtonMinHeight -> metric Metric.TextButtonMinHeight
              BodyFont = font
              DressRetry = (fun b -> surface StyleClass.hoverBordered b; b.Classes.Add StyleClass.buttonSecondary)
              Shadow = manager.ShadowPopup }
          Empty =
            { Ink =
                function
                | EmptyInk.Text -> brush PaletteKey.Text
                | EmptyInk.TextMuted -> brush PaletteKey.TextMuted
                | EmptyInk.SurfaceContainer -> brush PaletteKey.SurfaceContainer
                | EmptyInk.Hairline -> brush PaletteKey.Hairline
                | EmptyInk.Accent -> brush PaletteKey.Accent
                | EmptyInk.TextOnAccent -> brush PaletteKey.TextOnAccent
                | EmptyInk.Surface -> brush PaletteKey.Surface
                | EmptyInk.Border -> brush PaletteKey.Border
              Gauge =
                function
                | EmptyGauge.SpaceXs -> space Space.Xs
                | EmptyGauge.SpaceMd -> space Space.Md
                | EmptyGauge.SpaceXl -> space Space.Xl
                | EmptyGauge.Space2xl -> space Space.X2l
                | EmptyGauge.Space4xl -> space Space.X4l
                | EmptyGauge.Space6xl -> space Space.X6l
                | EmptyGauge.RadiusMd -> space Space.RadiusMd
                | EmptyGauge.RadiusLg -> space Space.RadiusLg
                | EmptyGauge.BorderWidth -> metric Metric.BorderWidth
                | EmptyGauge.FontDisplay -> metric Metric.FontDisplay
                | EmptyGauge.FontTitle -> metric Metric.FontTitle
                | EmptyGauge.FontBody -> metric Metric.FontBody
                | EmptyGauge.FontCaption -> metric Metric.FontCaption
                | EmptyGauge.FontSmall -> metric Metric.FontSmall
                | EmptyGauge.TextButtonPadY -> metric Metric.TextButtonPaddingY
                | EmptyGauge.TextButtonMinHeight -> metric Metric.TextButtonMinHeight
                | EmptyGauge.ActionMinWidth -> metric Metric.EmptyStateActionMinWidth
                | EmptyGauge.ActionHeight -> metric Metric.EmptyStateActionHeight
                | EmptyGauge.MaxWidth -> metric Metric.EmptyStateMaxWidth
                | EmptyGauge.TrackingEmphasis -> tracking
                | EmptyGauge.PromptLineHeight -> LiveValue ReadingRhythm.uiBodyLineHeight :> IObservable<float>
                | EmptyGauge.CaptionLineHeight -> LiveValue ReadingRhythm.captionLineHeight :> IObservable<float>
              BodyFont = font
              DressPrimary = (fun b -> surface StyleClass.hoverAccent b; b.Classes.Add StyleClass.buttonPrimary)
              DressSecondary = (fun b -> surface StyleClass.hoverBordered b; b.Classes.Add StyleClass.buttonSecondary)
              DressCard = (fun b -> b.Classes.Add StyleClass.groupingCard) }
          Skeleton =
            { Ink =
                function
                | SkeletonInk.Bar -> brush PaletteKey.BorderSoft
                | SkeletonInk.Edge -> brush PaletteKey.HairlineStrong
              Gauge =
                function
                | SkeletonGauge.Gap -> metric Metric.MessageGap
                | SkeletonGauge.EndBreathing -> metric Metric.MessageEndBreathing
                | SkeletonGauge.ReadingWidth -> metric Metric.ReadingWidth
                | SkeletonGauge.AvatarSize -> metric Metric.SkeletonAvatarSize
                | SkeletonGauge.RadiusSm -> space Space.RadiusSm
                | SkeletonGauge.RadiusLg -> space Space.RadiusLg
                | SkeletonGauge.RadiusPill -> space Space.RadiusPill
                | SkeletonGauge.SpaceMd -> space Space.Md
                | SkeletonGauge.SpaceXl -> space Space.Xl
                | SkeletonGauge.BorderWidth -> metric Metric.BorderWidth
                | SkeletonGauge.OpacityBase -> metric Metric.SkeletonOpacityBase
                | SkeletonGauge.OpacityBreathMin -> metric Metric.SkeletonOpacityBreathMin
                | SkeletonGauge.OpacityReduced -> metric Metric.SkeletonOpacityReduced
              Frame = span Timing.SkeletonBreathFrame
              Reduced = reduced }
          Space2xl = space Space.X2l
          Space3xl = space Space.X3l
          LogoEmpty = manager.Metric Metric.LogoEmpty }
