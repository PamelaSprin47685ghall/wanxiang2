module MediaContentTests

open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Agent

[<Fact>]
let ``images go as data when the transport accepts them, otherwise a note explains why`` () =
    let load _ = Some [| 1uy; 2uy |]
    let sent = MediaContent.resolve MediaSupport.openAiCompatible load ("h", "image/png", "p.png", 2L)
    Assert.IsType<DataContent>(sent[1]) |> ignore
    let pdf = MediaContent.resolve MediaSupport.openAiCompatible load ("h", "application/pdf", "d.pdf", 2L)
    Assert.Contains("不接受该类型", (pdf.Head :?> TextContent).Text)

[<Fact>]
let ``missing blobs and text files`` () =
    let gone = MediaContent.resolve MediaSupport.gemini (fun _ -> None) ("h", "text/plain", "a.md", 1L)
    Assert.Contains("内容已丢失", (gone.Head :?> TextContent).Text)
    let txt = MediaContent.resolve MediaSupport.gemini (fun _ -> Some "x"B) ("h", "text/plain", "a.md", 1L)
    Assert.Equal("【附件】a.md\n```markdown\nx\n```", (txt.Head :?> TextContent).Text)
