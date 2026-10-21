namespace Wanxiang2.MarkdownView

open System
open Avalonia.Controls
open Avalonia.Media
open Wanxiang2.MathFigure
open Wanxiang2.CopyButton

/// 正文用到的画笔槽位（旧 Tokens 同名画笔）。
[<RequireQualifiedAccess>]
type MarkdownInk =
    | Text
    | TextMuted
    | TextFaint
    | TextOnAccent
    | Accent
    | AccentSoft
    | Success
    | Line
    | BorderSoft
    | Hairline
    | HairlineStrong
    | SurfaceContainer
    | InlineCode
    | TableHeader
    | TableStripe
    | CodeBg
    | CodeHeaderBg
    | CodeBorder
    | CodeText
    | CodeMuted
    | CodeKeyword
    | CodeString
    | CodeComment
    | CodeNumber
    | CodeType
    | CodeFunction
    | CodeMeta
    | CodeVariable
    | CodeOperator
    | CodeAddition
    | CodeDeletion

/// 正文用到的图标
[<RequireQualifiedAccess>]
type MarkdownGlyph =
    | Copy
    | Check
    | TextWrap

/// 按钮的两种外观档：代码块头部的图标按钮、「展开全部」幽灵按钮
[<RequireQualifiedAccess>]
type MarkdownButton =
    | Icon
    | Ghost

/// Markdown 正文的外观：画笔与回调（普通记录，没有活值字段）。
/// 画笔是主题管理者的活实例（原地改色）；尺寸、字体、间距一律走主题资源键
/// （Metric.* / Space.* / CornerRadius.* / Thickness.* / Padding.* / Margin.* / Tracking.* / FontFamily.Mono / Timing.* /
/// Motion.Reduced），由控件声明式绑定，主题切换即时生效由资源系统保证。
/// Glyph 给定画笔造一个图标（尺寸由填写方决定）；Dress 给按钮挂上主题安装的共享样式类。
type MarkdownMetrics =
    {
        Ink: MarkdownInk -> IBrush
        Glyph: MarkdownGlyph -> IBrush -> Control
        Dress: MarkdownButton -> Button -> unit
        Copy: CopyButtonAppearance
        Math: MathFigureMetrics
    }
