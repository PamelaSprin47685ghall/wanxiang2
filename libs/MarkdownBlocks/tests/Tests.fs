module Tests

open System
open System.IO
open System.Text
open Xunit
open Wanxiang.UI

let private js (s: string) = Text.Json.JsonSerializer.Serialize s

let private render () =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    for md in Samples.markdown do
        line ("== markdown " + js md)
        let blocks = Markdown.parse md
        line (sprintf "%A" blocks)
        line ("codeBlocks: " + (Markdown.codeBlocks blocks |> List.map js |> String.concat ","))
        for b in blocks do
            match b with
            | MdParagraph items | MdHeading(_, items) -> line (sprintf "hasLongToken: %b" (Markdown.hasLongToken items))
            | MdTable(h, rows) -> for c in h @ List.concat rows do line (sprintf "cell hasLongToken: %b" (Markdown.hasLongToken c))
            | _ -> ()
    for t in Samples.longText do
        line (sprintf "== long %s %b %b" (js t) (Markdown.hasLongToken [ MdText(t, false, false, false, false) ]) (Markdown.hasLongToken [ MdLink(t, "u") ]))
    for (alt, url) in Samples.images do
        let a, b = Markdown.imagePlaceholder alt url
        line (sprintf "== image %s %s" (js alt) (js url))
        line ("text: " + a)
        line ("notice: " + b)
    let full = String.concat "\n\n" Samples.markdown
    let mutable prev = []
    for cut in [ 0; 10; 40; 80; 160; 320; full.Length ] do
        let blocks = Markdown.parse (full.Substring(0, min cut full.Length))
        line (sprintf "== stream %d prefix=%d blocks=%d" cut (Markdown.commonPrefix prev blocks) blocks.Length)
        prev <- blocks
    sb.ToString()

[<Fact>]
let ``empty input has no blocks; blank input falls back to its raw text`` () =
    Assert.Empty(Markdown.parse "")
    Assert.Empty(Markdown.parse null)
    // 解析不出任何块时整段原文作为一个段落，不让消息「消失」
    Assert.Equal<MdBlock list>([ MdParagraph [ MdText("   \n\n", false, false, false, false) ] ], Markdown.parse "   \n\n")

[<Fact>]
let ``code blocks inside quotes are collected`` () =
    let blocks = Markdown.parse "> ```\n> q\n> ```\n\n```\nt\n```"
    Assert.Equal<string list>([ "q"; "t" ], Markdown.codeBlocks blocks |> List.map (fun s -> s.Trim()))

[<Fact>]
let ``long token threshold is 33 unbroken characters`` () =
    Assert.False(Markdown.hasLongToken [ MdText(String('x', 32), false, false, false, false) ])
    Assert.True(Markdown.hasLongToken [ MdText(String('x', 33), false, false, false, false) ])
    Assert.False(Markdown.hasLongToken [ MdText(String('x', 20) + "\u200B" + String('x', 20), false, false, false, false) ])
    Assert.False(Markdown.hasLongToken [ MdMath(String('x', 50), false); MdBreak ])

[<Fact>]
let ``common prefix stops at the first changed block`` () =
    let a = Markdown.parse "one\n\ntwo\n\nthree"
    let b = Markdown.parse "one\n\ntwo!\n\nthree"
    Assert.Equal(1, Markdown.commonPrefix a b)
    Assert.Equal(3, Markdown.commonPrefix a a)
    Assert.Equal(0, Markdown.commonPrefix [] a)

[<Fact>]
let ``image placeholder names the host and keeps the full url in the notice`` () =
    let text, notice = Markdown.imagePlaceholder "" "https://img.example.com/x.png?t=1"
    Assert.Equal("[图片未加载：远程图片 · img.example.com]", text)
    Assert.Equal("为保护隐私，默认不加载远程图片。来源：https://img.example.com/x.png?t=1", notice)
    let text2, _ = Markdown.imagePlaceholder "图" "relative.png"
    Assert.Equal("[图片未加载：图 · relative.png]", text2)

// ---- 旧 UiPureLogicTests 第 3 节（原样）

[<Fact>]
let ``parse should parse headings, paragraphs, lists, and codes`` () =
    let md = """# 标题 1
这是一段包含 **粗体** 与 *斜体* 与 `代码` 的正文。

- 列表项 1
- 列表项 2

```fsharp
let x = 42
```
"""
    let blocks = Markdown.parse md
    Assert.True(blocks.Length >= 4)
    match blocks[0] with
    | MdHeading(1, inlines) ->
        Assert.NotEmpty(inlines)
    | other -> Assert.Fail(sprintf "期望 MdHeading，实际是 %A" other)

    let codes = Markdown.codeBlocks blocks
    Assert.Single(codes) |> ignore
    Assert.Contains("let x = 42", codes[0])

[<Fact>]
let ``parse math inlines and math blocks`` () =
    let md = """行内公式 $E = mc^2$ 以及独立公式块：

$$
\int_0^\infty e^{-x} dx = 1
$$
"""
    let blocks = Markdown.parse md
    Assert.NotEmpty(blocks)
    let hasMathInline =
        blocks
        |> List.exists (function
            | MdParagraph inlines ->
                inlines |> List.exists (function MdMath(tex, _) -> tex.Contains("mc^2") | _ -> false)
            | _ -> false)
    Assert.True(hasMathInline, "段落中应当解析出行内公式")

    let hasMathBlock =
        blocks
        |> List.exists (function
            | MdMathBlock tex -> tex.Contains("\int")
            | _ -> false)
    Assert.True(hasMathBlock, "应当解析出独占行的公式块")

[<Fact>]
let ``parse empty string returns empty list`` () =
    Assert.Empty(Markdown.parse "")
    Assert.Empty(Markdown.parse null)
