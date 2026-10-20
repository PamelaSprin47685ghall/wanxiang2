module Tests

open System
open Xunit
open Wanxiang.UI
open KaTeXSource

// 坐标约定：x 向右为正，y 向下为正；盒基线在 y = 0。
// 字形基线偏移 baseline < 0 表示在盒基线之上，> 0 表示在之下。

let private typesetter = MathTypesetter(Meter())

let private layout tex display =
    match typesetter.TryRender(14.5, html tex display) with
    | Ok box -> box
    | Error e -> failwithf "%s：%s" tex e

let private tryLayout tex display = typesetter.TryRender(14.5, html tex display)

// ---------------------------------------------------------------------------
// 基线与整体盒子
// ---------------------------------------------------------------------------

[<Fact>]
let ``行内公式的主体字形落在盒基线上`` () =
    for (tex, display) in [ "x^2", false; "E = mc^2", false; "a", false ] do
        for g in mainGlyphs (layout tex display) do
            Assert.True(abs g.baseline < 1e-6, sprintf "%s：主体字形 %s 的基线偏移是 %f" tex g.text g.baseline)

[<Fact>]
let ``所有字形的墨迹都落在盒宽之内`` () =
    for (tex, display) in
        [ "x^2", false
          "E = mc^2", false
          "\\frac{a}{b}", false
          "\\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}", true
          "\\sqrt{x^2+y^2}", false
          "\\sum_{i=1}^{n} i = \\frac{n(n+1)}{2}", true
          "\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}", true
          "\\int_0^\\infty e^{-x}\\,dx", true ] do
        let box = layout tex display
        for g in glyphsOf box do
            Assert.True(g.x >= -1e-6, sprintf "%s：字形 %s 的左边缘 %f 越过了盒左端" tex g.text g.x)
            Assert.True(g.x + g.width <= box.width + 1e-6, sprintf "%s：字形 %s 的右边缘 %f 越过了盒宽 %f" tex g.text (g.x + g.width) box.width)

[<Fact>]
let ``同一行的字形自左向右排列`` () =
    for (tex, display) in [ "E = mc^2", false; "a+b+c", false; "\\text{中文} + 1", false ] do
        let xs = glyphsOf (layout tex display) |> List.map (fun g -> g.x)
        for i in 1 .. xs.Length - 1 do
            Assert.True(xs.[i] >= xs.[i - 1] - 1e-6, sprintf "%s：第 %d 个字形往左退了" tex i)

[<Fact>]
let ``盒宽不小于任何字形的右边缘`` () =
    let box = layout "E = mc^2" false
    let right = glyphsOf box |> List.map (fun g -> g.x + g.width) |> List.max
    Assert.True(right <= box.width + 1e-6)
    Assert.True(box.width > 0.0)
    Assert.True(box.above > 0.0 && box.below > 0.0)

// ---------------------------------------------------------------------------
// 上下标
// ---------------------------------------------------------------------------

[<Fact>]
let ``上标基线高于基行基线且起点不早于基字右边缘`` () =
    let box = layout "x^2" false
    let baseGlyph = glyphWith box "x" 0
    let sup = glyphWith box "2" 0
    Assert.True(sup.baseline < baseGlyph.baseline, sprintf "上标基线 %f 不高于基行 %f" sup.baseline baseGlyph.baseline)
    Assert.True(sup.x >= baseGlyph.x + baseGlyph.width - 1e-6, "上标没有落在基字右侧")

[<Fact>]
let ``上标字号小于基字字号`` () =
    let box = layout "x^2" false
    let baseGlyph = glyphWith box "x" 0
    let sup = glyphWith box "2" 0
    Assert.True(sup.size < baseGlyph.size, sprintf "上标字号 %f 不小于基字 %f" sup.size baseGlyph.size)

[<Fact>]
let ``下标基线低于基行基线且起点不早于基字右边缘`` () =
    let box = layout "\\alpha_i^2" false
    let baseGlyph = glyphWith box "α" 0
    let sub = glyphWith box "i" 0
    Assert.True(sub.baseline > baseGlyph.baseline, sprintf "下标基线 %f 不低于基行 %f" sub.baseline baseGlyph.baseline)
    Assert.True(sub.x >= baseGlyph.x + baseGlyph.width - 1e-6, "下标没有落在基字右侧")
    Assert.True(sub.size < baseGlyph.size)

