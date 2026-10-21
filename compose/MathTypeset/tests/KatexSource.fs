module KaTeXSource

open Wanxiang.App
open Wanxiang.UI

/// 公式的 KaTeX HTML 由生产用的脚本后端现场产出（与宿主同一条路）：
/// 测试因此直接对真实的 KaTeX 输出断言排版事实，而不是对着一份记好的快照。
let private backend = lazy (JintRichBackend())

let html (tex: string) (display: bool) =
    match (backend.Value).MathHtml(tex, display) with
    | Some html -> html
    | None -> failwithf "KaTeX 没有给出 HTML：%s" tex

/// 确定性量尺：宽度按字族与字符给出稳定的推进量，基线与总高只随字号变。
///
/// 公式的纵向位置全部来自 KaTeX 算好的 em 数值，量尺只影响横向推进与墨迹高度，
/// 因此测试断言的都是几何事实，而不是某套字体的度量快照。
let advanceOf (text: string) (family: string) (size: float) =
    let unitWidth =
        match family with
        | "" -> 1.00
        | "KaTeX_Main" -> 0.60
        | "KaTeX_MathItalic" -> 0.55
        | f when f.StartsWith "KaTeX_Size" -> 0.68
        | _ -> 0.58

    let spread = text |> Seq.sumBy (fun c -> 0.90 + 0.05 * float (int c % 5)) |> float
    spread * unitWidth * size

type Meter() =
    interface IGlyphMeter with
        member _.Measure(text, family, size) =
            {
                width = advanceOf text family size
                baseline = 0.72 * size
                height = 0.98 * size
            }

/// 一个字形的排版事实。坐标相对盒基线左端，y 向下为正。
type Glyph =
    {
        text: string
        family: string
        size: float
        x: float
        /// 字形左边缘相对盒基线的偏移（向下为正）。
        top: float
        width: float
        /// 字形基线相对盒基线的偏移（向下为正，负值表示在基线之上）。
        baseline: float
    }

let glyphsOf (box: MathBox) =
    box.items
    |> List.choose (fun item ->
        match item with
        | MathItem.Glyph(text, family, size, x, y) ->
            let m = (Meter() :> IGlyphMeter).Measure(text, family, size)

            Some
                {
                    text = text
                    family = family
                    size = size
                    x = x
                    top = y
                    width = m.width
                    baseline = y + m.baseline
                }
        | _ -> None)

let rulesOf (box: MathBox) =
    box.items
    |> List.choose (fun item ->
        match item with
        | MathItem.Rule(x, y, w, h) -> Some(x, y, w, h)
        | _ -> None)

let pathsOf (box: MathBox) =
    box.items
    |> List.choose (fun item ->
        match item with
        | MathItem.Path(d, scale, dx, dy, cx, cy, cw, ch) -> Some(d, scale, dx, dy, cx, cy, cw, ch)
        | _ -> None)

/// 盒里最大字号的那一层：公式的主体（分母、上下限、括号内容都更小）。
let mainGlyphs (box: MathBox) =
    let all = glyphsOf box
    let top = all |> List.map (fun g -> g.size) |> List.max
    all |> List.filter (fun g -> g.size = top)

let textOf (box: MathBox) =
    glyphsOf box |> List.map (fun g -> g.text)

let glyphWith (box: MathBox) text index =
    glyphsOf box |> List.filter (fun g -> g.text = text) |> List.item index
