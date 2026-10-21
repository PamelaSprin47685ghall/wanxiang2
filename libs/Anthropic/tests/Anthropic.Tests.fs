module AnthropicTests

open System.Text.Json.Nodes
open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Agent

let texts (step: AnthropicStep) =
    match step with
    | AnthropicStep.Updates us ->
        [
            for u in us do
                for c in u.Contents do
                    match c with
                    | :? TextContent as t -> yield t.Text
                    | _ -> ()
        ]
    | _ -> []

[<Fact>]
let ``endpoint accepts base urls with or without v1, headers follow key and thinking`` () =
    Assert.Equal("https://a/v1/messages", AnthropicApi.endpoint "https://a/")
    Assert.Equal("https://a/v1/messages", AnthropicApi.endpoint "https://a/v1")
    Assert.Equal<(string * string) list>([ "anthropic-version", "2023-06-01" ], AnthropicApi.headers None 0)

    Assert.Equal<(string * string) list>(
        [
            "anthropic-version", "2023-06-01"
            "x-api-key", "k"
            "anthropic-beta", "interleaved-thinking-2025-05-14"
        ],
        AnthropicApi.headers (Some "k") 1
    )

[<Fact>]
let ``tool input split across deltas becomes one call when its block stops`` () =
    let s = AnthropicStream()

    s.Feed("", """{"type":"content_block_start","index":1,"content_block":{"type":"tool_use","id":"tu","name":"f"}}""")
    |> ignore

    s.Feed(
        "",
        """{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"a\":"}}"""
    )
    |> ignore

    s.Feed("", """{"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"1}"}}""")
    |> ignore

    match s.Feed("", """{"type":"content_block_stop","index":1}""") with
    | AnthropicStep.Updates [ u ] ->
        let call = u.Contents[0] :?> FunctionCallContent
        Assert.Equal("tu", call.CallId)
        Assert.Equal("1", string call.Arguments["a"])
    | other -> Assert.Fail(sprintf "%A" other)

[<Fact>]
let ``broken tool input drops the call instead of running the tool with partial arguments`` () =
    let s = AnthropicStream()

    s.Feed("", """{"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"tu","name":"f"}}""")
    |> ignore

    s.Feed(
        "",
        """{"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"a\":"}}"""
    )
    |> ignore

    Assert.Equal(AnthropicStep.Updates [], s.Feed("", """{"type":"content_block_stop","index":0}"""))

[<Fact>]
let ``event name is used when data has no type; errors and stop are reported`` () =
    let s = AnthropicStream()

    Assert.Equal<string list>(
        [ "hi" ],
        texts (s.Feed("content_block_delta", """{"index":0,"delta":{"type":"text_delta","text":"hi"}}"""))
    )

    Assert.Equal(AnthropicStep.Updates [], s.Feed("x", "not json"))
    Assert.Equal(AnthropicStep.Failed "overloaded", s.Feed("", """{"type":"error","error":{"message":"overloaded"}}"""))
    Assert.Equal(AnthropicStep.Stopped, s.Feed("", """{"type":"message_stop"}"""))

[<Fact>]
let ``closing update carries usage and the mapped finish reason`` () =
    let s = AnthropicStream()

    s.Feed("", """{"type":"message_start","message":{"id":"m1","usage":{"input_tokens":3,"output_tokens":1}}}""")
    |> ignore

    s.Feed("", """{"type":"message_delta","delta":{"stop_reason":"max_tokens"},"usage":{"output_tokens":9}}""")
    |> ignore

    let c = s.Closing()
    Assert.Equal("m1", c.ResponseId)
    Assert.Equal(System.Nullable(ChatFinishReason.Length), c.FinishReason)
    let usage = (c.Contents[0] :?> UsageContent).Details
    Assert.Equal(System.Nullable 12L, usage.TotalTokenCount)

[<Fact>]
let ``request body: system is top level, max tokens is raised above the thinking budget`` () =
    let body =
        AnthropicRequest.build
            "m"
            [ ChatMessage(ChatRole.System, "s"); ChatMessage(ChatRole.User, "u") ]
            null
            true
            4096
            false

    Assert.Equal(4096 + AnthropicRequest.DefaultMaxTokens, body["max_tokens"].GetValue<int>())
    let first = body["system"].AsArray()[0]
    Assert.Equal("s", first["text"].GetValue<string>())
    Assert.Equal(1, body["messages"].AsArray().Count)
