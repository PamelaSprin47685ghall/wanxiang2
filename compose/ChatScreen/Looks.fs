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
type ChatScreenMetrics =
    { Pane: ChatPaneMetrics
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

module ChatScreenMetrics =

    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : ChatScreenMetrics =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush
        let metric m = manager.Metric m :> IObservable<float>
        let space s = manager.Space s :> IObservable<float>
        let icon g b = IconSet.image g b (manager.Metric Metric.IconGlyph)
        let surface variant (b: Border) = styles.Surface(b, variant)
        let dressIcon (b: Border) = surface StyleClass.hoverPlain b; b.Classes.Add StyleClass.iconButton
        {
          Pane = ChatPaneMetrics.ofTheme manager styles
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
