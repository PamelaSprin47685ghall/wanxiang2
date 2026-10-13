module AgentHistoryTests

open Xunit
open Wanxiang.Agent

[<Fact>]
let ``the conversation id key is stable`` () =
    Assert.Equal("wanxiang.conversationId", WanxiangHistoryProvider.ConversationIdKey)
