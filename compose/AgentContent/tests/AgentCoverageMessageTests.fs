namespace Wanxiang.Tests

// 原样迁自旧仓库 d9651ed tests/Wanxiang.Tests/AgentCoverageTests.fs（只改了模块名与 open）。

open System
open System.Collections.Generic
open System.IO
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Net.Sockets
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Agent
open Wanxiang.Config
open Wanxiang.Core

module AgentCoverageMessageTests =

    // =========================================================================
    // 辅助工具与 Mock 实现
    // =========================================================================

    type FakeHttpMessageHandler(handler: HttpRequestMessage -> HttpResponseMessage) =
        inherit HttpMessageHandler()
        override _.SendAsync(request, cancellationToken) =
            Task.FromResult(handler request)

    type AsyncFakeHttpMessageHandler(handler: HttpRequestMessage -> CancellationToken -> Task<HttpResponseMessage>) =
        inherit HttpMessageHandler()
        override _.SendAsync(request, cancellationToken) =
            handler request cancellationToken

    let dummyProvider kind : ProviderConfig = {
        id = "test-provider"
        kind = kind
        label = "Test Provider"
        baseUrl = "https://api.example.com/v1"
        apiKey = Some "test-key"
        defaultModel = "default-model"
        models = [ "model-a"; "model-b" ]
        timeoutSeconds = 30
        maxRetries = 2
        enabled = true
        promptCaching = false
        headers = Map.empty
        extraJson = None; visionOverrides = Map.empty
    }

    let dummySession : SessionConfig = {
        provider = "test-provider"
        model = "model-a"
        instructions = Some "System prompt instructions"
        tools = []
        temperature = Some 0.7
        topP = Some 0.9
        maxTokens = Some 1024
        thinkingBudget = Some 0
        extraJson = None
    }

    // =========================================================================
    // 2. MessageSerde 序列化、反序列化与边界输入测试
    // =========================================================================

    [<Fact>]
    let ``MessageSerde roleName converts all ChatRole cases`` () =
        Assert.Equal("user", MessageSerde.roleName (ChatMessage(ChatRole.User, "u")))
        Assert.Equal("assistant", MessageSerde.roleName (ChatMessage(ChatRole.Assistant, "a")))
        Assert.Equal("system", MessageSerde.roleName (ChatMessage(ChatRole.System, "s")))
        Assert.Equal("tool", MessageSerde.roleName (ChatMessage(ChatRole.Tool, "t")))
        let customRole = ChatRole("custom")
        Assert.Equal("custom", MessageSerde.roleName (ChatMessage(customRole, "c")))

    [<Fact>]
    let ``MessageSerde textMessage and toolResultMessage helper creation`` () =
        let userMsg = MessageSerde.textMessage ChatRole.User "Hello, world!"
        Assert.Equal(ChatRole.User, userMsg.Role)
        Assert.Equal("Hello, world!", MessageSerde.textOf userMsg)

        let fnCall = FunctionCallContent("call_123", "calc", dict [ ("x", box 1) ])
        let toolMsg = MessageSerde.toolResultMessage fnCall """{"result":"ok"}"""
        Assert.Equal(ChatRole.Tool, toolMsg.Role)
        Assert.Equal(1, toolMsg.Contents.Count)
        let resContent = toolMsg.Contents.[0] :?> FunctionResultContent
        Assert.Equal("call_123", resContent.CallId)
        Assert.Equal("""{"result":"ok"}""", resContent.Result :?> string)

    [<Fact>]
    let ``MessageSerde hasToolCall and toolCalls detection`` () =
        let plainMsg = MessageSerde.textMessage ChatRole.Assistant "I have no tools."
        Assert.False(MessageSerde.hasToolCall plainMsg)
        Assert.Empty(MessageSerde.toolCalls plainMsg)

        let toolCall = FunctionCallContent("call_abc", "calc", dict [ ("x", box 42) ])
        let toolCallMsg = ChatMessage(ChatRole.Assistant, [| toolCall :> AIContent |])
        Assert.True(MessageSerde.hasToolCall toolCallMsg)
        let calls = MessageSerde.toolCalls toolCallMsg
        Assert.Equal(1, calls.Length)
        Assert.Equal("call_abc", calls.[0].CallId)
        Assert.Equal("calc", calls.[0].Name)

    [<Fact>]
    let ``MessageSerde attachmentRefs extraction with various object structures`` () =
        // 1. None if no contents
        let emptyNode = JsonObject()
        Assert.Empty(MessageSerde.attachmentRefs emptyNode)

        // 2. Array of contents with valid and invalid attachments
        let contentsArr = JsonArray()

        let validAtt = JsonObject()
        validAtt.["type"] <- "attachment"
        validAtt.["sha256"] <- "ABCD1234EF"
        validAtt.["mediaType"] <- "image/png"
        validAtt.["fileName"] <- "test.png"
        validAtt.["size"] <- 1024L
        contentsArr.Add validAtt

        let nonAtt = JsonObject()
        nonAtt.["type"] <- "text"
        nonAtt.["text"] <- "not an attachment"
        contentsArr.Add nonAtt

        let nodeWithContents = JsonObject()
        nodeWithContents.["contents"] <- contentsArr

        let refs = MessageSerde.attachmentRefs nodeWithContents
        Assert.Equal(1, refs.Length)
        Assert.Equal("abcd1234ef", refs.[0].sha256)
        Assert.Equal("image/png", refs.[0].mediaType)
        Assert.Equal("test.png", refs.[0].fileName)
        Assert.Equal(1024L, refs.[0].size)

    [<Fact>]
    let ``MessageSerde fromJsonNode and fromJsonNodeWith loose parsing and empty contents behavior`` () =
        // 1. Standard serialized ChatMessage
        let orig = ChatMessage(ChatRole.Assistant, "Direct assistant message")
        let serialized = MessageSerde.serialize orig
        let deserialized = MessageSerde.deserialize serialized |> Option.get
        Assert.Equal("Direct assistant message", MessageSerde.textOf deserialized)

        // 2. Loose JSON from client
        let clientJson = """
        {
            "role": "assistant",
            "contents": [
                { "text": "Loose assistant content" }
            ]
        }
        """
        let node = JsonNode.Parse clientJson
        let parsedOpt = MessageSerde.fromJsonNodeWith true node
        Assert.True(parsedOpt.IsSome)
        let parsed = parsedOpt.Value
        Assert.Equal(ChatRole.Assistant, parsed.Role)
        Assert.Equal("Loose assistant content", MessageSerde.textOf parsed)


    // =========================================================================
    // MessageSerde 扩展分支测试（基于公开 API）
    // =========================================================================

    [<Fact>]
    let ``MessageSerde attachmentRefs extracts attachment items and parses sizes`` () =
        let root = JsonObject()
        let arr = JsonArray()

        let item1 = JsonObject()
        item1.["type"] <- "attachment"
        item1.["sha256"] <- "ABCDEF1234567890"
        item1.["mediaType"] <- "image/png"
        item1.["fileName"] <- "pic.png"
        item1.["size"] <- 1024L
        arr.Add item1

        let item2 = JsonObject()
        item2.["type"] <- "attachment"
        item2.["sha256"] <- "FEEDCAFE"
        arr.Add item2

        let item3 = JsonObject()
        item3.["type"] <- "attachment"
        item3.["sha256"] <- "aabbccdd"
        item3.["size"] <- "not-a-number"
        arr.Add item3

        let item4 = JsonObject()
        item4.["type"] <- "attachment"
        item4.["sha256"] <- "   "
        arr.Add item4

        let item5 = JsonObject()
        item5.["type"] <- "text"
        item5.["text"] <- "not an attachment"
        arr.Add item5

        arr.Add(JsonValue.Create "plain-string")

        root.["contents"] <- arr

        let refs = MessageSerde.attachmentRefs root
        Assert.Equal(3, refs.Length)

        Assert.Equal("abcdef1234567890", refs.[0].sha256)
        Assert.Equal("image/png", refs.[0].mediaType)
        Assert.Equal("pic.png", refs.[0].fileName)
        Assert.Equal(1024L, refs.[0].size)

        Assert.Equal("feedcafe", refs.[1].sha256)
        Assert.Equal("application/octet-stream", refs.[1].mediaType)
        Assert.Equal("FEEDCAFE", refs.[1].fileName)
        Assert.Equal(0L, refs.[1].size)

        Assert.Equal("aabbccdd", refs.[2].sha256)
        Assert.Equal(0L, refs.[2].size)

        Assert.Empty(MessageSerde.attachmentRefs (JsonObject()))
        Assert.Empty(MessageSerde.attachmentRefs (JsonValue.Create "scalar"))

    [<Fact>]
    let ``MessageSerde roleName and message helpers`` () =
        Assert.Equal("system", MessageSerde.roleName (ChatMessage(ChatRole.System, "test")))
        Assert.Equal("tool", MessageSerde.roleName (ChatMessage(ChatRole.Tool, "test")))
        Assert.Equal("assistant", MessageSerde.roleName (ChatMessage(ChatRole.Assistant, "test")))
        Assert.Equal("user", MessageSerde.roleName (ChatMessage(ChatRole.User, "test")))
        Assert.Equal("customRole", MessageSerde.roleName (ChatMessage(ChatRole "customRole", "test")))

        let call = FunctionCallContent("call_123", "calculator", Dictionary<string, obj>())
        let toolMsg = MessageSerde.toolResultMessage call "{\"result\": 42}"
        Assert.Equal(ChatRole.Tool, toolMsg.Role)
        let resContent = Assert.IsType<FunctionResultContent>(Assert.Single(toolMsg.Contents))
        Assert.Equal("call_123", resContent.CallId)
        Assert.Equal("{\"result\": 42}", resContent.Result.ToString())

        let textMsg = MessageSerde.textMessage ChatRole.Assistant "hello assistant"
        Assert.Equal(ChatRole.Assistant, textMsg.Role)
        Assert.Equal("hello assistant", MessageSerde.textOf textMsg)

    [<Fact>]
    let ``MessageSerde hasToolCall and toolCalls detect function calls`` () =
        let msg = ChatMessage(ChatRole.Assistant, "")
        msg.Contents.Add(FunctionCallContent("fn_1", "calc", Dictionary<string, obj>()))
        msg.Contents.Add(TextContent("calling fn"))

        Assert.True(MessageSerde.hasToolCall msg)
        let calls = MessageSerde.toolCalls msg
        Assert.Single(calls) |> ignore
        Assert.Equal("fn_1", calls.[0].CallId)
        Assert.Equal("calc", calls.[0].Name)

    [<Fact>]
    let ``MessageSerde fromJsonNodeWith parses untagged, tagged, and attachment contents`` () =
        let makeUntaggedMsg (role: string) (contents: JsonNode list) =
            let o = JsonObject()
            if not (String.IsNullOrEmpty role) then o.["role"] <- role
            let arr = JsonArray()
            for c in contents do arr.Add c
            o.["contents"] <- arr
            o :> JsonNode

        let msgAssistant = MessageSerde.fromJsonNodeWith true (makeUntaggedMsg "assistant" [])
        Assert.Equal(ChatRole.Assistant, msgAssistant.Value.Role)

        let msgSystem = MessageSerde.fromJsonNodeWith true (makeUntaggedMsg "system" [])
        Assert.Equal(ChatRole.System, msgSystem.Value.Role)

        let msgTool = MessageSerde.fromJsonNodeWith true (makeUntaggedMsg "tool" [])
        Assert.Equal(ChatRole.Tool, msgTool.Value.Role)

        let msgUser = MessageSerde.fromJsonNodeWith true (makeUntaggedMsg "user" [])
        Assert.Equal(ChatRole.User, msgUser.Value.Role)

        let msgUnknown = MessageSerde.fromJsonNodeWith true (makeUntaggedMsg "other" [])
        Assert.Equal(ChatRole "other", msgUnknown.Value.Role)

        let msgNoRole = MessageSerde.fromJsonNodeWith true (makeUntaggedMsg null [])
        Assert.Equal(ChatRole.User, msgNoRole.Value.Role)

        let textItem = JsonObject()
        textItem.["text"] <- "some text"

        let emptyTextItem = JsonObject()
        emptyTextItem.["text"] <- ""

        let attItem = JsonObject()
        attItem.["type"] <- "attachment"
        attItem.["sha256"] <- "123"

        let nonObjItem = JsonValue.Create 42

        let complexMsgNode = makeUntaggedMsg "user" [ textItem :> JsonNode; emptyTextItem :> JsonNode; attItem :> JsonNode; nonObjItem :> JsonNode ]
        let parsed = MessageSerde.fromJsonNodeWith false complexMsgNode
        Assert.True(parsed.IsSome)
        Assert.Single(parsed.Value.Contents) |> ignore
        Assert.Equal("some text", MessageSerde.textOf parsed.Value)

        let emptyNode = makeUntaggedMsg "user" []
        Assert.True(MessageSerde.fromJsonNodeWith false emptyNode |> Option.isNone)
        Assert.True(MessageSerde.fromJsonNodeWith true emptyNode |> Option.isSome)
        Assert.True(MessageSerde.fromJsonNode (JsonValue.Create "scalar") |> Option.isNone)


    // =========================================================================
    // AttachmentContent 边界与契约测试
    // =========================================================================

    [<Fact>]
    let ``MediaSupport ofProviderKind assigns correct capabilities`` () =
        let anthropic = MediaSupport.ofProviderKind "anthropic"
        Assert.True(anthropic.image)
        Assert.True(anthropic.pdf)
        Assert.False(anthropic.audio)

        let gemini = MediaSupport.ofProviderKind "gemini"
        Assert.True(gemini.image)
        Assert.True(gemini.pdf)
        Assert.True(gemini.audio)

        let openai = MediaSupport.ofProviderKind "openai"
        Assert.True(openai.image)
        Assert.False(openai.pdf)
        Assert.False(openai.audio)

        let other = MediaSupport.ofProviderKind "custom-llm"
        Assert.True(other.image)
        Assert.False(other.pdf)
        Assert.False(other.audio)

    [<Theory>]
    [<InlineData("image/png", true)>]
    [<InlineData("IMAGE/JPEG", true)>]
    [<InlineData("image/webp", true)>]
    [<InlineData("image/gif", true)>]
    [<InlineData("text/plain", false)>]
    let ``AttachmentContent isImage correctly classifies media`` (mediaType: string, expected: bool) =
        Assert.Equal(expected, AttachmentContent.isImage mediaType)

    [<Theory>]
    [<InlineData("application/pdf", true)>]
    [<InlineData("APPLICATION/PDF", true)>]
    [<InlineData("text/plain", false)>]
    let ``AttachmentContent isPdf correctly classifies media`` (mediaType: string, expected: bool) =
        Assert.Equal(expected, AttachmentContent.isPdf mediaType)

    [<Theory>]
    [<InlineData("audio/mp3", true)>]
    [<InlineData("audio/wav", true)>]
    [<InlineData("audio/ogg", true)>]
    [<InlineData("video/mp4", false)>]
    let ``AttachmentContent isAudio correctly classifies media`` (mediaType: string, expected: bool) =
        Assert.Equal(expected, AttachmentContent.isAudio mediaType)

    [<Theory>]
    [<InlineData("text/plain", true)>]
    [<InlineData("application/json", true)>]
    [<InlineData("application/xml", true)>]
    [<InlineData("application/x-yaml", true)>]
    [<InlineData("application/javascript", true)>]
    [<InlineData("image/png", false)>]
    let ``AttachmentContent isText correctly classifies media`` (mediaType: string, expected: bool) =
        Assert.Equal(expected, AttachmentContent.isText mediaType)

    [<Fact>]
    let ``AttachmentContent resolve handles missing blob`` () =
        let ref = { sha256 = "abc"; mediaType = "text/plain"; fileName = "hello.txt"; size = 10L }
        let support = MediaSupport.ofProviderKind "anthropic"
        let contents = AttachmentContent.resolve support (fun _ -> None) ref
        Assert.Single(contents) |> ignore
        match contents.[0] with
        | :? TextContent as tc -> Assert.Contains("内容已丢失", tc.Text)
        | _ -> Assert.Fail("Expected TextContent")

    [<Fact>]
    let ``AttachmentContent resolve handles empty blob`` () =
        let ref = { sha256 = "abc"; mediaType = "text/plain"; fileName = "empty.txt"; size = 0L }
        let support = MediaSupport.ofProviderKind "anthropic"
        let contents = AttachmentContent.resolve support (fun _ -> Some [||]) ref
        Assert.Single(contents) |> ignore
        match contents.[0] with
        | :? TextContent as tc -> Assert.Contains("空文件", tc.Text)
        | _ -> Assert.Fail("Expected TextContent")

    [<Fact>]
    let ``AttachmentContent resolve handles normal text file`` () =
        let textBytes = Encoding.UTF8.GetBytes "let x = 42"
        let ref = { sha256 = "abc"; mediaType = "text/plain"; fileName = "test.fs"; size = int64 textBytes.Length }
        let support = MediaSupport.ofProviderKind "anthropic"
        let contents = AttachmentContent.resolve support (fun _ -> Some textBytes) ref
        Assert.Single(contents) |> ignore
        match contents.[0] with
        | :? TextContent as tc ->
            Assert.Contains("```fsharp", tc.Text)
            Assert.Contains("let x = 42", tc.Text)
        | _ -> Assert.Fail("Expected TextContent")

    [<Fact>]
    let ``AttachmentContent resolve handles truncated text file`` () =
        let largeText = String('a', 130 * 1024)
        let textBytes = Encoding.UTF8.GetBytes largeText
        let ref = { sha256 = "abc"; mediaType = "text/plain"; fileName = "large.txt"; size = int64 textBytes.Length }
        let support = MediaSupport.ofProviderKind "anthropic"
        let contents = AttachmentContent.resolve support (fun _ -> Some textBytes) ref
        Assert.Single(contents) |> ignore
        match contents.[0] with
        | :? TextContent as tc -> Assert.Contains("已截断", tc.Text)
        | _ -> Assert.Fail("Expected TextContent")

    [<Fact>]
    let ``AttachmentContent resolve handles supported image`` () =
        let imgBytes = [| 0uy; 1uy; 2uy; 3uy |]
        let ref = { sha256 = "abc"; mediaType = "image/png"; fileName = "pic.png"; size = 4L }
        let support = MediaSupport.ofProviderKind "anthropic"
        let contents = AttachmentContent.resolve support (fun _ -> Some imgBytes) ref
        Assert.Equal(2, contents.Length)
        match contents.[0], contents.[1] with
        | (:? TextContent as tc), (:? DataContent as dc) ->
            Assert.Contains("附件图片", tc.Text)
            Assert.Equal("image/png", dc.MediaType)
            Assert.Equal<byte>(imgBytes, dc.Data.ToArray())
        | _ -> Assert.Fail("Expected TextContent then DataContent")

    [<Fact>]
    let ``AttachmentContent resolve handles unsupported or oversize media`` () =
        let imgBytes = [| 1uy; 2uy |]
        let refImg = { sha256 = "abc"; mediaType = "image/png"; fileName = "pic.png"; size = 2L }
        let noSupport = { MediaSupport.image = false; pdf = false; audio = false }
        let contents = AttachmentContent.resolve noSupport (fun _ -> Some imgBytes) refImg
        Assert.Single(contents) |> ignore
        match contents.[0] with
        | :? TextContent as tc -> Assert.Contains("当前模型服务的传输不接受该类型", tc.Text)
        | _ -> Assert.Fail("Expected TextContent")

        // 超过大小限制
        let hugeImgBytes = Array.zeroCreate (9 * 1024 * 1024)
        let refHuge = { sha256 = "huge"; mediaType = "image/png"; fileName = "huge.png"; size = int64 hugeImgBytes.Length }
        let contentsHuge = AttachmentContent.resolve (MediaSupport.ofProviderKind "anthropic") (fun _ -> Some hugeImgBytes) refHuge
        Assert.Single(contentsHuge) |> ignore
        match contentsHuge.[0] with
        | :? TextContent as tc -> Assert.Contains("超过", tc.Text)
        | _ -> Assert.Fail("Expected TextContent")

        // 未知二进制
        let binBytes = [| 0xCAuy; 0xFEuy; 0xBAuy; 0xBEuy |]
        let refBin = { sha256 = "bin"; mediaType = "application/octet-stream"; fileName = "app.exe"; size = 4L }
        let contentsBin = AttachmentContent.resolve (MediaSupport.ofProviderKind "anthropic") (fun _ -> Some binBytes) refBin
        Assert.Single(contentsBin) |> ignore
        match contentsBin.[0] with
        | :? TextContent as tc -> Assert.Contains("二进制内容不进上下文", tc.Text)
        | _ -> Assert.Fail("Expected TextContent")

    [<Fact>]
    let ``AttachmentContent appendTo correctly adds to chat message`` () =
        let msg = ChatMessage(ChatRole.User, "initial prompt")
        let imgBytes = [| 9uy; 8uy |]
        let ref = { sha256 = "abc"; mediaType = "image/png"; fileName = "pic.png"; size = 2L }
        let support = MediaSupport.ofProviderKind "anthropic"
        let resMsg = AttachmentContent.appendTo support (fun _ -> Some imgBytes) [ref] msg
        Assert.Equal(3, resMsg.Contents.Count)
        Assert.Equal("initial prompt", (resMsg.Contents.[0] :?> TextContent).Text)

