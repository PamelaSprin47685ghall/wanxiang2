namespace Wanxiang2.ComposerDesk

open System
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch
open Wanxiang2.IconSet
open Wanxiang2.ComposerBox
open Wanxiang2.AttachmentTray
open Wanxiang2.ModelChip
open Wanxiang2.BusyRing

/// 输入区三件的外观，都由主题管理者的活实例填
type ComposerLooks =
    { Box: ComposerBoxLook
      Tray: AttachmentTrayLook
      Chip: ModelChipLook
      Pending: PendingLook
      /// 输入区四周留白（旧 Spacing.space2xl / spaceXl / spaceMd，水平随宽度取档）
      Space: Space -> IObservable<float> }

module ComposerLooks =

    let private fixedValue (v: float) = LiveValue v :> IObservable<float>

    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : ComposerLooks =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush
        let metric m = manager.Metric m :> IObservable<float>
        let space s = manager.Space s :> IObservable<float>
        let icon g b = IconSet.image g b (manager.Metric Metric.IconGlyph)
        let font = manager.FontFamily :> IObservable<FontFamily>
        let surface variant (b: Avalonia.Controls.Border) = styles.Surface(b, variant)
        let reduced =
            let v = LiveValue(manager.Motion.IsReduced)
            manager.Motion.Changed.Add(fun _ -> v.Set manager.Motion.IsReduced)
            v :> IObservable<bool>
        let busy: BusyRingLook =
            { Ink = (function BusyInk.Track -> brush PaletteKey.Border | BusyInk.Arc -> brush PaletteKey.Accent)
              StrokeWidth = metric Metric.SpinnerStrokeWidth
              Frame = Live.map (fun (v: float) -> TimeSpan.FromMilliseconds v) (manager.Timing Timing.BusySpinnerFrame)
              Reduced = reduced }
        { Box =
            { Ink =
                function
                | BoxInk.Text -> brush PaletteKey.Text
                | BoxInk.TextMuted -> brush PaletteKey.TextMuted
                | BoxInk.Accent -> brush PaletteKey.Accent
                | BoxInk.AccentSoft -> brush PaletteKey.AccentSoft
                | BoxInk.AccentFaint -> brush PaletteKey.AccentFaint
                | BoxInk.TextOnAccent -> brush PaletteKey.TextOnAccent
                | BoxInk.Surface -> brush PaletteKey.Surface
                | BoxInk.Hairline -> brush PaletteKey.Hairline
                | BoxInk.SurfaceContainer -> brush PaletteKey.SurfaceContainer
                | BoxInk.HairlineStrong -> brush PaletteKey.HairlineStrong
                | BoxInk.ControlStroke -> brush PaletteKey.ControlStroke
                | BoxInk.Warning -> brush PaletteKey.Warning
              Gauge =
                function
                | BoxGauge.FontReading -> metric Metric.FontReading
                | BoxGauge.FontCaption -> metric Metric.FontCaption
                | BoxGauge.FontSmall -> metric Metric.FontSmall
                | BoxGauge.InputMinHeight -> metric Metric.ComposerInputMinHeight
                | BoxGauge.InputMaxHeight -> metric Metric.ComposerMaxHeight
                | BoxGauge.ShellPadX -> metric Metric.ComposerShellPaddingX
                | BoxGauge.ShellPadY -> metric Metric.ComposerShellPaddingY
                | BoxGauge.RadiusMd -> space Space.RadiusMd
                | BoxGauge.RadiusLg -> space Space.RadiusLg
                | BoxGauge.BorderWidth -> metric Metric.BorderWidth
                | BoxGauge.SpaceXs -> space Space.Xs
                | BoxGauge.SpaceMd -> space Space.Md
                | BoxGauge.SpaceXl -> space Space.Xl
                | BoxGauge.Space2xl -> space Space.X2l
                | BoxGauge.IconButton -> metric Metric.IconButton
                | BoxGauge.CompactTarget -> fixedValue LayoutPolicy.compactActionTarget
                | BoxGauge.NestedGutter -> fixedValue LayoutPolicy.nestedScrollGutter
                | BoxGauge.OpacityDisabled -> metric Metric.OpacityDisabled
                | BoxGauge.OpacityComposerDisabled -> metric Metric.OpacityComposerDisabled
                | BoxGauge.CaptionLineHeight -> fixedValue ReadingRhythm.captionLineHeight
                | BoxGauge.ReadingWidth -> metric Metric.ReadingWidth
                | BoxGauge.StrokeThick -> space Space.StrokeThick
              BodyFont = font
              Glyph =
                fun g b ->
                    icon
                        (match g with
                         | BoxGlyph.Send -> Glyph.Send
                         | BoxGlyph.Stop -> Glyph.Stop
                         | BoxGlyph.Paperclip -> Glyph.Paperclip)
                        b
              DressSend = (fun b -> surface StyleClass.hoverAccent b; b.Classes.Add StyleClass.iconButtonAccent)
              DressQueue = (fun b -> surface StyleClass.hoverPlain b; b.Classes.Add StyleClass.iconButton)
              DressAttach = (fun b -> surface StyleClass.hoverPlain b; b.Classes.Add StyleClass.iconButton)
              DressGhost = (fun b -> surface StyleClass.hoverPlain b; b.Classes.Add StyleClass.buttonGhost) }
          Tray =
            { Ink =
                function
                | TrayInk.Text -> brush PaletteKey.Text
                | TrayInk.TextMuted -> brush PaletteKey.TextMuted
                | TrayInk.TextFaint -> brush PaletteKey.TextFaint
                | TrayInk.Danger -> brush PaletteKey.Danger
                | TrayInk.Accent -> brush PaletteKey.Accent
                | TrayInk.SurfaceContainer -> brush PaletteKey.SurfaceContainer
                | TrayInk.Hairline -> brush PaletteKey.Hairline
                | TrayInk.HairlineStrong -> brush PaletteKey.HairlineStrong
              Gauge =
                function
                | TrayGauge.SpaceXs -> space Space.Xs
                | TrayGauge.SpaceMd -> space Space.Md
                | TrayGauge.SpaceXl -> space Space.Xl
                | TrayGauge.RadiusSm -> space Space.RadiusSm
                | TrayGauge.RadiusMd -> space Space.RadiusMd
                | TrayGauge.BorderWidth -> metric Metric.BorderWidth
                | TrayGauge.FontMicro -> metric Metric.FontMicro
                | TrayGauge.IconSlot -> metric Metric.ComposerAttachmentIconSlot
                | TrayGauge.NameMaxWidth -> metric Metric.ComposerAttachmentNameMaxWidth
                | TrayGauge.StateWidth -> metric Metric.ComposerAttachmentStateWidth
                | TrayGauge.PadX -> metric Metric.ChipPaddingX
                | TrayGauge.PadY -> metric Metric.ChipPaddingY
                | TrayGauge.MaxHeight -> fixedValue LayoutPolicy.attachmentDraftMaxHeight
                | TrayGauge.ScrollGutter -> fixedValue LayoutPolicy.nestedScrollGutter
                | TrayGauge.CompactPadBottom -> space Space.Md
                | TrayGauge.FadeMs -> manager.Timing Timing.ControlRowFade
              BodyFont = font
              Glyph =
                fun g b ->
                    match g with
                    | TrayGlyph.Image -> icon Glyph.Image b
                    | TrayGlyph.File -> icon Glyph.File b
                    | TrayGlyph.Alert -> icon Glyph.Alert b
                    | TrayGlyph.Close -> icon Glyph.Close b
                    | TrayGlyph.Spinner -> BusyRing(busy, (manager.Metric Metric.IconGlyph).Value).Host
              DressChip = (fun b -> b.Classes.Add StyleClass.groupingCard)
              DressRemove = (fun b -> surface StyleClass.hoverPlain b; b.Classes.Add StyleClass.iconButton)
              Reduced = reduced }
          Chip =
            { Ink =
                function
                | ChipInk.Text -> brush PaletteKey.Text
                | ChipInk.TextMuted -> brush PaletteKey.TextMuted
                | ChipInk.Hairline -> brush PaletteKey.Hairline
                | ChipInk.TextFaint -> brush PaletteKey.TextFaint
              Gauge =
                function
                | ChipGauge.FontSmall -> metric Metric.FontSmall
                | ChipGauge.MaxWidth -> metric Metric.ComposerModelMaxWidth
                | ChipGauge.CompactMaxWidth -> metric Metric.ComposerModelCompactMaxWidth
                | ChipGauge.PadX -> metric Metric.CompactChipPaddingX
                | ChipGauge.PadY -> metric Metric.CompactChipPaddingY
                | ChipGauge.RadiusSm -> space Space.RadiusSm
                | ChipGauge.BorderWidth -> metric Metric.BorderWidth
                | ChipGauge.CompactMinHeight -> fixedValue LayoutPolicy.compactActionTarget
                | ChipGauge.SpaceXs -> space Space.Xs
              BodyFont = font
              Chevron = icon Glyph.ChevronDown
              Dress = surface StyleClass.hoverBordered }
          Pending =
            { Brush = brush
              Metric = metric
              Space = space
              BodyFont = font
              DressSecondary = (fun b -> surface StyleClass.hoverBordered b; b.Classes.Add StyleClass.buttonSecondary) }
          Space = space }
