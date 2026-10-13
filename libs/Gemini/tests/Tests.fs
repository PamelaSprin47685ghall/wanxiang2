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
