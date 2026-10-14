namespace Wanxiang2.ChatScreen

open System
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.IconSet
open Wanxiang2.ChatHeader
open Wanxiang2.ChatPane

/// 对话屏的外观：顶栏、对话区，加上组合层自己用的几样，都由主题管理者的活实例填
type ChatScreenLooks =
    { Header: ChatHeaderLook
      Pane: ChatPaneLooks
      /// 对话屏底色（旧 Tokens.canvas）
      Canvas: IBrush
      /// 定位高亮底色（旧 Tokens.accentSoft）
      Highlight: IBrush
      TextMuted: IBrush
      /// 卡片顶到视口顶的留白（旧 Spacing.space3xl）
      Space3xl: IObservable<float>
      SpaceXs: IObservable<float>
      FontCaption: IObservable<float>
      /// 消息卡里的品牌头像边长（旧 Tokens.logoAvatar）
      LogoAvatar: IObservable<float>
      IconButton: IObservable<float>
      /// 图标（菜单项与版本条）
      Icon: Glyph -> IBrush -> Control
      /// 图标按钮样式（版本条）
      DressIcon: Border -> unit }

module ChatScreenLooks =

    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : ChatScreenLooks =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush
        let metric m = manager.Metric m :> IObservable<float>
        let space s = manager.Space s :> IObservable<float>
        let icon g b = IconSet.image g b (manager.Metric Metric.IconGlyph)
        let surface variant (b: Border) = styles.Surface(b, variant)
        let dressIcon (b: Border) = surface StyleClass.hoverPlain b; b.Classes.Add StyleClass.iconButton
        { Header =
            { Ink =
                function
                | HeaderInk.Text -> brush PaletteKey.Text
                | HeaderInk.TextFaint -> brush PaletteKey.TextFaint
                | HeaderInk.TextMuted -> brush PaletteKey.TextMuted
                | HeaderInk.Accent -> brush PaletteKey.Accent
                | HeaderInk.AccentFaint -> brush PaletteKey.AccentFaint
                | HeaderInk.Hover -> brush PaletteKey.Hover
                | HeaderInk.BorderSoft -> brush PaletteKey.BorderSoft
                | HeaderInk.Border -> brush PaletteKey.Border
                | HeaderInk.Line -> brush PaletteKey.Line
                | HeaderInk.Surface -> brush PaletteKey.Surface
                | HeaderInk.Hairline -> brush PaletteKey.Hairline
              Gauge =
                function
                | HeaderGauge.IconButton -> metric Metric.IconButton
                | HeaderGauge.CompactTarget -> LiveValue LayoutPolicy.compactActionTarget :> _
                | HeaderGauge.BarHeight -> metric Metric.BarHeight
                | HeaderGauge.ShellInset -> metric Metric.ShellInset
                | HeaderGauge.SpaceXs -> space Space.Xs
                | HeaderGauge.SpaceMd -> space Space.Md
                | HeaderGauge.SpaceXl -> space Space.Xl
                | HeaderGauge.RadiusSm -> space Space.RadiusSm
                | HeaderGauge.RadiusPill -> space Space.RadiusPill
                | HeaderGauge.BorderWidth -> metric Metric.BorderWidth
                | HeaderGauge.FocusSpread -> space Space.StrokeThick
                | HeaderGauge.FontTitle -> metric Metric.FontTitle
                | HeaderGauge.FontMicro -> metric Metric.FontMicro
                | HeaderGauge.FontCaption -> metric Metric.FontCaption
                | HeaderGauge.ChipPadX -> metric Metric.ChipPaddingX
                | HeaderGauge.ChipPadY -> metric Metric.ChipPaddingY
                | HeaderGauge.ChipDot -> metric Metric.ChipDotSize
                | HeaderGauge.TitleMinWidth -> metric Metric.EmptyStateTitleMinWidth
                | HeaderGauge.IconGlyph -> metric Metric.IconGlyph
              BodyFont = manager.FontFamily :> IObservable<FontFamily>
              Glyph =
                fun g b ->
                    match g with
                    | HeaderGlyph.Sidebar -> icon Glyph.PanelLeft b
                    | HeaderGlyph.More -> icon Glyph.More b
                    | HeaderGlyph.Fork -> icon Glyph.Fork b
              DressIcon = dressIcon
              DressPlain = surface StyleClass.hoverPlain
              DressField = surface StyleClass.hoverBordered }
          Pane = ChatPaneLooks.ofTheme manager styles
          Canvas = brush PaletteKey.Canvas
          Highlight = brush PaletteKey.AccentSoft
          TextMuted = brush PaletteKey.TextMuted
          Space3xl = space Space.X3l
          SpaceXs = space Space.Xs
          FontCaption = metric Metric.FontCaption
          LogoAvatar = metric Metric.LogoAvatar
          IconButton = metric Metric.IconButton
          Icon = icon
          DressIcon = dressIcon }
