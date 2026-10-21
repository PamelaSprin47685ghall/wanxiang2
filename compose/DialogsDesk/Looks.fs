namespace Wanxiang2.DialogsDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.IconSet
open Wanxiang2.FieldCheck

/// 对话框各面板的外观：全部取主题管理者的活实例，这里不写字面样式值。
module DialogLooks =
    let private brush (t: ThemeManager) k = t.Brush k :> IBrush
    let private metric (t: ThemeManager) m = t.Metric m :> IObservable<float>
    let private space (t: ThemeManager) s = t.Space s :> IObservable<float>

    let glyphOf (t: ThemeManager) : string -> IBrush -> Control =
        let size = t.Space Space.GlyphMd :> IObservable<float>

        fun name brush ->
            let pick g = IconSet.image g brush size

            match name with
            | "close" -> pick Glyph.Close
            | "search" -> pick Glyph.Search
            | "gear" -> pick Glyph.Gear
            | _ -> pick Glyph.Info

    let common (t: ThemeManager) glyph =
        {|
            Text = brush t PaletteKey.Text
            Muted = brush t PaletteKey.TextMuted
            Faint = brush t PaletteKey.TextFaint
            Accent = brush t PaletteKey.Accent
            Danger = brush t PaletteKey.Danger
            Success = brush t PaletteKey.Success
            Warning = brush t PaletteKey.Warning
            FieldSurface = brush t PaletteKey.Surface
            FieldEdge = brush t PaletteKey.ControlStroke
            BodyFontSize = metric t Metric.FontBody
            SmallFontSize = metric t Metric.FontSmall
            CaptionFontSize = metric t Metric.FontCaption
            MicroFontSize = metric t Metric.FontMicro
            HeadingFontSize = metric t Metric.FontHeading
            TitleFontSize = metric t Metric.FontTitle
            DisplayFontSize = metric t Metric.FontDisplay
            CaptionLineHeight = metric t Metric.CaptionLineHeight
            LabelTracking = space t Space.TrackingSection
            SpaceXs = space t Space.Xs
            SpaceSm = space t Space.Sm
            SpaceMd = space t Space.Md
            SpaceXl = space t Space.Xl
            Space2xl = space t Space.X2l
            Space4xl = space t Space.X4l
            RadiusMd = space t Space.RadiusMd
            RadiusLg = space t Space.RadiusLg
            BorderWidth = metric t Metric.BorderWidth
            FieldInsetX = metric t Metric.FieldInsetX
            TextFieldPaddingY = metric t Metric.TextFieldPaddingY
            TextFieldTextMinHeight = metric t Metric.TextFieldTextMinHeight
            SettingsRowMinHeight = metric t Metric.SettingsRowMinHeight
            MonoFont = t.MonoFontFamily
            Glyph = glyph
        |}
