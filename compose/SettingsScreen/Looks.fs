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
