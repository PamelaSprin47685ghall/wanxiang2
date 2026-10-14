module Tests

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由旧 Wanxiang.UI 程序集跑同一份 Render.fs 生成。
[<Fact>]
let ``golden: snapshot items, merge, reply tail and sizes match old code`` () =
    let dir = Path.Combine(AppContext.BaseDirectory, "golden")
    let lines = File.ReadAllLines(Path.Combine(dir, "inputs.jsonl")) |> Array.filter (fun l -> l <> "") |> List.ofArray
    Assert.Equal(File.ReadAllText(Path.Combine(dir, "expected.txt")), Render.all lines)

// 迁自旧 UiModelTests。
[<Fact>]
let ``AttachmentRef detects image mime types and formats file sizes`` () =
    let imgAtt: AttachmentRef = {
        sha256 = "dummy-sha"
        fileName = "screenshot.PNG"
        size = 1048576L
        mediaType = "image/png"
    }
    Assert.True(AttachmentRef.isImage imgAtt)
    Assert.Equal("1.0 MiB", AttachmentRef.formatSize imgAtt.size)

    let textAtt: AttachmentRef = {
        sha256 = "dummy-sha-2"
        fileName = "notes.txt"
        size = 512L
        mediaType = "text/plain"
    }
    Assert.False(AttachmentRef.isImage textAtt)
    Assert.Equal("512 B", AttachmentRef.formatSize textAtt.size)

    let kbAtt: AttachmentRef = {
        sha256 = "dummy-sha-3"
        fileName = "doc.pdf"
        size = 2048L
        mediaType = "application/pdf"
    }
    Assert.Equal("2.0 KiB", AttachmentRef.formatSize kbAtt.size)

[<Fact>]
let ``MessageView ofJson parses user and assistant roles, reasoning, and tools`` () =
    let userJson = JsonObject()
    userJson["role"] <- "user"
    let userContents = JsonArray()
    let textNode = JsonObject()
    textNode["type"] <- "text"
    textNode["text"] <- "Hello Wanxiang"
    userContents.Add textNode
    userJson["contents"] <- userContents

    let userView = MessageView.ofJson userJson
    Assert.True(MessageView.isUser userView)
    Assert.Equal("Hello Wanxiang", userView.text)
    Assert.Empty(userView.reasoning)
    Assert.Empty(userView.toolCalls)

    let assistantJson = JsonObject()
    assistantJson["role"] <- "assistant"
    let asstContents = JsonArray()
    let reasonNode = JsonObject()
    reasonNode["type"] <- "reasoning"
    reasonNode["text"] <- "Let me think..."
    asstContents.Add reasonNode

    let toolNode = JsonObject()
    toolNode["type"] <- "functionCall"
    toolNode["callId"] <- "call-1"
    toolNode["name"] <- "read_file"
    let argsObj = JsonObject()
    argsObj["path"] <- "test.txt"
    toolNode["arguments"] <- argsObj
    asstContents.Add toolNode

    let asstText = JsonObject()
    asstText["text"] <- "I am an AI assistant"
    asstContents.Add asstText
    assistantJson["contents"] <- asstContents

    let assistantView = MessageView.ofJson assistantJson
    Assert.False(MessageView.isUser assistantView)
    Assert.Equal("I am an AI assistant", assistantView.text)
    Assert.Equal("Let me think...", assistantView.reasoning)
    Assert.Single(assistantView.toolCalls) |> ignore
    Assert.Equal("read_file", assistantView.toolCalls.[0].name)
    Assert.Equal("call-1", assistantView.toolCalls.[0].callId)

[<Fact>]
let ``MessageView mergeToolResults correctly associates results and removes tool messages`` () =
    let assistantView = {
        MessageView.empty with
            role = "assistant"
            text = "Checking directory..."
            toolCalls = [
                { callId = "call-dir"; name = "list_dir"; argumentsJson = "{}"; result = None }
            ]
    }

    let toolView = {
        MessageView.empty with
            role = "tool"
            toolResults = [ ("call-dir", "file1.fs, file2.fs") ]
    }

    let merged = MessageView.mergeToolResults [ assistantView; toolView ]
    Assert.Single(merged) |> ignore
    let finalAssistant = merged.[0]
    Assert.Single(finalAssistant.toolCalls) |> ignore
    Assert.Equal(Some "file1.fs, file2.fs", finalAssistant.toolCalls.[0].result)

// 迁自旧 UiPureLogicTests。
[<Fact>]
let ``AttachmentRef formatSize formats correctly`` () =
    Assert.Equal("500 B", AttachmentRef.formatSize 500L)
    Assert.Equal("1.5 KiB", AttachmentRef.formatSize 1536L)
    Assert.Equal("2.0 MiB", AttachmentRef.formatSize (2L * 1024L * 1024L))

[<Fact>]
let ``AttachmentRef isImage distinguishes correctly`` () =
    let img = { sha256 = "abc"; size = 10L; mediaType = "image/jpeg"; fileName = "a.jpg" }
    let doc = { sha256 = "def"; size = 20L; mediaType = "application/pdf"; fileName = "b.pdf" }
    Assert.True(AttachmentRef.isImage img)
    Assert.False(AttachmentRef.isImage doc)