[<Fact>]
let ``展示式求和的上下限分居算符上下`` () =
    let box = layout "\\sum_{i=1}^{n}" true
    let sigma = glyphWith box "∑" 0
    let gs = glyphsOf box
    let upper = gs |> List.filter (fun g -> g.baseline < sigma.baseline)
    let lower = gs |> List.filter (fun g -> g.baseline > sigma.baseline)
    Assert.NotEmpty(upper)
    Assert.NotEmpty(lower)
    let sigmaCenter = sigma.x + sigma.width / 2.0
    for group in [ upper; lower ] do
        let left = group |> List.map (fun g -> g.x) |> List.min
        let right = group |> List.map (fun g -> g.x + g.width) |> List.max
        Assert.True(abs ((left + right) / 2.0 - sigmaCenter) < 1e-6, "上下限没有与算符居中对齐")

[<Fact>]
let ``行内求和的上下限挂在算符右侧`` () =
    let box = layout "\\sum_{i=1}^{n}" false
    let sigma = glyphWith box "∑" 0
    let upper = glyphWith box "n" 0
    let lower = glyphWith box "i" 0
    let sigmaRight = sigma.x + sigma.width
    Assert.True(upper.x >= sigmaRight - 1e-6, "行内上限没有挂在算符右侧")
    Assert.True(lower.x >= sigmaRight - 1e-6, "行内下限没有挂在算符右侧")
    Assert.True(upper.baseline < sigma.baseline, "行内上限没有抬到基线之上")
    Assert.True(lower.baseline > sigma.baseline, "行内下限没有落到基线之下")

[<Fact>]
let ``大型算符用 Size 系列字体`` () =
    let box = layout "\\sum_{i=1}^{n} i" true
    let sigma = glyphWith box "∑" 0
    Assert.StartsWith("KaTeX_Size", sigma.family)

// ---------------------------------------------------------------------------
// 分数
// ---------------------------------------------------------------------------

[<Fact>]
let ``分数的分子高于分数线且分母低于分数线`` () =
    for (tex, display) in [ "\\frac{a}{b}", false; "\\frac{1}{2}", false; "\\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}", true ] do
        let box = layout tex display
        let (_, ruleY, _, ruleH) = rulesOf box |> List.head
        let ruleBottom = ruleY + ruleH
        let gs = glyphsOf box
        let numerator = gs |> List.filter (fun g -> g.baseline < ruleY)
        let denominator = gs |> List.filter (fun g -> g.baseline > ruleBottom)
        Assert.NotEmpty(numerator)
        Assert.NotEmpty(denominator)
        Assert.True(gs |> List.forall (fun g -> g.baseline < ruleY || g.baseline > ruleBottom), sprintf "%s：有字形压在分数线上" tex)

[<Fact>]
let ``分数线横跨分子分母`` () =
    for (tex, display) in [ "\\frac{a}{b}", false; "\\frac{1}{2}", false; "\\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}", true ] do
        let box = layout tex display
        let (ruleX, _, ruleW, _) = rulesOf box |> List.head
        let gs = glyphsOf box
        let left = gs |> List.map (fun g -> g.x) |> List.min
        let right = gs |> List.map (fun g -> g.x + g.width) |> List.max
        Assert.True(ruleX <= left + 1e-6, sprintf "%s：分数线没有覆盖分子的左边缘" tex)
        Assert.True(ruleX + ruleW >= right - 1e-6, sprintf "%s：分数线没有覆盖分子的右边缘" tex)

[<Fact>]
let ``分子分母与分数线共享同一条中轴`` () =
    for (tex, display) in [ "\\frac{a}{b}", false; "\\frac{1}{2}", false; "\\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}", true ] do
        let box = layout tex display
        let (ruleX, ruleY, ruleW, ruleH) = rulesOf box |> List.head
        let ruleBottom = ruleY + ruleH
        let center = ruleX + ruleW / 2.0
        let gs = glyphsOf box
        for row in [ gs |> List.filter (fun g -> g.baseline < ruleY); gs |> List.filter (fun g -> g.baseline > ruleBottom) ] do
            let rowLeft = row |> List.map (fun g -> g.x) |> List.min
            let rowRight = row |> List.map (fun g -> g.x + g.width) |> List.max
            Assert.True(abs ((rowLeft + rowRight) / 2.0 - center) < 1e-6, sprintf "%s：分子分母没有与分数线同轴" tex)

