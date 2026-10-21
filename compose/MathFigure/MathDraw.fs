namespace Wanxiang2.MathFigure

open System
open System.Collections.Concurrent
open System.Globalization
open Avalonia
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.ThemeFonts

/// 一条已转成 Avalonia 类型的绘制图元。坐标相对公式盒的基线左端，y 向下为正。
/// 字形保留整形结果与选中的字体，字族选错必须能在测试里断言。
[<RequireQualifiedAccess>]
type MathMark =
    | Glyph of text: string * shaped: FormattedText * typeface: Typeface * topLeft: Point
    | Rule of rect: Rect
    | Path of geometry: Geometry * transform: Matrix * clip: Rect

/// 排好的公式：宽度，以及基线上下各占多少。
type MathPicture =
    {
        Width: float
        Above: float
        Below: float
        Marks: MathMark list
    }

/// 公式量尺（旧 Rich/MathLayout.fs）：FormattedText 量字形，再把 MathTypeset 的图元转成 Avalonia 图元。
/// 正文字体由调用方给（主题管理者的活实例的当前值），KaTeX 字族取自 ThemeFonts。
module MathDraw =

    let private families = ConcurrentDictionary<string, FontFamily>()

    let private typefaceOf (body: FontFamily) (family: string) =
        if String.IsNullOrEmpty family then
            Typeface body
        else
            Typeface(families.GetOrAdd(family, ThemeFonts.family))

    let private shape body (text: string) family (size: float) (brush: IBrush) =
        let typeface = typefaceOf body family
        FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush), typeface

    /// 量尺与绘制用同一套 FormattedText，宽度与绘制结果同源。
    let meter (body: FontFamily) =
        { new IGlyphMeter with
            member _.Measure(text, family, size) =
                let shaped, _ = shape body text family size Brushes.Black

                {
                    width = shaped.WidthIncludingTrailingWhitespace
                    baseline = shaped.Baseline
                    height = shaped.Height
                }
        }

    let private convert body (brush: IBrush) (item: MathItem) =
        match item with
        | MathItem.Glyph(text, family, size, x, y) ->
            let shaped, typeface = shape body text family size brush
            MathMark.Glyph(text, shaped, typeface, Point(x, y))
        | MathItem.Rule(x, y, w, h) -> MathMark.Rule(Rect(x, y, w, h))
        | MathItem.Path(d, scale, dx, dy, cx, cy, cw, ch) ->
            // SVG 的 fill-rule 默认 nonzero，Avalonia 路径语法默认 evenodd；不加 F1 根号横线会被异或掉
            MathMark.Path(
                Geometry.Parse("F1 " + d),
                Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(dx, dy),
                Rect(cx, cy, cw, ch)
            )

    /// 排一整条公式；失败带原因返回。画笔原样放进字形，主题改色时字形跟着变。
    let tryLayout
        (body: FontFamily)
        (brush: IBrush)
        (baseFontSize: float)
        (html: string)
        : Result<MathPicture, string> =
        match MathTypesetter(meter body).TryRender(baseFontSize, html) with
        | Error e -> Error e
        | Ok box ->
            try
                Ok
                    {
                        Width = box.width
                        Above = box.above
                        Below = box.below
                        Marks = box.items |> List.map (convert body brush)
                    }
            with ex ->
                Error $"{ex.GetType().Name}: {ex.Message}"
