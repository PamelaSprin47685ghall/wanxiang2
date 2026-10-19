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


    /// 菜单项图标槽
    let menuIcon (t: ThemeManager) (name: string) : IBrush -> Control =
        let size = t.Metric Metric.IconGlyph
        fun b -> IconSet.image (glyphFor name) b size
