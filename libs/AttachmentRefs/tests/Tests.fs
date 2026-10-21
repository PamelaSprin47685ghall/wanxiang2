module AttachmentRefsTests

open System.Text.Json.Nodes
open Xunit
open Wanxiang.Server

[<Fact>]
let ``attachment objects are found at any depth with defaults for missing fields`` () =
    let m =
        JsonNode.Parse
            """{"role":"user","contents":[{"type":"text","text":"x"},{"type":"attachment","sha256":"ab","mediaType":"image/png","fileName":"p.png","size":12},{"nested":{"type":"attachment","sha256":"cd","size":"big"}}]}"""

    Assert.Equal<(string * string * string * int64) list>(
        [ "ab", "image/png", "p.png", 12L; "cd", "", "", 0L ],
        AttachmentRefs.ofPayload m
    )

[<Fact>]
let ``nothing is found in plain messages or null`` () =
    Assert.Empty(AttachmentRefs.ofPayload (JsonNode.Parse """{"type":"text","contents":[]}"""))
    Assert.Empty(AttachmentRefs.ofPayload null)