[<Fact>]
let ``分数里的字形字号小于同一符号单排时的字号`` () =
    let plain = layout "a" false
    let plainSize = (glyphWith plain "a" 0).size
    let box = layout "\\frac{a}{b}" false
    for g in glyphsOf box do
        Assert.True(g.size < plainSize, sprintf "分数里的 %s 字号 %f 没有小于单排的 %f" g.text g.size plainSize)

// ---------------------------------------------------------------------------
// 根式
// ---------------------------------------------------------------------------

[<Fact>]
let ``根式生成 SVG 路径且裁剪宽度等于盒宽`` () =
    let box = layout "\\sqrt{x^2+y^2}" false
    match pathsOf box with
    | [] -> failwith "根号没有生成 SVG 图元"
    | (_, scale, _, _, clipX, clipY, clipW, clipH) :: _ ->
        Assert.True(scale > 0.0, "根号路径的缩放比例不是正数")
        Assert.True(abs (clipW - box.width) < 1e-6, sprintf "根号的裁剪宽度 %f 与盒宽 %f 不一致" clipW box.width)
        Assert.True(clipH > 0.0)
        Assert.True(abs clipX < 1e-6)
        Assert.True(clipY < 0.0, "根号的裁剪矩形没有向上延伸")

[<Fact>]
let ``被开方内容位于根号符号右侧且落在裁剪区内`` () =
    let box = layout "\\sqrt{x^2+y^2}" false
    let (_, _, _, _, clipX, _, clipW, _) = pathsOf box |> List.head
    for g in glyphsOf box do
        Assert.True(g.x > clipX + 1e-6, sprintf "被开方的 %s 压在根号符号上" g.text)
        Assert.True(g.x + g.width <= clipX + clipW + 1e-6, sprintf "被开方的 %s 超出了根号的裁剪区" g.text)

[<Fact>]
let ``根号路径保留上方横线`` () =
    let box = layout "\\sqrt{x^2+y^2}" false
    let (d, _, _, _, _, _, _, _) = pathsOf box |> List.head
    Assert.Contains("400000", d)

// ---------------------------------------------------------------------------
// 积分与矩阵
// ---------------------------------------------------------------------------

[<Fact>]
let ``积分的下限在基线下且上限在基线上`` () =
    let box = layout "\\int_0^\\infty e^{-x}\\,dx" true
    let integral = glyphWith box "∫" 0
    let lower = glyphWith box "0" 0
    let upper = glyphWith box "∞" 0
    Assert.True(lower.baseline > integral.baseline, "积分下限没有落到基线之下")
    Assert.True(upper.baseline < integral.baseline, "积分上限没有抬到基线之上")
    Assert.True(lower.x >= integral.x + integral.width - 1e-6, "积分下限没有挂在积分号右侧")
    Assert.True(upper.x >= integral.x + integral.width - 1e-6, "积分上限没有挂在积分号右侧")

[<Fact>]
let ``矩阵同列的字形左边缘相同`` () =
    let box = layout "\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}" true
    let a = glyphWith box "a" 0
    let c = glyphWith box "c" 0
    let b = glyphWith box "b" 0
    let d = glyphWith box "d" 0
    Assert.True(abs (a.x - c.x) < 1e-6, "矩阵第一列没有对齐")
    Assert.True(abs (b.x - d.x) < 1e-6, "矩阵第二列没有对齐")
    Assert.True(b.x > a.x + a.width - 1e-6, "矩阵第二列没有排在第一列右边")

[<Fact>]
let ``矩阵上行的基线高于下行`` () =
    let box = layout "\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}" true
    let a = glyphWith box "a" 0
    let c = glyphWith box "c" 0
    Assert.True(a.baseline < c.baseline, sprintf "矩阵上行基线 %f 不高于下行 %f" a.baseline c.baseline)

