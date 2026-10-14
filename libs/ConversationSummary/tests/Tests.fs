module Tests

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.UI

let private mk title minutesAgo : ConversationSummary =
    { id = Guid.NewGuid(); title = title; preview = ""; running = false; pinned = false; archived = false
      createdAt = DateTimeOffset.Now.AddMinutes(-minutesAgo); updatedAt = DateTimeOffset.Now.AddMinutes(-minutesAgo)
      messageCount = 0; isFork = false; providerId = ""; model = ""; lastCommitId = 0UL }

// 迁自旧 UiPureLogicTests。
[<Fact>]
let ``ConversationSummary matching, sorting and grouping`` () =
    let now = DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)
    let item1 =
        { id = Guid.NewGuid(); title = "项目讨论"; preview = "好的，我们来看一下架构"; running = false; pinned = true; archived = false
          createdAt = now.AddHours(-2.0); updatedAt = now.AddHours(-1.0); messageCount = 5; isFork = false
          providerId = "openai"; model = "gpt-5"; lastCommitId = 100UL }
    let item2 =
        { id = Guid.NewGuid(); title = "随手记"; preview = "买菜清单"; running = false; pinned = false; archived = false
          createdAt = now.AddDays(-3.0); updatedAt = now.AddDays(-3.0); messageCount = 2; isFork = false
          providerId = "deepseek"; model = "deepseek-chat"; lastCommitId = 90UL }
    Assert.True(ConversationSummary.matches "讨论" item1)
    Assert.True(ConversationSummary.matches "gpt-5" item1)
    Assert.True(ConversationSummary.matches "买菜" item2)
    Assert.False(ConversationSummary.matches "无关词" item1)
    let groups = ConversationSummary.group now [ item1; item2 ]
    Assert.NotEmpty(groups)
    Assert.Equal("置顶", groups[0].label)
    Assert.Single(groups[0].items) |> ignore
    Assert.Equal("项目讨论", groups[0].items[0].title)

// 迁自旧 UiModelTests。
[<Fact>]
let ``ConversationSummary parseList handles valid, empty and invalid records`` () =
    let arr = JsonArray()
    let cid1 = Guid.NewGuid()
    let cid2 = Guid.NewGuid()
    let item1 = JsonObject()
    item1["conversationId"] <- cid1.ToString("D")
    item1["title"] <- "Valid Title"
    item1["createdAt"] <- "2026-09-30T10:00:00Z"
    item1["updatedAt"] <- "2026-09-30T10:05:00Z"
    item1["archived"] <- false
    item1["pinned"] <- true
    item1["lastCommitId"] <- 15UL
    arr.Add item1
    let item2 = JsonObject()
    item2["conversationId"] <- cid2.ToString("D")
    item2["title"] <- "Bad Date"
    item2["createdAt"] <- "not-a-date"
    item2["updatedAt"] <- "invalid"
    item2["lastCommitId"] <- 0UL
    arr.Add item2
    let item3 = JsonObject()
    item3["conversationId"] <- "not-a-valid-guid"
    arr.Add item3
    let parsed = ConversationSummary.parseList arr
    Assert.Equal(2, parsed.Length)
    Assert.Equal(cid1, parsed.[0].id)
    Assert.True(parsed.[0].pinned)
    Assert.Equal(DateTimeOffset.UnixEpoch, parsed.[1].createdAt)

[<Fact>]
let ``ConversationSummary matches searches single and multi-word keywords`` () =
    let s = { mk "Refactoring Wanxiang UI ViewModel Architecture" 0.0 with providerId = "test"; model = "test-model" }
    Assert.True(ConversationSummary.matches "Wanxiang" s)
    Assert.True(ConversationSummary.matches "ui refactoring" s)
    Assert.False(ConversationSummary.matches "unrelated keyword" s)

// 迁自旧 R19SimplifyTests。
[<Fact>]
let ``361 同名对话附时间，不同名不附`` () =
    let a = mk "新对话" 5.0
    let b = mk "新对话" 90.0
    let c = mk "写周报" 1.0
    let all = [ a; b; c ]
    Assert.True((ConversationSummary.disambiguator all DateTimeOffset.Now a).IsSome)
    Assert.True((ConversationSummary.disambiguator all DateTimeOffset.Now b).IsSome)
    Assert.NotEqual(ConversationSummary.disambiguator all DateTimeOffset.Now a, ConversationSummary.disambiguator all DateTimeOffset.Now b)
    Assert.Equal(None, ConversationSummary.disambiguator all DateTimeOffset.Now c)
    let old = { (mk "新对话" 0.0) with updatedAt = DateTimeOffset.Now.AddDays(-3.0) }
    let stamp = ConversationSummary.disambiguator (old :: all) DateTimeOffset.Now old
    Assert.Contains("/", stamp.Value)

[<Fact>]
let ``generating status is the only running marker`` () =
    Assert.True(ConversationSummary.isGeneratingStatus "generating")
    Assert.False(ConversationSummary.isGeneratingStatus "idle")
