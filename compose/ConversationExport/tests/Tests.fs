module Tests

// 迁自旧 ConversationExportTests：真实的 ChatDomain 投影 + ServerModel.exportPage 产出分页，交给本组合者的导出控制器。
// 旧用例外层的 Headless.run 只为 Avalonia 线程，这里是纯逻辑，去掉；断言 Markdown 的两处改为直接断言文档（Markdown 渲染归 TranscriptMarkdown）。

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Server
open Wanxiang.UI

let private userMessageJson (text: string) : JsonNode =
    let o = JsonObject()
    o["role"] <- "user"
    let contents = JsonArray()
    let tc = JsonObject()
    tc["text"] <- text
    contents.Add tc
    o["contents"] <- contents
    o

let private testConfig () =
    { SessionConfig.empty with
        provider = "openai"
        model = "test-model"
    }

let private append events (proj: Projection) =
    let commit =
        Events.Commit.create (proj.latestCommitId + 1UL) DateTimeOffset.UtcNow events

    match Projection.applyCommit proj commit with
    | Ok updated -> updated
    | Error error -> failwith (WanxiangError.message error)

let private fixture count =
    let id = Guid.NewGuid()

    let mutable proj =
        Projection.empty
        |> append
            [
                ConversationCreated
                    {
                        conversationId = id
                        title = "完整历史"
                        config = testConfig ()
                    }
            ]

    for n in 1..count do
        proj <-
            proj
            |> append
                [
                    AgentMessageRecorded
                        {
                            conversationId = id
                            payloadJson = userMessageJson (sprintf "message-%04d" n)
                        }
                ]

    proj, id

let private page proj query =
    match ServerModel.exportPage proj query with
    | Ok result -> result
    | Error error -> failwith error

let private complete proj (controller: ConversationExportController) query =
    let mutable next = Some query
    let mutable pages = 0

    while next.IsSome do
        pages <- pages + 1
        Assert.True(pages < 100, "分页必须终止")
        next <- controller.Accept(page proj next.Value)

    Assert.True(controller.Document.IsSome, sprintf "%A" controller.Status)
    controller.Document.Value

[<Fact>]
let ``export retrieves all 450 messages while normal snapshot still contains only 200`` () =
    let proj, id = fixture 450

    let normal, _, more =
        ServerModel.conversationMessagesTail proj proj.conversations[id] 200

    Assert.Equal(200, normal.Count)
    Assert.True more
    let controller = ConversationExportController id
    let document = complete proj controller (controller.Start())
    Assert.Equal(450, document.messages.Length)
    Assert.Equal("message-0001", document.messages.Head.text)
    Assert.Equal("message-0450", document.messages |> List.last |> _.text)
    Assert.Equal<CommitId list>([ 2UL .. 451UL ], document.messages |> List.choose _.commitId)

[<Fact>]
let ``later deletion insertion and renaming cannot alter an export already reading`` () =
    let original, id = fixture 250
    let controller = ConversationExportController id
    let first = page original (controller.Start())
    let next = controller.Accept first |> Option.get
    Assert.True controller.Document.IsNone

    let changed =
        original
        |> append
            [
                MessageDeleted
                    {
                        conversationId = id
                        messageCommitId = 2UL
                    }
            ]
        |> append
            [
                AgentMessageRecorded
                    {
                        conversationId = id
                        payloadJson = userMessageJson "不能混入"
                    }
            ]
        |> append [ ConversationRenamed { conversationId = id; title = "后来的标题" } ]

    let document = complete changed controller next
    Assert.Equal("完整历史", document.title)
    Assert.Equal(original.latestCommitId, document.atCommitId)
    Assert.Equal(250, document.messages.Length)
    Assert.Equal("message-0001", document.messages.Head.text)
    Assert.DoesNotContain(document.messages, fun message -> message.text = "不能混入")

[<Fact>]
let ``deletions before export stay hidden even on older pages`` () =
    let original, id = fixture 350

    let proj =
        original
        |> append
            [
                MessageDeleted
                    {
                        conversationId = id
                        messageCommitId = 3UL
                    }
            ]

    let controller = ConversationExportController id
    let document = complete proj controller (controller.Start())
    Assert.Equal(349, document.messages.Length)
    Assert.DoesNotContain(document.messages, fun message -> message.commitId = Some 3UL)

[<Fact>]
let ``nested fork export preserves its frozen ancestors after parent deletion`` () =
    let root, parentId = fixture 250
    let childId, grandchildId = Guid.NewGuid(), Guid.NewGuid()

    let withChild =
        root
        |> append
            [
                ConversationForked
                    {
                        conversationId = childId
                        parentConversationId = parentId
                        forkAfterId = Some 221UL
                    }
            ]

    let withMessage =
        withChild
        |> append
            [
                AgentMessageRecorded
                    {
                        conversationId = childId
                        payloadJson = userMessageJson "子分支"
                    }
            ]

    let proj =
        withMessage
        |> append
            [
                ConversationForked
                    {
                        conversationId = grandchildId
                        parentConversationId = childId
                        forkAfterId = Some withMessage.latestCommitId
                    }
            ]
        |> append
            [
                AgentMessageRecorded
                    {
                        conversationId = grandchildId
                        payloadJson = userMessageJson "孙分支"
                    }
            ]
        |> append
            [
                MessageDeleted
                    {
                        conversationId = parentId
                        messageCommitId = 2UL
                    }
            ]
        |> append [ EventData.ConversationDeleted { conversationId = parentId } ]

    let controller = ConversationExportController grandchildId
    let document = complete proj controller (controller.Start())
    Assert.Equal(222, document.messages.Length)
    Assert.Equal("message-0001", document.messages.Head.text)
    Assert.Equal<string list>([ "子分支"; "孙分支" ], document.messages |> List.skip 220 |> List.map _.text)

