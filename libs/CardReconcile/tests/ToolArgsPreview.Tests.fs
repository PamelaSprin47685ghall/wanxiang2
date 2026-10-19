module ToolArgsPreviewTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "ToolArgsPreview", "toolargspreview.txt"))

/// 金样由旧代码（从 旧 Views/MessageCard.fs 的 prettyFormatJson 与 formatToolArgsPreview 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), ToolArgsPreviewRender.render ())

[<Fact>]
let ``empty object reads as no arguments`` () =
    Assert.Equal((0, "无参数"), ToolArgsPreview.preview "{}")

[<Fact>]
let ``long previews end with an ellipsis at 96 chars`` () =
    let _, p = ToolArgsPreview.preview (String.replicate 200 "z")
    Assert.Equal(96, p.Length)
    Assert.EndsWith("…", p)

[<Fact>]
let ``non json is returned unchanged by prettyJson`` () =
    Assert.Equal("{broken", ToolArgsPreview.prettyJson "{broken")
