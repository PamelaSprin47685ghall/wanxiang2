namespace Wanxiang2.ComposerDesk

open System
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch

/// 输入区外观，只保留 PendingLook 与 Space 订阅
type ComposerMetrics =
    { Pending: PendingAppearance
      /// 输入区四周留白（旧 Spacing.space2xl / spaceXl / spaceMd，水平随宽度取档）
      Space: Space -> IObservable<float> }

module ComposerMetrics =

    let private fixedValue (v: float) = LiveValue v :> IObservable<float>

    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : ComposerMetrics =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush
        let metric m = manager.Metric m :> IObservable<float>
        let space s = manager.Space s :> IObservable<float>
        let font = manager.FontFamily :> IObservable<FontFamily>
        let surface variant (b: Avalonia.Controls.Border) = styles.Surface(b, variant)
        { Pending =
            { Brush = brush
              Metric = metric
              Space = space
              BodyFont = font
              DressSecondary = (fun b -> surface StyleClass.hoverBordered b; b.Classes.Add StyleClass.buttonSecondary) }
          Space = space }
