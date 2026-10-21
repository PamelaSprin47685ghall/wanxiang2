namespace Wanxiang2.ReadingLook

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
open Wanxiang2.MathFigure
open Wanxiang2.MarkdownView

/// 正文与公式的外观由主题管理者的活实例填写（旧 Tokens / ControlMetrics / Spacing / MotionLedger 的同名值）。
/// 共享外观（图标按钮、幽灵按钮、表面状态反馈）走 ThemeStyles 安装的样式类。
module ReadingLook =

    let private ink (manager: ThemeManager) (slot: MarkdownInk) : IBrush =
        let key =
            match slot with
            | MarkdownInk.Text -> PaletteKey.Text
            | MarkdownInk.TextMuted -> PaletteKey.TextMuted
            | MarkdownInk.TextFaint -> PaletteKey.TextFaint
            | MarkdownInk.TextOnAccent -> PaletteKey.TextOnAccent
            | MarkdownInk.Accent -> PaletteKey.Accent
            | MarkdownInk.AccentSoft -> PaletteKey.AccentSoft
            | MarkdownInk.Success -> PaletteKey.Success
            | MarkdownInk.Line -> PaletteKey.Line
            | MarkdownInk.BorderSoft -> PaletteKey.BorderSoft
            | MarkdownInk.Hairline -> PaletteKey.Hairline
            | MarkdownInk.HairlineStrong -> PaletteKey.HairlineStrong
            | MarkdownInk.SurfaceContainer -> PaletteKey.SurfaceContainer
            | MarkdownInk.InlineCode -> PaletteKey.InlineCode
            | MarkdownInk.TableHeader -> PaletteKey.TableHeader
            | MarkdownInk.TableStripe -> PaletteKey.TableStripe
            | MarkdownInk.CodeBg -> PaletteKey.CodeBg
            | MarkdownInk.CodeHeaderBg -> PaletteKey.CodeHeaderBg
            | MarkdownInk.CodeBorder -> PaletteKey.CodeBorder
            | MarkdownInk.CodeText -> PaletteKey.CodeText
            | MarkdownInk.CodeMuted -> PaletteKey.CodeMuted
            | MarkdownInk.CodeKeyword -> PaletteKey.CodeKeyword
            | MarkdownInk.CodeString -> PaletteKey.CodeString
            | MarkdownInk.CodeComment -> PaletteKey.CodeComment
            | MarkdownInk.CodeNumber -> PaletteKey.CodeNumber
            | MarkdownInk.CodeType -> PaletteKey.CodeType
            | MarkdownInk.CodeFunction -> PaletteKey.CodeFunction
            | MarkdownInk.CodeMeta -> PaletteKey.CodeMeta
            | MarkdownInk.CodeVariable -> PaletteKey.CodeVariable
            | MarkdownInk.CodeOperator -> PaletteKey.CodeOperator
            | MarkdownInk.CodeAddition -> PaletteKey.CodeAddition
            | MarkdownInk.CodeDeletion -> PaletteKey.CodeDeletion

        manager.Brush key :> IBrush

    /// 公式外观：活画笔实例；字体与留白走资源键（FontFamily.Body / Padding.MathBlock）。
    let math (manager: ThemeManager) : MathFigureMetrics = { Ink = manager.Brush PaletteKey.Text }

    /// 复制确认外观：静态参数（度量常量 + MotionLedger 时长档）。
    let copy (manager: ThemeManager) : CopyButtonAppearance =
        {
            FadeOpacity = (manager.Metric Metric.OpacityCopyConfirmFade).Value
            Hold = TimeSpan.FromMilliseconds (manager.Timing Timing.CopyConfirmationHold).Value
            Fade = TimeSpan.FromMilliseconds (manager.Timing Timing.CopyConfirmationFade).Value
        }

    /// Markdown 正文外观：画笔与回调；尺寸字体全部经主题资源键声明式送达。
    let markdown (manager: ThemeManager) (styles: ThemeStyles) : MarkdownMetrics =
        {
            Ink = ink manager
            Glyph =
                fun glyph brush ->
                    let g =
                        match glyph with
                        | MarkdownGlyph.Copy -> Glyph.Copy
                        | MarkdownGlyph.Check -> Glyph.Check
                        | MarkdownGlyph.TextWrap -> Glyph.TextWrap

                    IconSet.image g brush (manager.Metric Metric.IconGlyph)
            Dress =
                fun role (button: Button) ->
                    styles.Surface(button, StyleClass.hoverPlain)

                    button.Classes.Add(
                        match role with
                        | MarkdownButton.Icon -> StyleClass.iconButton
                        | MarkdownButton.Ghost -> StyleClass.buttonGhost
                    )
            Copy = copy manager
            Math = math manager
        }
