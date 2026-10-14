namespace Wanxiang2.SettingsScreen

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.IconSet
open Wanxiang2.FieldCheck
open Wanxiang2.ShellDesk
open Wanxiang2.GeneralPane
open Wanxiang2.ProvidersPane
open Wanxiang2.ToolsPane
open Wanxiang2.DevicesPane
open Wanxiang2.OnboardingPane
open Wanxiang2.SettingsFrame

/// 设置页各面板的外观：全部取主题管理者的活实例（画笔同一实例、尺寸是可变的活值），这里不写任何字面样式值。
module SettingsLooks =
    /// 面板用到的图标名 → 官方图标集；"logo" 是品牌标（尺寸由面板绑到活的 LogoEmpty）
    let glyphOf (t: ThemeManager) : string -> IBrush -> Control =
        let brand = lazy (ShellBrand(t))
        let size = t.Space Space.GlyphMd :> IObservable<float>
        fun name brush ->
            let pick g = IconSet.image g brush size
            match name with
            | "logo" -> brand.Value.Logo((t.Metric Metric.LogoEmpty).Value)
            | "alert" -> pick Glyph.Alert
            | "check-circle" -> pick Glyph.CheckCircle
            | "chevron-down" -> pick Glyph.ChevronDown
            | "minus" -> pick Glyph.Minus
            | "plus" -> pick Glyph.Plus
            | "more" -> pick Glyph.More
            | "pencil" -> pick Glyph.Pencil
            | "trash" -> pick Glyph.Trash
            | "server" -> pick Glyph.Server
            | "wrench" -> pick Glyph.Wrench
            | "sliders" -> pick Glyph.Sliders
            | "devices" -> pick Glyph.Devices
            | "sun" -> pick Glyph.Sun
            | "info" -> pick Glyph.Info
            | _ -> pick Glyph.Info

    let fieldCheckOf (t: ThemeManager) : FieldCheckLook =
        { ErrorForeground = t.Brush PaletteKey.Danger
          InvalidStroke = t.Brush PaletteKey.Danger
          NormalStroke = t.Brush PaletteKey.Border
          MessageFontSize = t.Metric Metric.FontCaption
          MessageLineHeight = t.Metric Metric.CaptionLineHeight
          MessageMargin = Live.map (fun v -> Thickness(0.0, v, 0.0, 0.0)) (t.Space Space.XXs) }


    let generalOf (t: ThemeManager) : GeneralPaneLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        let space s = t.Space s :> IObservable<float>
        let glyph = glyphOf t
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Faint = brush PaletteKey.TextFaint
          Accent = brush PaletteKey.Accent
          Danger = brush PaletteKey.Danger
          Success = brush PaletteKey.Success
          Warning = brush PaletteKey.Warning
          FieldSurface = brush PaletteKey.Surface
          FieldEdge = brush PaletteKey.Border
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          CaptionFontSize = metric Metric.FontCaption
          MicroFontSize = metric Metric.FontMicro
          HeadingFontSize = metric Metric.FontHeading
          TitleFontSize = metric Metric.FontTitle
          DisplayFontSize = metric Metric.FontDisplay
          CaptionLineHeight = metric Metric.CaptionLineHeight
          LabelTracking = space Space.TrackingSection
          SpaceXs = space Space.Xs
          SpaceSm = space Space.Sm
          SpaceMd = space Space.Md
          SpaceXl = space Space.Xl
          Space2xl = space Space.X2l
          Space4xl = space Space.X4l
          RadiusMd = space Space.RadiusMd
          RadiusLg = space Space.RadiusLg
          BorderWidth = metric Metric.BorderWidth
          FieldInsetX = metric Metric.FieldInsetX
          TextFieldPaddingY = metric Metric.TextFieldPaddingY
          TextFieldTextMinHeight = metric Metric.TextFieldTextMinHeight
          SettingsRowMinHeight = metric Metric.SettingsRowMinHeight
          MonoFont = t.MonoFontFamily
          Glyph = glyph
          SpaceXXs = space Space.XXs
          TextAreaMinHeight = metric Metric.TextAreaMinHeight
          FormSingleColumnBreakpoint = metric Metric.FormSingleColumnBreakpoint
          AboutKeyMinWidth = metric Metric.AboutKeyMinWidth
          FontSizeValueMinWidth = metric Metric.FontSizeValueMinWidth
          PendingMinWidth = metric Metric.PendingActionMinWidth
          SelectCaptionMaxWidth = metric Metric.SelectCaptionMaxWidth
          SelectPaddingY = metric Metric.SelectButtonPaddingY
          Selection = brush PaletteKey.AccentSoft }

    let providersOf (t: ThemeManager) : ProvidersPaneLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        let space s = t.Space s :> IObservable<float>
        let glyph = glyphOf t
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Faint = brush PaletteKey.TextFaint
          Accent = brush PaletteKey.Accent
          Danger = brush PaletteKey.Danger
          Success = brush PaletteKey.Success
          Warning = brush PaletteKey.Warning
          FieldSurface = brush PaletteKey.Surface
          FieldEdge = brush PaletteKey.Border
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          CaptionFontSize = metric Metric.FontCaption
          MicroFontSize = metric Metric.FontMicro
          HeadingFontSize = metric Metric.FontHeading
          TitleFontSize = metric Metric.FontTitle
          DisplayFontSize = metric Metric.FontDisplay
          CaptionLineHeight = metric Metric.CaptionLineHeight
          LabelTracking = space Space.TrackingSection
          SpaceXs = space Space.Xs
          SpaceSm = space Space.Sm
          SpaceMd = space Space.Md
          SpaceXl = space Space.Xl
          Space2xl = space Space.X2l
          Space4xl = space Space.X4l
          RadiusMd = space Space.RadiusMd
          RadiusLg = space Space.RadiusLg
          BorderWidth = metric Metric.BorderWidth
          FieldInsetX = metric Metric.FieldInsetX
          TextFieldPaddingY = metric Metric.TextFieldPaddingY
          TextFieldTextMinHeight = metric Metric.TextFieldTextMinHeight
          SettingsRowMinHeight = metric Metric.SettingsRowMinHeight
          MonoFont = t.MonoFontFamily
          Glyph = glyph
          SpaceXXs = space Space.XXs
          TextAreaMinHeight = metric Metric.TextAreaMinHeight
          EmptyLogoSize = metric Metric.LogoEmpty
          PendingMinWidth = metric Metric.PendingActionMinWidth
          StatusDotSize = metric Metric.StatusDotSize
          SelectCaptionMaxWidth = metric Metric.SelectCaptionMaxWidth
          SelectPaddingY = metric Metric.SelectButtonPaddingY }

    let toolsOf (t: ThemeManager) : ToolsPaneLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        let space s = t.Space s :> IObservable<float>
        let glyph = glyphOf t
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Faint = brush PaletteKey.TextFaint
          Accent = brush PaletteKey.Accent
          Danger = brush PaletteKey.Danger
          Success = brush PaletteKey.Success
          Warning = brush PaletteKey.Warning
          FieldSurface = brush PaletteKey.Surface
          FieldEdge = brush PaletteKey.Border
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          CaptionFontSize = metric Metric.FontCaption
          MicroFontSize = metric Metric.FontMicro
          HeadingFontSize = metric Metric.FontHeading
          TitleFontSize = metric Metric.FontTitle
          DisplayFontSize = metric Metric.FontDisplay
          CaptionLineHeight = metric Metric.CaptionLineHeight
          LabelTracking = space Space.TrackingSection
          SpaceXs = space Space.Xs
          SpaceSm = space Space.Sm
          SpaceMd = space Space.Md
          SpaceXl = space Space.Xl
          Space2xl = space Space.X2l
          Space4xl = space Space.X4l
          RadiusMd = space Space.RadiusMd
          RadiusLg = space Space.RadiusLg
          BorderWidth = metric Metric.BorderWidth
          FieldInsetX = metric Metric.FieldInsetX
          TextFieldPaddingY = metric Metric.TextFieldPaddingY
          TextFieldTextMinHeight = metric Metric.TextFieldTextMinHeight
          SettingsRowMinHeight = metric Metric.SettingsRowMinHeight
          MonoFont = t.MonoFontFamily
          Glyph = glyph
          SpaceXXs = space Space.XXs
          TextAreaMinHeight = metric Metric.TextAreaMinHeight
          EmptyLogoSize = metric Metric.LogoEmpty
          PendingMinWidth = metric Metric.PendingActionMinWidth }

    let devicesOf (t: ThemeManager) : DevicesPaneLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        let space s = t.Space s :> IObservable<float>
        let glyph = glyphOf t
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Faint = brush PaletteKey.TextFaint
          Accent = brush PaletteKey.Accent
          Danger = brush PaletteKey.Danger
          Success = brush PaletteKey.Success
          Warning = brush PaletteKey.Warning
          FieldSurface = brush PaletteKey.Surface
          FieldEdge = brush PaletteKey.Border
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          CaptionFontSize = metric Metric.FontCaption
          MicroFontSize = metric Metric.FontMicro
          HeadingFontSize = metric Metric.FontHeading
          TitleFontSize = metric Metric.FontTitle
          DisplayFontSize = metric Metric.FontDisplay
          CaptionLineHeight = metric Metric.CaptionLineHeight
          LabelTracking = space Space.TrackingSection
          SpaceXs = space Space.Xs
          SpaceSm = space Space.Sm
          SpaceMd = space Space.Md
          SpaceXl = space Space.Xl
          Space2xl = space Space.X2l
          Space4xl = space Space.X4l
          RadiusMd = space Space.RadiusMd
          RadiusLg = space Space.RadiusLg
          BorderWidth = metric Metric.BorderWidth
          FieldInsetX = metric Metric.FieldInsetX
          TextFieldPaddingY = metric Metric.TextFieldPaddingY
          TextFieldTextMinHeight = metric Metric.TextFieldTextMinHeight
          SettingsRowMinHeight = metric Metric.SettingsRowMinHeight
          MonoFont = t.MonoFontFamily
          Glyph = glyph
          CodeTracking = metric Metric.PairingCodeTracking
          QrModuleSize = metric Metric.QrModuleSize }

    let onboardingOf (t: ThemeManager) : OnboardingPaneLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        let space s = t.Space s :> IObservable<float>
        let glyph = glyphOf t
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Faint = brush PaletteKey.TextFaint
          Accent = brush PaletteKey.Accent
          Danger = brush PaletteKey.Danger
          Success = brush PaletteKey.Success
          Warning = brush PaletteKey.Warning
          FieldSurface = brush PaletteKey.Surface
          FieldEdge = brush PaletteKey.Border
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          CaptionFontSize = metric Metric.FontCaption
          MicroFontSize = metric Metric.FontMicro
          HeadingFontSize = metric Metric.FontHeading
          TitleFontSize = metric Metric.FontTitle
          DisplayFontSize = metric Metric.FontDisplay
          CaptionLineHeight = metric Metric.CaptionLineHeight
          LabelTracking = space Space.TrackingSection
          SpaceXs = space Space.Xs
          SpaceSm = space Space.Sm
          SpaceMd = space Space.Md
          SpaceXl = space Space.Xl
          Space2xl = space Space.X2l
          Space4xl = space Space.X4l
          RadiusMd = space Space.RadiusMd
          RadiusLg = space Space.RadiusLg
          BorderWidth = metric Metric.BorderWidth
          FieldInsetX = metric Metric.FieldInsetX
          TextFieldPaddingY = metric Metric.TextFieldPaddingY
          TextFieldTextMinHeight = metric Metric.TextFieldTextMinHeight
          SettingsRowMinHeight = metric Metric.SettingsRowMinHeight
          MonoFont = t.MonoFontFamily
          Glyph = glyph
          Space3xl = space Space.X3l
          CardMaxWidth = metric Metric.EmptyStateMaxWidth
          PendingMinWidth = metric Metric.PendingActionMinWidth }

    let frameOf (t: ThemeManager) : SettingsFrameLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        let space s = t.Space s :> IObservable<float>
        let glyph = glyphOf t
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Faint = brush PaletteKey.TextFaint
          Accent = brush PaletteKey.Accent
          Danger = brush PaletteKey.Danger
          Success = brush PaletteKey.Success
          Warning = brush PaletteKey.Warning
          FieldSurface = brush PaletteKey.Surface
          FieldEdge = brush PaletteKey.Border
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          CaptionFontSize = metric Metric.FontCaption
          MicroFontSize = metric Metric.FontMicro
          HeadingFontSize = metric Metric.FontHeading
          TitleFontSize = metric Metric.FontTitle
          DisplayFontSize = metric Metric.FontDisplay
          CaptionLineHeight = metric Metric.CaptionLineHeight
          LabelTracking = space Space.TrackingSection
          SpaceXs = space Space.Xs
          SpaceSm = space Space.Sm
          SpaceMd = space Space.Md
          SpaceXl = space Space.Xl
          Space2xl = space Space.X2l
          Space4xl = space Space.X4l
          RadiusMd = space Space.RadiusMd
          RadiusLg = space Space.RadiusLg
          BorderWidth = metric Metric.BorderWidth
          FieldInsetX = metric Metric.FieldInsetX
          TextFieldPaddingY = metric Metric.TextFieldPaddingY
          TextFieldTextMinHeight = metric Metric.TextFieldTextMinHeight
          SettingsRowMinHeight = metric Metric.SettingsRowMinHeight
          MonoFont = t.MonoFontFamily
          Glyph = glyph
          Space6xl = space Space.X6l
          Space8xl = space Space.X8l
          Canvas = brush PaletteKey.Canvas
          Rail = brush PaletteKey.SurfaceContainer
          Chrome = brush PaletteKey.Hairline
          Selected = brush PaletteKey.Selected
          BarHeight = metric Metric.BarHeight
          NavWidth = metric Metric.SettingsNavWidth
          NavMinHeight = metric Metric.SettingsNavMinHeight
          SelectedEdgeWidth = metric Metric.SidebarSelectedEdgeWidth
          ContentMaxWidth = metric Metric.SettingsContentMaxWidth
          SubtleOpacity = metric Metric.OpacitySubtle
          CompactBreakpoint = metric Metric.CompactBreakpoint }