[<Fact>]
let ``empty conversation can be exported as an empty transcript`` () =
    let proj, id = fixture 0
    let controller = ConversationExportController id
    let document = complete proj controller (controller.Start())
    Assert.Empty document.messages
    Assert.Equal("完整历史", document.title)

[<Fact>]
let ``cancel retry and disconnect cannot expose partial or stale documents`` () =
    let proj, id = fixture 250
    let controller = ConversationExportController id
    let first = page proj (controller.Start())
    controller.Accept first |> ignore
    controller.Cancel()
    Assert.True(controller.Accept(first).IsNone)
    Assert.True controller.Document.IsNone
    let restarted = controller.Start()
    Assert.NotEqual(first.exportId, restarted.exportId)
    Assert.True(controller.Accept(first).IsNone)
    Assert.Equal(ExportReading(0, None), controller.Status)
    controller.Accept(page proj restarted) |> ignore
    controller.Disconnect()
    Assert.True controller.Document.IsNone

    match controller.Status with
    | ExportFailed _ -> ()
    | other -> failwithf "%A" other

[<Theory>]
[<InlineData("short")>]
[<InlineData("empty")>]
[<InlineData("duplicate")>]
[<InlineData("foreign")>]
[<InlineData("boundary")>]
let ``invalid export pages never become saveable`` mode =
    let proj, id = fixture 250
    let controller = ConversationExportController id
    let first = page proj (controller.Start())

    let invalid =
        match mode with
        | "short" -> { first with hasMore = false }
        | "empty" -> { first with items = JsonArray() }
        | "foreign" ->
            { first with
                conversationId = Guid.NewGuid()
            }
        | "boundary" -> { first with atCommitId = 1UL }
        | _ ->
            { first with
                items = JsonArray(first.items[0].DeepClone(), first.items[0].DeepClone())
            }

    Assert.True(controller.Accept(invalid).IsNone)
    Assert.True controller.Document.IsNone

    match controller.Status with
    | ExportFailed _ -> ()
    | other -> failwithf "%A" other

[<Fact>]
let ``changed watermark or total aborts a multi-page export`` () =
    let proj, id = fixture 250

    let alterations: (ConversationExportPageData -> ConversationExportPageData) list =
        [
            (fun p ->
                { p with
                    atCommitId = p.atCommitId + 1UL
                })
            (fun p ->
                { p with
                    totalMessages = p.totalMessages + 1
                })
        ]

    for alter in alterations do
        let controller = ConversationExportController id
        let next = controller.Accept(page proj (controller.Start())) |> Option.get
        controller.Accept(alter (page proj next)) |> ignore
        Assert.True controller.Document.IsNone

        match controller.Status with
        | ExportFailed _ -> ()
        | other -> failwithf "%A" other

[<Fact>]
let ``timeout and size limit stop without offering a partial file`` () =
    let proj, id = fixture 250
    let small = ConversationExportController(id, maxBytes = 100L)
    small.Accept(page proj (small.Start())) |> ignore
    Assert.True small.Document.IsNone

    match small.Status with
    | ExportFailed _ -> ()
    | other -> failwithf "%A" other

    let slow = ConversationExportController id
    slow.Start() |> ignore
    Assert.True(slow.Expire(DateTimeOffset.UtcNow.AddMinutes 1.0, TimeSpan.FromSeconds 30.0))
    Assert.True slow.Document.IsNone

[<Fact>]
let ``page byte budget splits large messages without splitting or truncating a message`` () =
    let initial, id = fixture 0
    let text = String('x', 200000)

    let proj =
        [ 1..5 ]
        |> List.fold
            (fun proj _ ->
                proj
                |> append
                    [
                        AgentMessageRecorded
                            {
                                conversationId = id
                                payloadJson = userMessageJson text
                            }
                    ])
            initial

    let controller = ConversationExportController id
    let query = controller.Start()
    let first = page proj query
    Assert.InRange(first.items.Count, 1, 2)
    Assert.True first.hasMore
    let document = complete proj controller query
    Assert.Equal(5, document.messages.Length)
    Assert.All(document.messages, fun message -> Assert.Equal(text, message.text))

[<Fact>]
let ``unrenderable messages retain their raw content in export`` () =
    let initial, id = fixture 0

    let proj =
        initial
        |> append
            [
                AgentMessageRecorded
                    {
                        conversationId = id
                        payloadJson =
                            JsonNode.Parse(
                                """{"role":"assistant","contents":[{"type":"future-content","value":"keep-me"}]}"""
                            )
                    }
            ]

    let controller = ConversationExportController id
    let document = complete proj controller (controller.Start())
    Assert.Contains("keep-me", document.messages.Head.text)