[<Fact>]
let ``矩阵的可伸缩括号用 Size 系列字体`` () =
    let box = layout "\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}" true
    Assert.StartsWith("KaTeX_Size", (glyphWith box "(" 0).family)
    Assert.StartsWith("KaTeX_Size", (glyphWith box ")" 0).family)

// ---------------------------------------------------------------------------
// 字族
// ---------------------------------------------------------------------------

[<Fact>]
let ``数字与运算符用直立的 KaTeX_Main`` () =
    let box = layout "E = mc^2" false
    let upright = glyphsOf box |> List.filter (fun g -> g.text = "=" || g.text = "2")
    Assert.NotEmpty(upright)
    for g in upright do Assert.Equal("KaTeX_Main", g.family)

[<Fact>]
let ``变量用斜体的数学字体`` () =
    let box = layout "E = mc^2" false
    let italics = glyphsOf box |> List.filter (fun g -> List.contains g.text [ "E"; "m"; "c" ])
    Assert.NotEmpty(italics)
    for g in italics do Assert.Equal("KaTeX_MathItalic", g.family)

[<Fact>]
let ``减号与正负号落在有这些字形的字体上`` () =
    let box = layout "\\frac{-b \\pm \\sqrt{b^2-4ac}}{2a}" true
    let signs = glyphsOf box |> List.filter (fun g -> g.text = "−" || g.text = "±")
    Assert.Equal(3, signs.Length)
    for g in signs do Assert.Equal("KaTeX_Main", g.family)

[<Fact>]
let ``CJK 文本回落到正文字体`` () =
    let box = layout "\\text{中文} + 1" false
    let cjk = glyphsOf box |> List.filter (fun g -> g.text = "中文")
    Assert.Equal(1, cjk.Length)
    Assert.Equal("", cjk.[0].family)
    Assert.Equal("KaTeX_Main", (glyphsOf box |> List.find (fun g -> g.text = "+")).family)

// ---------------------------------------------------------------------------
// 行内与展示式
// ---------------------------------------------------------------------------

[<Fact>]
let ``展示式的基线上方留白不少于行内式`` () =
    for tex in [ "x^2"; "E = mc^2"; "\\sqrt{x}" ] do
        let inlineBox = layout tex false
        let display = layout tex true
        Assert.True(display.above >= inlineBox.above - 1e-6, sprintf "%s：展示式的上方留白 %f 小于行内式 %f" tex display.above inlineBox.above)
        Assert.True(display.above + display.below >= inlineBox.above + inlineBox.below - 1e-6, sprintf "%s：展示式比行内式矮" tex)

[<Fact>]
let ``展示式与行内式的主体字形落在同一条基线上`` () =
    for tex in [ "x^2"; "E = mc^2" ] do
        for g in mainGlyphs (layout tex false) do Assert.True(abs g.baseline < 1e-6, sprintf "%s：行内主体 %s 不在基线上" tex g.text)
        for g in mainGlyphs (layout tex true) do Assert.True(abs g.baseline < 1e-6, sprintf "%s：展示主体 %s 不在基线上" tex g.text)

[<Fact>]
let ``展示式与行内式的字形内容一致`` () =
    for tex in [ "x^2"; "E = mc^2"; "\\frac{a}{b}" ] do
        Assert.Equal<string list>(textOf (layout tex false), textOf (layout tex true))

// ---------------------------------------------------------------------------
// 边界与失败路径
// ---------------------------------------------------------------------------

[<Fact>]
let ``空公式报错说明排版结果尺寸为零`` () =
    match tryLayout "" false with
    | Error e -> Assert.Contains("尺寸为零", e)
    | Ok _ -> failwith "空公式不该排出盒子"

[<Fact>]
let ``语法错误的公式没有可排版的子树`` () =
    match tryLayout "\\frac{" false with
    | Error e -> Assert.Contains("katex-html", e)
    | Ok _ -> failwith "语法错误的公式不该排出盒子"

