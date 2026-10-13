module Wanxiang.Tests.ProductizationOrchestrationTests

// 原样迁自旧仓库 d9651ed tests/Wanxiang.Tests/ProductizationTests.fs「对话标题」「上下文裁剪」「编排记账与结束决策」三节，
// 以及 PartialPersistAndUrlSchemeTests.fs 的「250 只有助手正文才算可续写的半截」（只改了模块名与 open）。

open System
open System.IO
open System.Text
open System.Text.Json.Nodes
open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Agent
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Server
open Wanxiang.Store
open Wanxiang.Tests.Helpers


// ---------------------------------------------------------------- 对话标题

[<Fact>]
let ``auto title keeps only the first sentence and strips markdown`` () =
    let title = TitleGenerator.sanitize "**万象存储模型**。第二句应当被丢掉。" "原始提问"
    Assert.Equal("万象存储模型", title)

[<Fact>]
let ``auto title falls back to the user question when the model returns nothing`` () =
    Assert.Equal("解释一下投影", TitleGenerator.sanitize "   " "解释一下投影")

[<Fact>]
let ``only system placeholder titles are auto-renamed`` () =
    Assert.True(TitleGenerator.isPlaceholder "新对话")
    Assert.True(TitleGenerator.isPlaceholder "对话 3f8a1c22")
    Assert.False(TitleGenerator.isPlaceholder "我给它起的名字")

// ---------------------------------------------------------------- 上下文裁剪

let private textMessage (role: ChatRole) (text: string) =
    MessageSerde.textMessage role text

[<Fact>]
let ``context trimming keeps the most recent messages`` () =
    let messages = [ for i in 1 .. 30 -> textMessage ChatRole.User (string i) ]
    let trimmed = GenerationContext.trim 10 0 messages
    Assert.Equal(10, List.length trimmed)
    Assert.Equal("30", MessageSerde.textOf (List.last trimmed))

[<Fact>]
let ``trimming never leaves an orphan tool result at the front`` () =
    let messages =
        [ textMessage ChatRole.User "问题"
          textMessage ChatRole.Tool "工具结果"
          textMessage ChatRole.Assistant "回答" ]
    let trimmed = GenerationContext.trim 2 0 messages
    Assert.DoesNotContain(ChatRole.Tool, trimmed |> List.map (fun m -> m.Role))

[<Fact>]
let ``zero means no message limit`` () =
    let messages = [ for i in 1 .. 5 -> textMessage ChatRole.User (string i) ]
    Assert.Equal(5, GenerationContext.trim 0 0 messages |> List.length)

// ---------------------------------------------------------------- 编排记账与结束决策（D1/D2/N2 回归）

[<Fact>]
let ``queued submit outcomes always resolve to committed or visible error`` () =
    let convId = newConversationId ()
    let commit =
        Events.Commit.create
            7UL
            DateTimeOffset.UtcNow
            [ ConversationCreated { conversationId = convId; title = "T"; config = testConfig () } ]
    let failure = ValidationError "boom"
    // committed 系列 → Ok（拿到 log id，可广播 CommandCommitted）
    for result in [ Committed commit; IdempotentReplay commit ] do
        match SubmitOutcome.ofSubmitResult result with
        | Ok c -> Assert.Equal(7UL, c.id)
        | Error e -> failwithf "should resolve to committed, got %s" (WanxiangError.message e)
    // 失败系列 → Error（调用方经 RecordSubmitOutcome 广播 ServerError，不静默）
    for result in [ CommandIdRejected failure; CommitFailed failure ] do
        match SubmitOutcome.ofSubmitResult result with
        | Ok _ -> failwith "should resolve to a visible error"
        | Error e -> Assert.Equal(failure, e)

[<Fact>]
let ``empty built context with a drained batch fails retryably, never completes`` () =
    let status, err = EmptyContextFinish.decide true
    Assert.Equal("failed", status)
    match err with
    | None -> failwith "non-empty batch must carry an error card"
    | Some e ->
        Assert.True e.retryable
        Assert.False(String.IsNullOrWhiteSpace e.message)
    let idleStatus, idleErr = EmptyContextFinish.decide false
    Assert.Equal("completed", idleStatus)
    Assert.Equal(None, idleErr)

[<Fact>]
let ``tool round limit error is retryable and names maxToolRounds`` () =
    let err = ToolRoundLimit.error 12
    Assert.Equal(ToolFailed, err.kind)
    Assert.True err.retryable
    Assert.Contains("maxToolRounds", err.message)
    Assert.Contains("12", err.message)


let private partialMessage (text: string) : JsonNode =
    JsonNode.Parse(sprintf """{"role":"assistant","contents":[{"$type":"text","text":%s}]}""" (System.Text.Json.JsonSerializer.Serialize text))

[<Fact>]
let ``250 只有助手正文才算可续写的半截`` () =
    Assert.True(ChatOrchestrator.PartialText(partialMessage "有字"))
    Assert.False(ChatOrchestrator.PartialText(partialMessage "   "))
    Assert.False(ChatOrchestrator.PartialText(JsonNode.Parse """{"role":"user","contents":[{"text":"x"}]}"""))

