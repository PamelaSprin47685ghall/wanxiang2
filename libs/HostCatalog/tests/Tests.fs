module Tests

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由旧 Wanxiang.UI 程序集跑同一份 Render.fs 生成。
[<Fact>]
let ``golden: parse, selection and descriptions match old code`` () =
    let dir = Path.Combine(AppContext.BaseDirectory, "golden")
    let lines = File.ReadAllLines(Path.Combine(dir, "inputs.jsonl")) |> Array.filter ((<>) "") |> List.ofArray
    Assert.Equal(File.ReadAllText(Path.Combine(dir, "expected.txt")), Render.all lines)

// 迁自旧 UiPureLogicTests。
let private sampleCatalogJson () =
    let providers = JsonArray()
    let p1 = JsonObject()
    p1["id"] <- "openai"
    p1["label"] <- "OpenAI Official"
    p1["kind"] <- "openai"
    p1["baseUrl"] <- "https://api.openai.com/v1"
    p1["hasApiKey"] <- true
    let m1 = JsonArray()
    m1.Add("gpt-5")
    m1.Add("gpt-4o")
    p1["models"] <- m1
    p1["defaultModel"] <- "gpt-5"
    p1["timeoutSeconds"] <- 60
    p1["maxRetries"] <- 3
    p1["enabled"] <- true
    providers.Add(p1)

    let p2 = JsonObject()
    p2["id"] <- "disabled-prov"
    p2["label"] <- "Disabled Provider"
    p2["enabled"] <- false
    p2["models"] <- JsonArray()
    providers.Add(p2)

    let tools = JsonArray()
    let t1 = JsonObject()
    t1["id"] <- "builtin:web_search"
    t1["label"] <- "网页搜索"
    t1["description"] <- "搜索全网信息"
    t1["source"] <- "builtin"
    tools.Add(t1)

    let gen = JsonObject()
    gen["temperature"] <- 0.7
    gen["topP"] <- 0.95
    gen["maxTokens"] <- 4096
    gen["instructions"] <- "You are a helpful assistant."
    gen["maxContextMessages"] <- 50
    gen["autoTitle"] <- true
    gen["maxToolRounds"] <- 8
    let toolsObj = JsonObject()
    let roots = JsonArray()
    roots.Add("/home/user/workspace")
    toolsObj["fileReadRoots"] <- roots
    gen["tools"] <- toolsObj

    let mcpArr = JsonArray()
    let mcp1 = JsonObject()
    mcp1["id"] <- "fetch-server"
    mcp1["label"] <- "Fetch Server"
    mcp1["command"] <- "node"
    let mcpArgs = JsonArray()
    mcpArgs.Add("server.js")
    mcp1["args"] <- mcpArgs
    mcp1["callTimeoutSeconds"] <- 30
    mcp1["enabled"] <- true
    gen["mcpServers"] <- mcpArr

    Catalog.parse providers tools gen

[<Fact>]
let ``empty catalog defaults`` () =
    let empty = Catalog.empty
    Assert.False(Catalog.isReady empty)
    Assert.Empty(Catalog.usableProviders empty)
    Assert.True(Catalog.tryProvider "openai" empty |> Option.isNone)
    Assert.True(Catalog.defaultSelection empty |> Option.isNone)

[<Fact>]
let ``parse and query catalog properties`` () =
    let catalog = sampleCatalogJson ()
    Assert.True(Catalog.isReady catalog)
    Assert.Single(Catalog.usableProviders catalog) |> ignore

    let p = Catalog.tryProvider "openai" catalog
    Assert.True(p.IsSome)
    Assert.Equal("OpenAI Official", p.Value.label)

    let desc = Catalog.describeModel "openai" "gpt-5" catalog
    Assert.Equal("OpenAI Official · gpt-5", desc)

    let descFallback = Catalog.describeModel "unknown" "custom-model" catalog
    Assert.Equal("custom-model", descFallback)

    let descEmpty = Catalog.describeModel "unknown" "" catalog
    Assert.Equal("未选择模型", descEmpty)

    let defSel = Catalog.defaultSelection catalog
    Assert.Equal(Some("openai", "gpt-5"), defSel)

    Assert.Equal(Some 0.7, catalog.generation.temperature)
    Assert.Equal(Some 0.95, catalog.generation.topP)
    Assert.Equal(Some 4096, catalog.generation.maxTokens)
    Assert.Equal(Some "You are a helpful assistant.", catalog.generation.instructions)
    Assert.Equal(50, catalog.generation.maxContextMessages)
    Assert.Equal<string list>([ "/home/user/workspace" ], catalog.generation.fileReadRoots)

