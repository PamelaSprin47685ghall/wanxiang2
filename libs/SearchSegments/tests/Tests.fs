module Tests

open System
open System.IO
open System.Text
open Xunit
open Wanxiang.UI

/// 金样由旧代码（从旧视图文件原样摘出的函数）在同一组输入上生成
let render () =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    let js (s: string) = Text.Json.JsonSerializer.Serialize s
    for q in Samples.queries do
        line (sprintf "terms %s -> %s" (js q) (SidebarText.terms q |> List.map js |> String.concat ","))
        for t in Samples.texts do
            line (sprintf "segments %s %s -> %s" (js q) (js t) (SidebarText.segments q t |> List.map (fun s -> sprintf "%s%s" (if s.isMatch then "*" else "") (js s.text)) |> String.concat ","))
    sb.ToString()

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "segments.txt"))

[<Fact>]
let ``golden: terms and segments match the old SidebarText`` () =
    Assert.Equal(expected (), render ())


// ---- 旧 SearchHighlightTests 第 1 节（去掉 Headless 包装）

[<Fact>]
let ``empty query yields one plain segment covering the whole text`` () =
    let spans = SidebarText.segments "" "万象 对话标题"
    Assert.Equal<string list>([ "万象 对话标题" ], spans |> List.map _.text)
    Assert.False(spans |> List.forall _.isMatch)

[<Fact>]
let ``whitespace-only query is treated as no query`` () =
    let spans = SidebarText.segments "   " "随便一段文本"
    Assert.Equal<string list>([ "随便一段文本" ], spans |> List.map _.text)
    Assert.False(spans |> List.forall _.isMatch)

[<Fact>]
let ``single term highlights only the matched substring, keeping original casing`` () =
    let spans = SidebarText.segments "模型" "万象 模型 设计草案"
    Assert.Equal<string list>([ "万象 "; "模型"; " 设计草案" ], spans |> List.map _.text)
    Assert.Equal<bool list>([ false; true; false ], spans |> List.map _.isMatch)

[<Fact>]
let ``segments always reassemble the original text exactly`` () =
    let text = "存储模型的方案讨论与回顾"
    for query in [ "存储"; "存储 方案"; "方案 存储"; "型"; "讨论 回顾"; "xyz"; "存储 存储" ] do
        let spans = SidebarText.segments query text
        Assert.Equal(text, spans |> List.map _.text |> String.concat "")

/// 旧用例同时断言 ConversationSummary.matches（写手 2 的模型）；这里只保留分段部分
[<Fact>]
let ``every term that matched the row is highlighted`` () =
    let highlighted =
        SidebarText.segments "模型 选型" "万象模型选型记录"
        |> List.filter _.isMatch
        |> List.map _.text
    Assert.Equal<string list>([ "模型"; "选型" ], highlighted)

[<Fact>]
let ``adjacent matches cover the whole text without gaps`` () =
    let spans = SidebarText.segments "存储 模型" "存储模型"
    Assert.Equal<string list>([ "存储"; "模型" ], spans |> List.map _.text)
    Assert.True(spans |> List.forall _.isMatch)
    Assert.Equal("存储模型", spans |> List.filter _.isMatch |> List.map _.text |> String.concat "")

[<Fact>]
let ``overlapping occurrences of one term are not double counted`` () =
    let spans = SidebarText.segments "aa" "aaaa"
    Assert.Equal("aaaa", spans |> List.map _.text |> String.concat "")
    Assert.Equal<int>(2, spans |> List.filter _.isMatch |> List.length)

[<Fact>]
let ``a term that does not occur leaves the text unstyled`` () =
    let spans = SidebarText.segments "不存在的词" "万象 对话"
    Assert.Equal<string list>([ "万象 对话" ], spans |> List.map _.text)
    Assert.False(spans |> List.forall _.isMatch)

[<Fact>]
let ``terms helper splits on spaces and tabs and drops duplicates`` () =
    Assert.Equal<string list>([ "甲"; "乙" ], SidebarText.terms "甲\t乙 甲")
    Assert.Equal<string list>([], SidebarText.terms "   ")

[<Fact>]
let ``null text is one empty plain segment`` () =
    Assert.Equal<SidebarText.Segment list>([ { SidebarText.Segment.text = ""; SidebarText.Segment.isMatch = false } ], SidebarText.segments "a" null)
