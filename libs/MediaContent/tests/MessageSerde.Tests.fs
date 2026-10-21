module MessageSerdeTests

open System.Text.Json.Nodes
open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Agent

[<Fact>]
let ``loose client json keeps text and skips attachments, refs are extracted`` () =
    let node =
        JsonNode.Parse """{"role":"user","contents":[{"text":"hi"},{"type":"attachment","sha256":"AB","size":3}]}"""

    let msg = (MessageSerde.fromJsonNode node).Value
    Assert.Equal("hi", MessageSerde.textOf msg)
    Assert.Equal("user", MessageSerde.roleName msg)
    let r = List.exactlyOne (MessageSerde.attachmentRefs node)
    Assert.Equal("ab", r.sha256)
    Assert.Equal("application/octet-stream", r.mediaType)

[<Fact>]
let ``tagged messages round trip`` () =
    let m = MessageSerde.textMessage ChatRole.Assistant "x"
    let back = (MessageSerde.deserialize (MessageSerde.serialize m)).Value
    Assert.Equal("x", MessageSerde.textOf back)
    Assert.True((MessageSerde.fromJsonNode (JsonNode.Parse """{"role":"user","contents":[]}""")).IsNone)
