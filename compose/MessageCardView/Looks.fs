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

/// 消息卡用到的全部外观：画笔、图标与样式类回调（普通记录，没有活值字段，W7 外观退役）。
/// 主题管的尺寸与间距（Space.Md/Sm/Xl、RadiusLg、FontCaption、LogoAvatar、减弱动效、
/// 复制淡出时长）都由 Card 经主题资源键声明式绑定或动作时刻现读，改值即时生效。
type CardAppearance =
    {
        Markdown: MarkdownMetrics
        Copy: CopyButtonAppearance
        /// 组合者自己用的笔刷、度量、图标与表面反馈
        Brush: PaletteKey -> IBrush
        Icon: Glyph -> IBrush -> Control
        /// 图标按钮的样式类（旧 Ui.iconButton）
        DressIcon: Control -> unit
    }

module CardLooks =

    /// 用主题管理者与样式填全部外观
    let ofTheme (manager: ThemeManager) (styles: ThemeStyles) : CardAppearance =
        let brush (k: PaletteKey) = manager.Brush k :> IBrush

        let icon g b =
            IconSet.image g b (manager.Metric Metric.IconGlyph)

        let surface variant (b: Control) = styles.Surface(b, variant)

        let iconButton (b: Control) =
            surface StyleClass.hoverPlain b
            b.Classes.Add StyleClass.iconButton

        {
            Markdown = ReadingLook.markdown manager styles
            Copy = ReadingLook.copy manager
            Brush = brush
            Icon = icon
            DressIcon = iconButton
        }
