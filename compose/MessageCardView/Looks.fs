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

/// 消息卡用到的全部外观，都由主题管理者的活实例填写
type CardAppearance =
    { Markdown: MarkdownMetrics
      Copy: CopyButtonAppearance
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

    /// 用主题管理者与样式填全部外观
    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : CardAppearance =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush
        let metric m = manager.Metric m :> IObservable<float>
        let space s = manager.Space s :> IObservable<float>
        let timing t = manager.Timing t :> IObservable<float>
        let icon g b = IconSet.image g b (manager.Metric Metric.IconGlyph)
        let reduced = ReadingLook.reduced manager
        let surface variant (b: Border) = styles.Surface(b, variant)
        let iconButton (b: Border) =
            surface StyleClass.hoverPlain b
            b.Classes.Add StyleClass.iconButton
        { Markdown = ReadingLook.markdown manager styles
          Copy = ReadingLook.copy manager
          Brush = brush
          Metric = metric
          Space = space
          Timing = timing
          Icon = icon
          DressIcon = iconButton
          Reduced = reduced }
