namespace Wanxiang.UI

open System
open System.Globalization

/// 一个字形的量测结果：推进宽度（含尾随空白）、基线到顶的距离、总高。
type GlyphMetrics =
    { width: float
      baseline: float
      height: float }

/// 字形量尺。由宿主（Avalonia 的 FormattedText、测试里的回放表）提供。
/// family 为空串表示正文字体（KaTeX 字体里没有汉字）。
type IGlyphMeter =
    abstract Measure: text: string * family: string * size: float -> GlyphMetrics

/// 一条已定位的绘制图元。坐标相对公式盒的基线左端，y 向下为正。
[<RequireQualifiedAccess>]
type MathItem =
    /// 字形：左上角位于 (x, y)
    | Glyph of text: string * family: string * size: float * x: float * y: float
    | Rule of x: float * y: float * width: float * height: float
    /// SVG 路径：先等比缩放 scale，再平移 (dx, dy)；按 clip 矩形裁剪。
    /// 路径语法是 SVG 的，填充规则是 nonzero（Avalonia 侧要加 "F1 " 前缀）。
    | Path of d: string * scale: float * dx: float * dy: float * clipX: float * clipY: float * clipWidth: float * clipHeight: float

/// 一个排好的盒子：宽度，以及基线上下各占多少。
type MathBox =
    { width: float
      above: float
      below: float
      items: MathItem list }

type private Context =
    { size: float
      family: string
      fill: float
      centerStack: bool }

