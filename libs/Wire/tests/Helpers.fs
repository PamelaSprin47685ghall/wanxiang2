module Wanxiang.Tests.Helpers

open System
open Wanxiang.Core
let userMessageJson (text: string) : System.Text.Json.Nodes.JsonNode =
    let o = System.Text.Json.Nodes.JsonObject()
    o["role"] <- "user"
    let contents = System.Text.Json.Nodes.JsonArray()
    let tc = System.Text.Json.Nodes.JsonObject()
    tc["text"] <- text
    contents.Add tc
    o["contents"] <- contents
    o

let assistantMessageJson (text: string) : System.Text.Json.Nodes.JsonNode =
    let o = System.Text.Json.Nodes.JsonObject()
    o["role"] <- "assistant"
    let contents = System.Text.Json.Nodes.JsonArray()
    let tc = System.Text.Json.Nodes.JsonObject()
    tc["text"] <- text
    contents.Add tc
    o["contents"] <- contents
    o

let testConfig () =
    { provider = "openai"
      model = "test-model"
      instructions = None
      tools = []
      temperature = None
      topP = None
      maxTokens = None
      thinkingBudget = None
      extraJson = None }

let newConversationId () = Guid.NewGuid()
