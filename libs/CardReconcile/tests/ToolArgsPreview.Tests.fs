module ToolArgsPreviewTests
open System
open System.IO
open Xunit
open Wanxiang.UI


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
