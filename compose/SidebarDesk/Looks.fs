namespace Wanxiang2.SidebarDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.Theme
open Wanxiang2.MotionSwitch
open Wanxiang2.IconSet
open Wanxiang2.ConversationRows
open Wanxiang2.SidebarSearch
open Wanxiang2.BatchBar
open Wanxiang2.HostSwitcher
open Wanxiang2.SidebarBrand
open Wanxiang2.ListEmptyCard

/// 侧栏各控件的外观：全部取主题管理者的活实例，这里不写字面样式值。
module SidebarLooks =
    let private brush (t: ThemeManager) k = t.Brush k :> IBrush
    let private metric (t: ThemeManager) m = t.Metric m :> IObservable<float>
    let private space (t: ThemeManager) s = t.Space s :> IObservable<float>

    let glyphFor (name: string) =
        match name with
        | "more" -> Glyph.More
        | "pin" -> Glyph.Pin
        | "archive" -> Glyph.Archive
        | "chevron-up" -> Glyph.ChevronUp
        | "chevron-down" -> Glyph.ChevronDown
        | "close" -> Glyph.Close
        | "search" -> Glyph.Search
        | "gear" -> Glyph.Gear
        | "arrow-left" -> Glyph.ArrowLeft
        | "check" -> Glyph.Check
        | "pencil" -> Glyph.Pencil
        | "fork" -> Glyph.Fork
        | "download" -> Glyph.Download
        | "trash" -> Glyph.Trash
        | _ -> Glyph.Info

    let glyphOf (t: ThemeManager) : string -> IBrush -> Control =
        let size = metric t Metric.IconGlyph
        fun name b -> IconSet.image (glyphFor name) b size

    /// 菜单项图标槽
    let menuIcon (t: ThemeManager) (name: string) : IBrush -> Control =
        let size = metric t Metric.IconGlyph
        fun b -> IconSet.image (glyphFor name) b size

    let common (t: ThemeManager) =
        {| Text = brush t PaletteKey.Text
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
           Glyph = glyphOf t |}

    let rowsOf (t: ThemeManager) : ConversationRowsLook =
        let c = common t
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
          Selected = brush t PaletteKey.Selected
          Hover = brush t PaletteKey.Hover
          SelectedHover = t.Derived DerivedBrush.HoverSelected
          AccentSoft = brush t PaletteKey.AccentSoft
          RowMinHeight = metric t Metric.SidebarRowMinHeight
          RowPaddingY = metric t Metric.SidebarRowPaddingY
          StateSlotWidth = metric t Metric.SidebarStateSlotWidth
          StateGlyphSize = metric t Metric.SidebarStateGlyphSize
          RunningDotSize = metric t Metric.SidebarRunningDotSize
          SelectedEdgeWidth = metric t Metric.SidebarSelectedEdgeWidth
          RowContentSpacing = metric t Metric.SidebarRowContentSpacing
          IconButtonSize = metric t Metric.IconButton
          CompactTarget = metric t Metric.CompactActionTarget
          OpacitySubtle = metric t Metric.OpacitySubtle
          Transitions = fun bordered -> t.StateTransitions bordered }

    let searchOf (t: ThemeManager) : SidebarSearchLook =
        let c = common t
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
          DebounceMs = t.Timing Timing.SearchInputDebounce :> IObservable<float>
          GlyphSize = metric t Metric.IconGlyph
          IconButtonSize = metric t Metric.IconButton
          CompactTarget = metric t Metric.CompactActionTarget
          Space6xl = space t Space.X6l }

    let batchOf (t: ThemeManager) : BatchBarLook =
        let c = common t
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
          HeaderSurface = brush t PaletteKey.SurfaceContainer
          BarSurface = brush t PaletteKey.Surface
          Hairline = brush t PaletteKey.Hairline
          DividerWidth = metric t Metric.SidebarDividerWidth
          SelectButtonPaddingY = metric t Metric.SelectButtonPaddingY }

    let hostOf (t: ThemeManager) : HostSwitcherLook =
        let c = common t
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
          DotSize = metric t Metric.SidebarRunningDotSize
          BarHeight = metric t Metric.BarHeight
          DividerWidth = metric t Metric.SidebarDividerWidth
          IconButtonSize = metric t Metric.IconButton
          CompactTarget = metric t Metric.CompactActionTarget
          TextButtonMinHeight = metric t Metric.TextButtonMinHeight
          Hairline = brush t PaletteKey.Hairline
          RadiusSm = space t Space.RadiusSm }

    let brandOf (t: ThemeManager) (logo: float -> Control) : SidebarBrandLook =
        let c = common t
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
          Logo = logo
          LogoSize = metric t Metric.LogoSidebar
          DisplayTracking = space t Space.TrackingDisplay
          BarHeight = metric t Metric.BarHeight
          IconButtonSize = metric t Metric.IconButton
          CompactTarget = metric t Metric.CompactActionTarget
          TextButtonMinHeight = metric t Metric.TextButtonMinHeight }

    let emptyOf (t: ThemeManager) (logo: float -> Control) : ListEmptyCardLook =
        let c = common t
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
          Logo = logo
          LogoSize = metric t Metric.LogoEmpty
          SkeletonFill = brush t PaletteKey.BorderSoft
          SkeletonOpacity = metric t Metric.SkeletonOpacityBase
          SkeletonBarHeight = metric t Metric.SidebarSkeletonBarHeight
          RadiusSm = space t Space.RadiusSm
          Space3xl = space t Space.X3l
          Space6xl = space t Space.X6l }
