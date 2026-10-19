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

/// 正文用到的尺寸槽位（旧 Tokens / ControlMetrics / Spacing 同名值）。
[<RequireQualifiedAccess>]
type MarkdownGauge =
    | FontMicro
    | BorderWidth
    | QuoteEdgeWidth
    | BlockPaddingX
    | BlockPaddingY
    | SpaceXXs
    | SpaceXs
    | SpaceSm
    | SpaceMd
    | RadiusXs
    | RadiusSm
    | RadiusMd
    | ListMarkerDotSize
    | ListMarkerDotRadius
    | TaskBoxSize
    | TaskBoxBorderWidth
    | TaskCheckGlyphSize
    | TrackingEmphasis
    /// 复制确认淡出时长（毫秒）
    | CopyFadeMs

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

/// Markdown 正文的外观。全部是主题管理者的活实例，由组合者填：
/// 画笔原地改色；尺寸与字体是活值，控件挂在可视树上期间跟随；
/// Glyph 给定画笔造一个图标（尺寸由填写方决定）；Dress 给按钮挂上主题安装的共享样式类。
type MarkdownMetrics =
    { Ink: MarkdownInk -> IBrush
      Gauge: MarkdownGauge -> IObservable<float>
      BodyFont: IObservable<FontFamily>
      MonoFont: IObservable<FontFamily>
      Glyph: MarkdownGlyph -> IBrush -> Control
      Dress: MarkdownButton -> Border -> unit
      /// 减弱动效（复制确认不淡出）
      Reduced: IObservable<bool>
      Copy: CopyButtonAppearance
      Math: MathFigureMetrics }
