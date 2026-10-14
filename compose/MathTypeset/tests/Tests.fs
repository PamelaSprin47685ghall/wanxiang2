module Tests

open Xunit
open Wanxiang.UI
open Replay

let private layout name =
    let c = cases |> List.find (fun c -> c.name = name)
    match typesetter.TryRender(14.5, c.html) with
    | Ok box -> box
    | Error e -> failwith e

let private glyphs (box: MathBox) =
    box.items |> List.choose (function MathItem.Glyph(t, fam, s, x, y) -> Some(t, fam, s, x, y) | _ -> None)

let private widthOf (t, fam, s, _, _) = ((ReplayMeter() :> IGlyphMeter).Measure(t, fam, s)).width

[<Fact>]
let ``golden: every case matches the old MathLayout`` () =
    for c in cases do
        Assert.Equal(c.expected, render c.html)

// ---- 旧 MathRenderTests（改为回放量尺，不再需要 Avalonia）

[<Fact>]
let ``数字与运算符用直立的 KaTeX_Main`` () =
    let upright = layout "summation" |> glyphs |> List.filter (fun (t, _, _, _, _) -> List.contains t [ "1"; "2"; "="; "+" ])
    Assert.NotEmpty upright
    for (_, fam, _, _, _) in upright do Assert.Equal("KaTeX_Main", fam)

[<Fact>]
let ``变量用斜体的数学字体`` () =
    let italics = layout "quadratic" |> glyphs |> List.filter (fun (t, _, _, _, _) -> List.contains t [ "a"; "b"; "c" ])
    Assert.NotEmpty italics
    for (_, fam, _, _, _) in italics do Assert.Equal("KaTeX_MathItalic", fam)

[<Fact>]
let ``大型算符取 Size 系列字体`` () =
    match layout "summation" |> glyphs |> List.tryFind (fun (t, _, _, _, _) -> t = "\u2211") with
    | Some(_, fam, _, _, _) -> Assert.StartsWith("KaTeX_Size", fam)
    | None -> failwith "没有找到 ∑ 字形"

[<Fact>]
let ``减号与正负号落在有这些字形的字体上`` () =
    let signs = layout "quadratic" |> glyphs |> List.filter (fun (t, _, _, _, _) -> t = "\u2212" || t = "\u00B1")
    Assert.Equal(3, signs.Length)
    for (_, fam, _, _, _) in signs do Assert.Equal("KaTeX_Main", fam)

[<Fact>]
let ``分子分母与分数线共享同一条中轴`` () =
    let box = layout "quadratic"
    let (bx, bw) = box.items |> List.choose (function MathItem.Rule(x, _, w, _) -> Some(x, w) | _ -> None) |> List.maxBy snd
    let denominator = glyphs box |> List.filter (fun (_, _, _, _, y) -> y > -10.0)
    Assert.NotEmpty denominator
    let left = denominator |> List.map (fun (_, _, _, x, _) -> x) |> List.min
    let right = denominator |> List.map (fun g -> let (_, _, _, x, _) = g in x + widthOf g) |> List.max
    Assert.True(abs ((bx + bw / 2.0) - (left + right) / 2.0) < 1.5)

[<Fact>]
let ``上下限居中于大型算符`` () =
    let gs = layout "summation" |> glyphs
    let sigma = gs |> List.find (fun (t, _, _, _, _) -> t = "\u2211")
    let (_, _, _, sx, sy) = sigma
    let sw = widthOf sigma
    for upper in [ true; false ] do
        let band =
            gs |> List.filter (fun g ->
                let (_, _, _, x, y) = g
                x + widthOf g <= sx + sw + 8.0 && (if upper then y < sy else y > sy))
        Assert.NotEmpty band
        let left = band |> List.map (fun (_, _, _, x, _) -> x) |> List.min
        let right = band |> List.map (fun g -> let (_, _, _, x, _) = g in x + widthOf g) |> List.max
        Assert.True(abs ((left + right) / 2.0 - (sx + sw / 2.0)) < 1.5)