[<Fact>]
let ``ofJson parses text, reasoning, toolCalls, and attachments`` () =
    let obj = JsonObject()
    obj["role"] <- "assistant"
    obj["text"] <- "你好，我是助手。"
    let contents = JsonArray()

    let reasoningObj = JsonObject()
    reasoningObj["type"] <- "reasoning"
    reasoningObj["text"] <- "思考中..."
    contents.Add(reasoningObj)

    let callObj = JsonObject()
    callObj["type"] <- "functionCall"
    callObj["callId"] <- "call_123"
    callObj["name"] <- "get_weather"
    let callArgs = JsonObject()
    callArgs["city"] <- "Beijing"
    callObj["arguments"] <- callArgs
    contents.Add(callObj)

    obj["contents"] <- contents

    let msg = MessageView.ofJson obj
    Assert.Equal("assistant", msg.role)
    Assert.Equal("你好，我是助手。", msg.text)
    Assert.Equal("思考中...", msg.reasoning)
    Assert.Single(msg.toolCalls) |> ignore
    Assert.Equal("call_123", msg.toolCalls[0].callId)
    Assert.Equal("get_weather", msg.toolCalls[0].name)
    Assert.Contains("Beijing", msg.toolCalls[0].argumentsJson)

[<Fact>]
let ``mergeToolResults correlates callId into toolCalls`` () =
    let callMsg =
        { MessageView.empty with
            role = "assistant"
            toolCalls = [ { callId = "c1"; name = "search"; argumentsJson = "{}"; result = None } ] }
    let resMsg =
        { MessageView.empty with
            role = "tool"
            toolResults = [ ("c1", "search result payload") ] }

    let merged = MessageView.mergeToolResults [ callMsg; resMsg ]
    Assert.Single(merged) |> ignore
    Assert.Equal(Some "search result payload", merged[0].toolCalls[0].result)

// 迁自旧 ToolCallStatusTests（只有这两条不依赖 MessageCard）。
[<Fact>]
let ``ToolCallStatus badgeText returns exact expected text for all states`` () =
    Assert.Equal("执行中", ToolCallStatus.badgeText ToolCallStatus.Running)
    Assert.Equal("已完成", ToolCallStatus.badgeText ToolCallStatus.Success)
    Assert.Equal("执行失败", ToolCallStatus.badgeText (ToolCallStatus.Failed(Some "特定错误")))
    Assert.Equal("执行失败", ToolCallStatus.badgeText (ToolCallStatus.Failed None))
    Assert.Equal("结果未知", ToolCallStatus.badgeText ToolCallStatus.Unknown)

[<Fact>]
let ``ToolCallStatus isTerminal returns true only for terminal states`` () =
    Assert.False(ToolCallStatus.isTerminal ToolCallStatus.Running)
    Assert.True(ToolCallStatus.isTerminal ToolCallStatus.Success)
    Assert.True(ToolCallStatus.isTerminal (ToolCallStatus.Failed(Some "错误")))
    Assert.True(ToolCallStatus.isTerminal (ToolCallStatus.Failed None))
    Assert.True(ToolCallStatus.isTerminal ToolCallStatus.Unknown)

// 迁自旧 ConversationJourneyTests。
module Journey =
    let private msg role text = { MessageView.empty with role = role; text = text }

    [<Fact>]
    let ``latest reply text skips empty assistant tail and ignores earlier turns`` () =
        let messages =
            [ msg "user" "q1"; msg "assistant" "a1"
              msg "user" "q2"; msg "assistant" "a2"; msg "tool" ""; msg "assistant" "" ]
        Assert.Equal(Some "a2", ReplyTail.latestText messages)
        // 尾部是用户消息：没有可复制的「最新回复」，不回退去复制上一轮。
        Assert.Equal(None, ReplyTail.latestText (messages @ [ msg "user" "q3" ]))

    [<Fact>]
    let ``regenerate needs confirmation only when the tail has something to lose`` () =
        let failedEmpty = [ msg "user" "q"; msg "assistant" "" ]
        Assert.False(ReplyTail.hasLosableContent failedEmpty)
        Assert.False(ReplyTail.hasLosableContent [ msg "user" "q" ])
        Assert.True(ReplyTail.hasLosableContent [ msg "user" "q"; msg "assistant" "partial" ])
        Assert.True(ReplyTail.hasLosableContent [ msg "user" "q"; { msg "assistant" "" with reasoning = "thinking" } ])

// 迁自旧 JourneyR9Tests；unansweredError 的 retryable 断言留在旧仓库（GenerationError 属 Wire）。
module R9 =
    let private msg role text (at: DateTimeOffset option) : MessageView =
        { MessageView.empty with role = role; text = text; commitId = Some 1UL; committedAt = at }

    [<Fact>]
    let ``263 末尾用户消息超过宽限期仍无回复判为未回复`` () =
        let now = DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero)
        let old = Some(now.AddMinutes -3.0)
        let fresh = Some(now.AddSeconds -3.0)
        Assert.True(ReplyTail.isUnanswered now [ msg "user" "RATE" old ])
        Assert.False(ReplyTail.isUnanswered now [ msg "user" "刚发" fresh ])
        Assert.False(ReplyTail.isUnanswered now [ msg "user" "q" old; msg "assistant" "a" old ])
        Assert.False(ReplyTail.isUnanswered now [ msg "user" "流式" None ])
        Assert.False(ReplyTail.isUnanswered now [])

