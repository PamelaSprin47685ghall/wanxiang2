module GeminiTests

open System.Text.Json
open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Agent

[<Fact>]
let ``endpoint adds v1beta only when no version is given; key goes in a header`` () =
    Assert.Equal("https://g/v1beta/models/m:streamGenerateContent?alt=sse", GeminiApi.endpoint "https://g/" "m")
    Assert.Equal("https://g/v1/models/m:streamGenerateContent?alt=sse", GeminiApi.endpoint "https://g/v1" "m")
    Assert.Equal<(string * string) list>([ "x-goog-api-key", "k" ], GeminiApi.headers (Some "k"))
    Assert.Empty(GeminiApi.headers (Some " "))

[<Fact>]
let ``call ids are made from name and index and give the name back`` () =
    let id = GeminiRequest.makeCallId "weatherTool" 3
    Assert.Equal("weatherTool@3", id)
    Assert.Equal("weatherTool", GeminiRequest.nameOfCallId id)
    Assert.Equal("custom", GeminiRequest.nameOfCallId "custom")
    Assert.Equal("", GeminiRequest.nameOfCallId null)

// SPEC 4：跨提供方历史里的 id 不含名字时，functionResponse.name 取同一份请求里那次调用的名字
[<Fact>]
let ``functionResponse takes the name from the call in the same request when the id has none`` () =
    let foreign =
        [ ChatMessage(ChatRole.Assistant, [| TextContent "查一下" :> AIContent; FunctionCallContent("call1", "search", null) :> AIContent |])
          ChatMessage(ChatRole.Tool, [| FunctionResultContent("call1", box "{\"ok\":true}") :> AIContent |]) ]
    Assert.Contains("\"functionResponse\":{\"name\":\"search\"", (GeminiRequest.build foreign null 0).ToJsonString())
    // Gemini 自己的「名字@序号」仍然解析出名字（此时两者一致）
    let own =
        [ ChatMessage(ChatRole.Assistant, [| FunctionCallContent("search@0", "search", null) :> AIContent |])
          ChatMessage(ChatRole.Tool, [| FunctionResultContent("search@0", box "ok") :> AIContent |]) ]
    Assert.Contains("\"functionResponse\":{\"name\":\"search\"", (GeminiRequest.build own null 0).ToJsonString())
    // 都找不到（历史里没有那次调用）才退回解析 id：保持旧行为
    let orphan = [ ChatMessage(ChatRole.Tool, [| FunctionResultContent("legacy-call", box "ok") :> AIContent |]) ]
    Assert.Contains("\"functionResponse\":{\"name\":\"legacy-call\"", (GeminiRequest.build orphan null 0).ToJsonString())

[<Fact>]
let ``thought parts are reasoning, calls are numbered in arrival order`` () =
    let s = GeminiStream()
    match s.Feed """{"candidates":[{"content":{"parts":[{"text":"t","thought":true},{"functionCall":{"name":"f","args":{}}},{"functionCall":{"name":"f"}}]}}]}""" with
    | GeminiStep.Updates [ a; b; c ] ->
        Assert.IsType<TextReasoningContent>(a.Contents[0]) |> ignore
        Assert.Equal("f@0", (b.Contents[0] :?> FunctionCallContent).CallId)
        Assert.Equal("f@1", (c.Contents[0] :?> FunctionCallContent).CallId)
    | other -> Assert.Fail(sprintf "%A" other)

[<Fact>]
let ``error object in a 200 stream is a failure; malformed data throws`` () =
    let s = GeminiStream()
    Assert.Equal(GeminiStep.Failed "quota", s.Feed """{"error":{"message":"quota"}}""")
    Assert.ThrowsAny<JsonException>(fun () -> s.Feed "not json" |> ignore) |> ignore

[<Fact>]
let ``total tokens fall back to prompt plus output when upstream omits it`` () =
    let s = GeminiStream()
    s.Feed """{"usageMetadata":{"promptTokenCount":2,"candidatesTokenCount":5}}""" |> ignore
    let usage = (s.Closing().Contents[0] :?> UsageContent).Details
    Assert.Equal(System.Nullable 7L, usage.TotalTokenCount)