[<Fact>]
let ``根号画成 SVG 且保留上方横线`` () =
    match (layout "quadratic").items |> List.choose (function MathItem.Path(d, _, _, _, _, _, cw, _) -> Some(d, cw) | _ -> None) with
    | [] -> failwith "根号没有生成 SVG 图元"
    | (d, clipWidth) :: _ ->
        // 末段 h400000 就是被开方式上方的横线
        Assert.Contains("400000", d)
        Assert.True(clipWidth > 0.0)

// ---- 旧 UiPureLogicTests 第 6 节

[<Fact>]
let ``katexFontFactor constant value is verified`` () =
    Assert.Equal(1.21, MathTypesetter.KatexFontFactor)

[<Fact>]
let ``MathBox structure can hold items and baselines`` () =
    let box = { width = 100.0; above = 20.0; below = 10.0; items = [] }
    Assert.Equal(100.0, box.width)
    Assert.Equal(20.0, box.above)
    Assert.Equal(10.0, box.below)
    Assert.Empty box.items

// ---- 边界与失败路径

/// 每个字符宽 10、基线 8、高 10 的假量尺，记下被量过的字族
type FixedMeter() =
    member val Families = ResizeArray<string>()
    interface IGlyphMeter with
        member this.Measure(text, family, _) =
            this.Families.Add family
            { width = 10.0 * float text.Length; baseline = 8.0; height = 10.0 }

[<Fact>]
let ``html without a katex-html subtree is an error with a reason`` () =
    match MathTypesetter(FixedMeter()).TryRender(14.5, "<span>plain</span>") with
    | Error e -> Assert.Contains("katex-html", e)
    | Ok _ -> failwith "should fail"

[<Fact>]
let ``meter failure becomes an error naming the exception`` () =
    let broken = { new IGlyphMeter with member _.Measure(_, _, _) = invalidOp "no font" }
    let html = (cases |> List.find (fun c -> c.name = "inline-emc2")).html
    match MathTypesetter(broken).TryRender(14.5, html) with
    | Error e -> Assert.Contains("InvalidOperationException: no font", e)
    | Ok _ -> failwith "should fail"
    Assert.True((MathTypesetter(broken).Render(14.5, html)).IsNone)

[<Fact>]
let ``zero width-space only text produces no glyph and no measurement`` () =
    let meter = FixedMeter()
    let html = """<span class="katex"><span class="katex-html"><span class="katex-strut" style="height:1em;vertical-align:-0.25em;"></span><span class="mord">&#8203;x</span></span></span>"""
    match MathTypesetter(meter).TryRender(10.0, html) with
    | Ok box ->
        Assert.Equal(10.0, box.width)
        Assert.Equal<string list>([ "x" ], box.items |> List.choose (function MathItem.Glyph(t, _, _, _, _) -> Some t | _ -> None))
        Assert.Equal(1, meter.Families.Count)
    | Error e -> failwith e

[<Fact>]
let ``cjk fallback asks the meter for the body font`` () =
    let meter = FixedMeter()
    let html = """<span class="katex"><span class="katex-html"><span class="mord text"><span class="mord cjk_fallback">中</span></span></span></span>"""
    match MathTypesetter(meter).TryRender(10.0, html) with
    | Ok _ -> Assert.Equal<string list>([ "" ], List.ofSeq meter.Families)
    | Error e -> failwith e

[<Fact>]
let ``margins widen the box and shift the content`` () =
    let html = """<span class="katex"><span class="katex-html"><span class="mord" style="margin-left:1em;margin-right:0.5em;">x</span></span></span>"""
    match MathTypesetter(FixedMeter()).TryRender(10.0, html) with
    | Ok box ->
        let size = 10.0 * MathTypesetter.KatexFontFactor
        Assert.Equal(size * 1.5 + 10.0, box.width, 9)
        match box.items with
        | [ MathItem.Glyph(_, _, _, x, y) ] ->
            Assert.Equal(size, x, 9)
            Assert.Equal(-8.0, y)
        | other -> failwithf "%A" other
    | Error e -> failwith e