/// 把 KaTeX 的 HTML 盒模型摆成图元。纵向位置全部来自 KaTeX 算好的 em 数值，
/// 横向推进靠量尺给出的真实字形宽度。
type MathTypesetter(meter: IGlyphMeter) =

    // .katex { font-size: 1.21em }
    static let katexFontFactor = 1.21
    // .nulldelimiter { width: 0.12em }：CSS 常量，HTML 里不带
    static let nullDelimiterEm = 0.12

    static let hasClass name (classes: string list) = List.contains name classes

    // class → KaTeX 字族；字族名已含样式，斜体由字体本身提供
    static let applyFont (classes: string list) (ctx: Context) =
        let mutable ctx = ctx
        let face name = ctx <- { ctx with family = name }
        for cls in classes do
            match cls with
            | "mathnormal" -> face "KaTeX_MathItalic"
            | "mathit" -> face "KaTeX_MainItalic"
            | "mathrm" | "mainrm" | "text" -> face "KaTeX_Main"
            | "mathbf" -> face "KaTeX_MainBold"
            | "boldsymbol" -> face "KaTeX_MathBoldItalic"
            | "mathbb" | "amsrm" -> face "KaTeX_AMS"
            | "mathcal" -> face "KaTeX_Caligraphic"
            | "mathscr" -> face "KaTeX_Script"
            | "mathfrak" -> face "KaTeX_Fraktur"
            | "mathsf" -> face "KaTeX_SansSerif"
            | "mathtt" -> face "KaTeX_Typewriter"
            // \text{中文} 回落到正文字体
            | "cjk_fallback" -> face ""
            | "small-op" -> face "KaTeX_Size1"
            | "large-op" -> face "KaTeX_Size2"
            | _ -> ()
        // 可伸缩括号：delimsizing 里的 sizeN 指字族而非缩放
        if hasClass "delimsizing" classes then
            for cls in classes do
                match cls with
                | "size1" | "size2" | "size3" | "size4" -> face ("KaTeX_Size" + cls.Substring 4)
                | _ -> ()
        ctx

    static let empty = { width = 0.0; above = 0.0; below = 0.0; items = [] }

    static let shift (dx: float) (dy: float) (items: MathItem list) =
        if dx = 0.0 && dy = 0.0 then items
        else
            items
            |> List.map (function
                | MathItem.Glyph(t, f, s, x, y) -> MathItem.Glyph(t, f, s, x + dx, y + dy)
                | MathItem.Rule(x, y, w, h) -> MathItem.Rule(x + dx, y + dy, w, h)
                | MathItem.Path(d, s, tx, ty, cx, cy, cw, ch) -> MathItem.Path(d, s, tx + dx, ty + dy, cx + dx, cy + dy, cw, ch))

    // 横向依次摆放（inline-block：宽度含左右外边距）
    static let row (boxes: MathBox list) =
        let mutable x = 0.0
        let mutable above = 0.0
        let mutable below = 0.0
        let items = ResizeArray<MathItem>()
        for box in boxes do
            items.AddRange(shift x 0.0 box.items)
            x <- x + box.width
            above <- max above box.above
            below <- max below box.below
        { width = x; above = above; below = below; items = List.ofSeq items }

    static let childrenOf node =
        match node with
        | KElement(_, _, _, children) -> children
        | KText _ -> []

    static let classesOf node =
        match node with
        | KElement(_, classes, _, _) -> classes
        | KText _ -> []

    static let styleOf node =
        match node with
        | KElement(_, _, style, _) -> style
        | KText _ -> Map.empty

    static let findClass name nodes = nodes |> List.tryFind (fun n -> hasClass name (classesOf n))

    let rec layout (ctx: Context) (node: KNode) : MathBox =
        match node with
        | KText text ->
            // 零宽空格只是 vlist 的撑高用
            let cleaned = text.Replace("\u200b", "")
            if cleaned.Length = 0 then empty
            else
                let m = meter.Measure(cleaned, ctx.family, ctx.size)
                { width = m.width
                  above = m.baseline
                  below = max 0.0 (m.height - m.baseline)
                  items = [ MathItem.Glyph(cleaned, ctx.family, ctx.size, 0.0, -m.baseline) ] }
        | KElement(tag, classes, style, children) ->
            if hasClass "katex-mathml" classes || hasClass "vlist-s" classes then empty
            elif tag = "svg" then layoutSvg ctx style children
            elif tag = "path" then empty
            else

            let inner =
                let scaled = { ctx with size = ctx.size * KatexHtml.fontScaleOf classes }
                // mfrac / op-limits 与 vlist 之间可能隔着若干层，往下传，由 layoutVlist 清零
                let staged =
                    { scaled with
                        centerStack = ctx.centerStack || hasClass "mfrac" classes || hasClass "op-limits" classes }
                applyFont classes staged

            let em key = KatexHtml.emOf style key |> Option.map (fun v -> v * inner.size)

            let content =
                if hasClass "pstrut" classes then empty
                elif hasClass "katex-strut" classes then
                    let total = defaultArg (em "height") 0.0
                    let depth = defaultArg (em "vertical-align") 0.0 |> abs
                    { empty with above = total - depth; below = depth }
                elif hasClass "frac-line" classes || hasClass "overline-line" classes then
                    // 分数线／上划线撑满父盒；粗细来自 border-bottom-width
                    let thickness = defaultArg (em "border-bottom-width") 1.0 |> max 0.5
                    { width = inner.fill
                      above = thickness
                      below = 0.0
                      items = [ MathItem.Rule(0.0, -thickness, inner.fill, thickness) ] }
                elif hasClass "nulldelimiter" classes then
                    { empty with width = nullDelimiterEm * inner.size }
                elif hasClass "vlist-t" classes then
                    layoutVlist inner classes children
                elif hasClass "hide-tail" classes then
                    // 根号：SVG 被拉到整条根号宽度，右端长尾就是被开方式上方的横线
                    let width = max (defaultArg (em "min-width") 0.0) inner.fill
                    let height = defaultArg (em "height") 0.0
                    let laid = children |> List.map (layout { inner with fill = width }) |> row
                    { laid with width = width; above = height; below = 0.0 }
                elif List.isEmpty children then
                    // 纯占位
                    let width =
                        [ em "width"; em "min-width"; em "padding-left" ]
                        |> List.tryPick id
                        |> Option.defaultValue 0.0
                    { empty with width = width; above = defaultArg (em "height") 0.0 }
                else
                    let declared = em "width"
                    let padLeft = defaultArg (em "padding-left") 0.0
                    let laid =
                        children |> List.map (layout { inner with fill = defaultArg declared inner.fill }) |> row
                    let padded =
                        { laid with
                            width = padLeft + laid.width
                            items = shift padLeft 0.0 laid.items }
                    match declared with
                    | Some w -> { padded with width = w }
                    | None -> padded

            let marginLeft = defaultArg (em "margin-left") 0.0
            let marginRight = defaultArg (em "margin-right") 0.0
            { content with
                width = marginLeft + content.width + marginRight
                items = shift marginLeft 0.0 content.items }

    // vlist：零高块 + pstrut 垫片 + top 偏移；子块基线偏移 = pstrut.height + top
    and layoutVlist (ctx: Context) (classes: string list) (children: KNode list) : MathBox =
        let rows = children |> List.filter (fun n -> hasClass "vlist-r" (classesOf n))
        match rows |> List.tryHead |> Option.bind (fun r -> findClass "vlist" (childrenOf r)) with
        | None -> empty
        | Some vlist ->
            let above = defaultArg (KatexHtml.emOf (styleOf vlist) "height") 0.0 * ctx.size
            let below =
                if hasClass "vlist-t2" classes then
                    rows
                    |> List.tryItem 1
                    |> Option.bind (fun r -> findClass "vlist" (childrenOf r))
                    |> Option.bind (fun v -> KatexHtml.emOf (styleOf v) "height")
                    |> Option.defaultValue 0.0
                    |> (*) ctx.size
                else 0.0
            let stacked =
                childrenOf vlist
                |> List.filter (fun n -> not (hasClass "vlist-s" (classesOf n)))
                |> List.map (fun child ->
                    let style = styleOf child
                    let top = defaultArg (KatexHtml.emOf style "top") 0.0
                    let pstrut =
                        childrenOf child
                        |> findClass "pstrut"
                        |> Option.bind (fun p -> KatexHtml.emOf (styleOf p) "height")
                        |> Option.defaultValue 0.0
                    let body = childrenOf child |> List.filter (fun n -> not (hasClass "pstrut" (classesOf n)))
                    let margin = defaultArg (KatexHtml.emOf style "margin-right") 0.0 * ctx.size
                    (pstrut + top) * ctx.size, margin, body)
            // 先量宽度（fill 清零，否则撑满父盒的元素会把栏宽再撑大一轮），再按此宽度摆放
            let stackCtx = { ctx with centerStack = false; fill = 0.0 }
            let widthOf (body: KNode list) = (body |> List.map (layout stackCtx) |> row).width
            let contentWidth = stacked |> List.fold (fun acc (_, m, body) -> max acc (widthOf body + m)) 0.0
            let filled = { stackCtx with fill = contentWidth }
            let items =
                stacked
                |> List.collect (fun (dy, _, body) ->
                    let laid = body |> List.map (layout filled) |> row
                    // 分子分母、算符上下限相对整栏居中
                    let dx = if ctx.centerStack then (contentWidth - laid.width) / 2.0 else 0.0
                    shift dx dy laid.items)
            { width = contentWidth; above = above; below = below; items = items }

    // 根号与可伸缩括号：按 viewBox 等比缩放到声明高度，溢出由裁剪处理（xMinYMin slice）
    and layoutSvg (ctx: Context) (style: Map<string, string>) (children: KNode list) : MathBox =
        try
            let viewBox =
                match style.TryFind "-viewbox" with
                | Some raw ->
                    let parts =
                        raw.Split([| ' '; ',' |], StringSplitOptions.RemoveEmptyEntries)
                        |> Array.choose (fun p ->
                            match Double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture) with
                            | true, v -> Some v
                            | _ -> None)
                    if parts.Length = 4 then Some parts[3] else None
                | None -> None
            let height = defaultArg (KatexHtml.emOf style "height" |> Option.map ((*) ctx.size)) 0.0
            let path =
                children
                |> List.tryPick (function
                    | KElement("path", _, pathStyle, _) -> pathStyle.TryFind "-d"
                    | _ -> None)
            match viewBox, path with
            | Some vbHeight, Some d when vbHeight > 0.0 && height > 0.0 && ctx.fill > 0.0 ->
                { width = 0.0
                  above = height
                  below = 0.0
                  items = [ MathItem.Path(d, height / vbHeight, 0.0, -height, 0.0, -height, ctx.fill, height) ] }
            | _ -> { empty with above = height }
        with _ -> empty

    /// .katex 相对正文的字号倍率
    static member KatexFontFactor = katexFontFactor

    /// 排一整条公式；失败带原因返回。
    member _.TryRender(baseFontSize: float, html: string) : Result<MathBox, string> =
        try
            let nodes = KatexHtml.parse html
            match KatexHtml.visualSubtree nodes with
            | None -> Error "找不到 katex-html 子树"
            | Some visual ->
                let ctx =
                    { size = baseFontSize * katexFontFactor
                      family = "KaTeX_Main"
                      fill = 0.0
                      centerStack = false }
                let box = childrenOf visual |> List.map (layout ctx) |> row
                if box.width <= 0.0 || box.above + box.below <= 0.0 then
                    Error $"排版结果尺寸为零（w=%.2f{box.width} h=%.2f{box.above + box.below}）"
                else Ok box
        with ex -> Error $"{ex.GetType().Name}: {ex.Message}"

    member this.Render(baseFontSize: float, html: string) : MathBox option =
        match this.TryRender(baseFontSize, html) with
        | Ok box -> Some box
        | Error _ -> None
