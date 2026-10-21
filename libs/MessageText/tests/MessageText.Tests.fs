module MessageTextTests

open System.Text.Json.Nodes
open Xunit
open Wanxiang.Server

[<Fact>]
let ``text items are joined by newline; reasoning and tool parts are skipped`` () =
    let m =
        JsonNode.Parse
            """{"role":"assistant","contents":[{"$type":"text","text":"一"},{"$type":"reasoning","text":"想"},{"$type":"functionCall","name":"f"},{"type":"reasoning","text":"x"},{"$type":"text","text":"二"}]}"""

    Assert.Equal("一\n二", MessageText.visible 100 m)

[<Fact>]
let ``top level text and arrays are read; output is capped`` () =
    Assert.Equal("ab", MessageText.visible 2 (JsonNode.Parse """[{"text":"abc"},{"text":"d"}]"""))
    Assert.Equal("", MessageText.visible 10 null)
