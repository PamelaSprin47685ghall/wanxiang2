module McpProtocolTests

open System.Text.Json.Nodes
open Xunit
open Wanxiang.Server

[<Fact>]
let ``initialize carries the single protocol version and client info`` () =
    Assert.Equal("""{"protocolVersion":"2025-06-18","capabilities":{"roots":{}},"clientInfo":{"name":"wanxiang","version":"1.0"}}""", (McpProtocol.initializeParams ()).ToJsonString())

[<Fact>]
let ``tools without a name are dropped; non-object schema counts as none`` () =
    let tools = McpProtocol.parseTools (JsonNode.Parse """{"tools":[{"name":"a","inputSchema":"x"},{"description":"d"},{"name":"b","description":"乙","inputSchema":{"type":"object"}},3]}""")
    Assert.Equal<string list>([ "a"; "b" ], tools |> List.map _.name)
    Assert.True(tools.Head.inputSchema.IsNone)
    Assert.Equal("乙", tools[1].description)
    Assert.Empty(McpProtocol.parseTools (JsonNode.Parse "[]"))

[<Fact>]
let ``arguments: blank and null are empty objects, broken JSON is refused`` () =
    Assert.Equal("{}", (McpProtocol.parseArguments " ").Value.ToJsonString())
    Assert.Equal("{}", (McpProtocol.parseArguments "null").Value.ToJsonString())
    Assert.True((McpProtocol.parseArguments "{oops").IsNone)

[<Fact>]
let ``call results keep Chinese readable and wrap errors`` () =
    Assert.Equal("""{"t":"好"}""", McpProtocol.renderCallResult (Ok(JsonNode.Parse """{"t":"好"}""")))
    Assert.Equal("""{"error":{"m":"坏"}}""", McpProtocol.renderCallResult (Error """{"m":"坏"}"""))
    Assert.Equal("""{"error":"纯文本"}""", McpProtocol.renderCallResult (Error "纯文本"))
