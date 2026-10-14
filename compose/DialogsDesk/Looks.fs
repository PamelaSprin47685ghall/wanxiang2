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
open Wanxiang2.ConfirmPane
open Wanxiang2.PromptPane
open Wanxiang2.TextEditPane
open Wanxiang2.ConnectPane
open Wanxiang2.SessionPrefsPane
open Wanxiang2.ShortcutHelpPane

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
        {| Text = brush t PaletteKey.Text
           Muted = brush t PaletteKey.TextMuted
           Faint = brush t PaletteKey.TextFaint
           Accent = brush t PaletteKey.Accent
           Danger = brush t PaletteKey.Danger
           Success = brush t PaletteKey.Success
           Warning = brush t PaletteKey.Warning
           FieldSurface = brush t PaletteKey.Surface
           FieldEdge = brush t PaletteKey.Border
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
           Glyph = glyph |}

    let fieldCheckOf (t: ThemeManager) : FieldCheckLook =
        { ErrorForeground = t.Brush PaletteKey.Danger
          InvalidStroke = t.Brush PaletteKey.Danger
          NormalStroke = t.Brush PaletteKey.Border
          MessageFontSize = t.Metric Metric.FontCaption
          MessageLineHeight = t.Metric Metric.CaptionLineHeight
          MessageMargin = Live.map (fun v -> Thickness(0.0, v, 0.0, 0.0)) (t.Space Space.XXs) }

    let confirmOf (t: ThemeManager) : ConfirmPaneLook =
        let c = common t (glyphOf t)
        { Text = c.Text; Muted = c.Muted; Faint = c.Faint; Accent = c.Accent; Danger = c.Danger
          Success = c.Success; Warning = c.Warning; FieldSurface = c.FieldSurface; FieldEdge = c.FieldEdge
          BodyFontSize = c.BodyFontSize; SmallFontSize = c.SmallFontSize; CaptionFontSize = c.CaptionFontSize
          MicroFontSize = c.MicroFontSize; HeadingFontSize = c.HeadingFontSize; TitleFontSize = c.TitleFontSize
          DisplayFontSize = c.DisplayFontSize; CaptionLineHeight = c.CaptionLineHeight; LabelTracking = c.LabelTracking
          SpaceXs = c.SpaceXs; SpaceSm = c.SpaceSm; SpaceMd = c.SpaceMd; SpaceXl = c.SpaceXl; Space2xl = c.Space2xl
          Space4xl = c.Space4xl; RadiusMd = c.RadiusMd; RadiusLg = c.RadiusLg; BorderWidth = c.BorderWidth
          FieldInsetX = c.FieldInsetX; TextFieldPaddingY = c.TextFieldPaddingY
          TextFieldTextMinHeight = c.TextFieldTextMinHeight; SettingsRowMinHeight = c.SettingsRowMinHeight
          MonoFont = c.MonoFont; Glyph = c.Glyph; BodyLineHeight = metric t Metric.UiBodyLineHeight }

    let promptOf (t: ThemeManager) : PromptPaneLook =
        let c = common t (glyphOf t)
        { Text = c.Text; Muted = c.Muted; Faint = c.Faint; Accent = c.Accent; Danger = c.Danger
          Success = c.Success; Warning = c.Warning; FieldSurface = c.FieldSurface; FieldEdge = c.FieldEdge
          BodyFontSize = c.BodyFontSize; SmallFontSize = c.SmallFontSize; CaptionFontSize = c.CaptionFontSize
          MicroFontSize = c.MicroFontSize; HeadingFontSize = c.HeadingFontSize; TitleFontSize = c.TitleFontSize
          DisplayFontSize = c.DisplayFontSize; CaptionLineHeight = c.CaptionLineHeight; LabelTracking = c.LabelTracking
          SpaceXs = c.SpaceXs; SpaceSm = c.SpaceSm; SpaceMd = c.SpaceMd; SpaceXl = c.SpaceXl; Space2xl = c.Space2xl
          Space4xl = c.Space4xl; RadiusMd = c.RadiusMd; RadiusLg = c.RadiusLg; BorderWidth = c.BorderWidth
          FieldInsetX = c.FieldInsetX; TextFieldPaddingY = c.TextFieldPaddingY
          TextFieldTextMinHeight = c.TextFieldTextMinHeight; SettingsRowMinHeight = c.SettingsRowMinHeight
          MonoFont = c.MonoFont; Glyph = c.Glyph; PendingMinWidth = metric t Metric.PendingActionMinWidth }

    let textEditOf (t: ThemeManager) : TextEditPaneLook =
        let c = common t (glyphOf t)
        { Text = c.Text; Muted = c.Muted; Faint = c.Faint; Accent = c.Accent; Danger = c.Danger
          Success = c.Success; Warning = c.Warning; FieldSurface = c.FieldSurface; FieldEdge = c.FieldEdge
          BodyFontSize = c.BodyFontSize; SmallFontSize = c.SmallFontSize; CaptionFontSize = c.CaptionFontSize
          MicroFontSize = c.MicroFontSize; HeadingFontSize = c.HeadingFontSize; TitleFontSize = c.TitleFontSize
          DisplayFontSize = c.DisplayFontSize; CaptionLineHeight = c.CaptionLineHeight; LabelTracking = c.LabelTracking
          SpaceXs = c.SpaceXs; SpaceSm = c.SpaceSm; SpaceMd = c.SpaceMd; SpaceXl = c.SpaceXl; Space2xl = c.Space2xl
          Space4xl = c.Space4xl; RadiusMd = c.RadiusMd; RadiusLg = c.RadiusLg; BorderWidth = c.BorderWidth
          FieldInsetX = c.FieldInsetX; TextFieldPaddingY = c.TextFieldPaddingY
          TextFieldTextMinHeight = c.TextFieldTextMinHeight; SettingsRowMinHeight = c.SettingsRowMinHeight
          MonoFont = c.MonoFont; Glyph = c.Glyph; TextAreaLongMinHeight = metric t Metric.TextAreaLongMinHeight }

    let connectOf (t: ThemeManager) : ConnectPaneLook =
        let c = common t (glyphOf t)
        { Text = c.Text; Muted = c.Muted; Faint = c.Faint; Accent = c.Accent; Danger = c.Danger
          Success = c.Success; Warning = c.Warning; FieldSurface = c.FieldSurface; FieldEdge = c.FieldEdge
          BodyFontSize = c.BodyFontSize; SmallFontSize = c.SmallFontSize; CaptionFontSize = c.CaptionFontSize
          MicroFontSize = c.MicroFontSize; HeadingFontSize = c.HeadingFontSize; TitleFontSize = c.TitleFontSize
          DisplayFontSize = c.DisplayFontSize; CaptionLineHeight = c.CaptionLineHeight; LabelTracking = c.LabelTracking
          SpaceXs = c.SpaceXs; SpaceSm = c.SpaceSm; SpaceMd = c.SpaceMd; SpaceXl = c.SpaceXl; Space2xl = c.Space2xl
          Space4xl = c.Space4xl; RadiusMd = c.RadiusMd; RadiusLg = c.RadiusLg; BorderWidth = c.BorderWidth
          FieldInsetX = c.FieldInsetX; TextFieldPaddingY = c.TextFieldPaddingY
          TextFieldTextMinHeight = c.TextFieldTextMinHeight; SettingsRowMinHeight = c.SettingsRowMinHeight
          MonoFont = c.MonoFont; Glyph = c.Glyph; SpaceXXs = space t Space.XXs; Hairline = brush t PaletteKey.Hairline }

    let sessionOf (t: ThemeManager) : SessionPrefsPaneLook =
        let c = common t (glyphOf t)
        { Text = c.Text; Muted = c.Muted; Faint = c.Faint; Accent = c.Accent; Danger = c.Danger
          Success = c.Success; Warning = c.Warning; FieldSurface = c.FieldSurface; FieldEdge = c.FieldEdge
          BodyFontSize = c.BodyFontSize; SmallFontSize = c.SmallFontSize; CaptionFontSize = c.CaptionFontSize
          MicroFontSize = c.MicroFontSize; HeadingFontSize = c.HeadingFontSize; TitleFontSize = c.TitleFontSize
          DisplayFontSize = c.DisplayFontSize; CaptionLineHeight = c.CaptionLineHeight; LabelTracking = c.LabelTracking
          SpaceXs = c.SpaceXs; SpaceSm = c.SpaceSm; SpaceMd = c.SpaceMd; SpaceXl = c.SpaceXl; Space2xl = c.Space2xl
          Space4xl = c.Space4xl; RadiusMd = c.RadiusMd; RadiusLg = c.RadiusLg; BorderWidth = c.BorderWidth
          FieldInsetX = c.FieldInsetX; TextFieldPaddingY = c.TextFieldPaddingY
          TextFieldTextMinHeight = c.TextFieldTextMinHeight; SettingsRowMinHeight = c.SettingsRowMinHeight
          MonoFont = c.MonoFont; Glyph = c.Glyph
          TextAreaMinHeight = metric t Metric.TextAreaMinHeight
          ContentMaxHeight = metric t Metric.DialogContentMaxHeight
          PendingMinWidth = metric t Metric.PendingActionMinWidth }

    let shortcutsOf (t: ThemeManager) : ShortcutHelpPaneLook =
        let c = common t (glyphOf t)
        { Text = c.Text; Muted = c.Muted; Faint = c.Faint; Accent = c.Accent; Danger = c.Danger
          Success = c.Success; Warning = c.Warning; FieldSurface = c.FieldSurface; FieldEdge = c.FieldEdge
          BodyFontSize = c.BodyFontSize; SmallFontSize = c.SmallFontSize; CaptionFontSize = c.CaptionFontSize
          MicroFontSize = c.MicroFontSize; HeadingFontSize = c.HeadingFontSize; TitleFontSize = c.TitleFontSize
          DisplayFontSize = c.DisplayFontSize; CaptionLineHeight = c.CaptionLineHeight; LabelTracking = c.LabelTracking
          SpaceXs = c.SpaceXs; SpaceSm = c.SpaceSm; SpaceMd = c.SpaceMd; SpaceXl = c.SpaceXl; Space2xl = c.Space2xl
          Space4xl = c.Space4xl; RadiusMd = c.RadiusMd; RadiusLg = c.RadiusLg; BorderWidth = c.BorderWidth
          FieldInsetX = c.FieldInsetX; TextFieldPaddingY = c.TextFieldPaddingY
          TextFieldTextMinHeight = c.TextFieldTextMinHeight; SettingsRowMinHeight = c.SettingsRowMinHeight
          MonoFont = c.MonoFont; Glyph = c.Glyph
          KeyCapMinWidth = metric t Metric.ShortcutKeyMinWidth
          ContentMaxHeight = metric t Metric.DialogContentMaxHeight
          SpaceXXs = space t Space.XXs
          RadiusSm = space t Space.RadiusSm }