[<Fact>]
let ``未定义的宏按原文排出来`` () =
    let box = layout "\\undefinedmacro{x}" false
    let texts = textOf box
    Assert.Contains("\\undefinedmacro", texts)
    Assert.Contains("x", texts)
    Assert.Equal("KaTeX_Main", (glyphsOf box |> List.find (fun g -> g.text = "\\undefinedmacro")).family)

[<Fact>]
let ``长公式的宽度随项数单调增加`` () =
    let widthOf n = layout (String.concat "+" [ for i in 1 .. n -> sprintf "a%d" i ]) false |> fun b -> b.width
    let narrow = widthOf 3
    let wide = widthOf 12
    Assert.True(wide > narrow, sprintf "项数变多后宽度没有变大：%f vs %f" wide narrow)

[<Fact>]
let ``长公式的字形都落在盒宽之内`` () =
    let tex = String.concat "+" [ for i in 1 .. 24 -> sprintf "a%d" i ]
    let box = layout tex false
    for g in glyphsOf box do
        Assert.True(g.x + g.width <= box.width + 1e-6, sprintf "字形 %s 超出了盒宽 %f" g.text box.width)

[<Fact>]
let ``html without a katex-html subtree is an error with a reason`` () =
    match MathTypesetter(Meter()).TryRender(14.5, "<span>plain</span>") with
    | Error e -> Assert.Contains("katex-html", e)
    | Ok _ -> failwith "should fail"

/// 每个字符宽 10、基线 8、高 10 的假量尺，记下被量过的字族
type FixedMeter() =
    member val Families = ResizeArray<string>()
    interface IGlyphMeter with
        member this.Measure(text, family, _) =
            this.Families.Add family
            { width = 10.0 * float text.Length; baseline = 8.0; height = 10.0 }

[<Fact>]
let ``meter failure becomes an error naming the exception`` () =
    let broken = { new IGlyphMeter with member _.Measure(_, _, _) = invalidOp "no font" }
    let markup = html "E = mc^2" false
    match MathTypesetter(broken).TryRender(14.5, markup) with
    | Error e -> Assert.Contains("InvalidOperationException: no font", e)
    | Ok _ -> failwith "should fail"
    Assert.True((MathTypesetter(broken).Render(14.5, markup)).IsNone)

[<Fact>]
let ``zero width-space only text produces no glyph and no measurement`` () =
    let meter = FixedMeter()
    let markup = """<span class="katex"><span class="katex-html"><span class="katex-strut" style="height:1em;vertical-align:-0.25em;"></span><span class="mord">&#8203;x</span></span></span>"""
    match MathTypesetter(meter).TryRender(10.0, markup) with
    | Ok box ->
        Assert.Equal(10.0, box.width)
        Assert.Equal<string list>([ "x" ], box.items |> List.choose (function MathItem.Glyph(t, _, _, _, _) -> Some t | _ -> None))
        Assert.Equal(1, meter.Families.Count)
    | Error e -> failwith e

[<Fact>]
let ``cjk fallback asks the meter for the body font`` () =
    let meter = FixedMeter()
    let markup = """<span class="katex"><span class="katex-html"><span class="mord text"><span class="mord cjk_fallback">中</span></span></span></span>"""
    match MathTypesetter(meter).TryRender(10.0, markup) with
    | Ok _ -> Assert.Equal<string list>([ "" ], List.ofSeq meter.Families)
    | Error e -> failwith e

[<Fact>]
let ``margins widen the box and shift the content`` () =
    let markup = """<span class="katex"><span class="katex-html"><span class="mord" style="margin-left:1em;margin-right:0.5em;">x</span></span></span>"""
    match MathTypesetter(Meter()).TryRender(10.0, markup) with
    | Ok box ->
        let size = 10.0 * MathTypesetter.KatexFontFactor
        let glyphWidth = advanceOf "x" "KaTeX_Main" size
        Assert.Equal(size * 1.5 + glyphWidth, box.width, 9)
        match box.items with
        | [ MathItem.Glyph(_, _, _, x, y) ] ->
            Assert.Equal(size, x, 9)
            Assert.Equal(-0.72 * size, y, 9)
        | other -> failwithf "%A" other
    | Error e -> failwith e

// ---------------------------------------------------------------------------
// 公开契约
// ---------------------------------------------------------------------------

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
