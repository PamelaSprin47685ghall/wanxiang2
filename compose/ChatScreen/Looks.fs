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

/// 对话屏的外观：只有画笔与图标回调（普通记录，没有活值字段，W7 外观退役）。
/// 顶栏留白（Space.X3l）、版本条留白（Space.Xs）、说明字号（Metric.FontCaption）、
/// 头像边长（Metric.LogoAvatar）与图标按钮档（Metric.IconButton）都是资源字典里的浮点值，
/// ChatScreen 用量时刻现读；对话区自身也不再需要外观注入。
type ChatScreenMetrics =
    {
        /// 对话屏底色（旧 Tokens.canvas）
        Canvas: IBrush
        /// 定位高亮底色（旧 Tokens.accentSoft）
        Highlight: IBrush
        TextMuted: IBrush
        /// 图标（菜单项与版本条）
        Icon: Glyph -> IBrush -> Control
        /// 图标按钮样式（版本条）
        DressIcon: Border -> unit
    }

module ChatScreenMetrics =

    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : ChatScreenMetrics =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush

        let icon g b =
            IconSet.image g b (manager.Metric Metric.IconGlyph)

        let surface variant (b: Control) = styles.Surface(b, variant)

        let dressIcon (b: Control) =
            surface StyleClass.hoverPlain b
            b.Classes.Add StyleClass.iconButton

        {
            Canvas = brush PaletteKey.Canvas
            Highlight = brush PaletteKey.AccentSoft
            TextMuted = brush PaletteKey.TextMuted
            Icon = icon
            DressIcon = dressIcon
        }
