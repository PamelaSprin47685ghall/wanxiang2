module Scenario

// 金样场景：出站事件和命令编成 AG-UI 文本，入站文本解回语义事件（再用 WireCodec 编成文本以便比较）。
// 同一份源码既编进新组合者的测试，也编进旧 WireAgui 源码生成金样。

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol

let g = Guid("11111111-2222-3333-4444-555555555555")
let h = Guid("66666666-7777-8888-9999-000000000000")
let cfg = { provider = "p"; model = "m"; instructions = Some "i"; tools = [ "t" ]; temperature = Some 0.5; topP = None; maxTokens = Some 10; thinkingBudget = None; extraJson = None }

let content (role: string) (fields: (string * JsonNode) list) =
    let p = JsonObject()
    p["role"] <- role
    let c = JsonObject()
    for k, v in fields do c[k] <- v
    p["contents"] <- JsonArray(c :> JsonNode)
    p :> JsonNode

let s (x: string) = JsonValue.Create x :> JsonNode

let events : WireEvent list =
    [ GenerationStarted {| conversationId = g; generationId = h; providerId = "p"; model = "m" |}
      GenerationDelta { conversationId = g; generationId = h; payload = content "assistant" [ "$type", s "text"; "text", s "你好 \"x\"" ] }
      GenerationDelta { conversationId = g; generationId = h; payload = content "assistant" [ "$type", s "functionCall"; "callId", s "c1"; "name", s "search"; "arguments", JsonNode.Parse """{"q":1}""" ] }
      GenerationDelta { conversationId = g; generationId = h; payload = content "tool" [ "$type", s "functionResult"; "callId", s "c1"; "result", JsonNode.Parse """{"r":"好"}""" ] }
      GenerationDelta { conversationId = g; generationId = h; payload = content "assistant" [ "$type", s "reasoning"; "text", s "想" ] }
      GenerationDelta { conversationId = g; generationId = h; payload = JsonArray() }
      GenerationFinished {| conversationId = g; generationId = h; status = "completed"; error = None; usage = Some { promptTokens = Some 1; completionTokens = Some 2; cachedTokens = None; totalTokens = Some 3; durationMs = Some 9L } |}
      GenerationFinished {| conversationId = g; generationId = h; status = "completed"; error = None; usage = None |}
      GenerationFinished {| conversationId = g; generationId = h; status = "cancelled"; error = None; usage = None |}
      GenerationFinished {| conversationId = g; generationId = h; status = "failed"; error = Some { kind = GenerationErrorKind.ProviderTimeout; message = "慢"; detail = None; retryable = true; retryAfterSeconds = Some 3 }; usage = None |}
      CursorAdvanced {| id = 18446744073709551615UL |}
      Hello {| protocol = "wanxiang"; version = 1; instanceId = None |}
      Ping
      AuthPresent {| token = "tok" |}
      ObserveConversation {| conversationId = g |}
      CommandCommitted {| invocationId = g; commandId = "c"; commitId = 7UL |}
      CommandRejected {| invocationId = g; code = "x"; message = "y"; requiredCommitId = Some 7UL |}
      AttachmentChunk {| attachmentId = g; index = 2; dataBase64 = "AA==" |}
      ToolApprovalResolved {| approvalId = g; conversationId = h; status = "denied"; reason = "不"; alreadyHandled = false |} ]

let commands : ClientCommand list =
    [ CreateConversation {| invocationId = g; conversationId = h; title = "标题"; config = cfg |}
      ForkConversation {| invocationId = g; conversationId = h; parentConversationId = g; forkAfterId = Some 10UL; config = SessionConfig.empty; editedMessageJson = JsonObject() |} ]

let inbound : string list =
    [ """{"type":"RUN_STARTED","threadId":"11111111-2222-3333-4444-555555555555","runId":"66666666-7777-8888-9999-000000000000"}"""
      """{"type":"RUN_STARTED","threadId":"bad","runId":""}"""
      """{"type":"TEXT_MESSAGE_CHUNK","messageId":"66666666-7777-8888-9999-000000000000","delta":"嗨"}"""
      """{"type":"REASONING_MESSAGE_CHUNK","messageId":"66666666-7777-8888-9999-000000000000","delta":"想"}"""
      """{"type":"TOOL_CALL_START","toolCallId":"c1","toolCallName":"search"}"""
      """{"type":"TOOL_CALL_RESULT","messageId":"66666666-7777-8888-9999-000000000000","toolCallId":"c1","content":"ok"}"""
      """{"type":"RUN_FINISHED","threadId":"11111111-2222-3333-4444-555555555555","runId":"66666666-7777-8888-9999-000000000000"}"""
      """{"type":"RUN_ERROR","message":"boom"}"""
      """{"type":"STATE_DELTA","delta":[]}"""
      """{"type":"SOME_FUTURE_EVENT","x":1}"""
      """{"type":"CUSTOM","name":"someone.else/thing","value":{}}"""
      """{"type":"CUSTOM","name":"wanxiang.dev/cursor.advanced","value":{"id":42}}"""
      """{"type":"CUSTOM","name":"wanxiang.dev/cursor.advanced","value":{"type":"cursor.advanced","payload":{"id":43}}}"""
      """{"type":"CUSTOM","name":"wanxiang.dev/no.such","value":{"a":1}}"""
      """{"type":"CUSTOM","name":"wanxiang.dev/cursor.advanced","value":[1]}"""
      """{"type":"CUSTOM","name":"wanxiang.dev/cursor.advanced","value":{"id":"x"}}"""
      """{"type":"CUSTOM","value":{}}"""
      """{"type":"TEXT_MESSAGE_CHUNK","messageId":5}"""
      """[1]"""
      "not json" ]

let decoded (text: string) =
    match WireAgui.tryDecode text with
    | Ok None -> "ignored"
    | Ok(Some ev) -> "event " + WireCodec.encode ev
    | Error e -> "error " + e

let all () =
    String.concat "\n"
        [ yield "== encode"
          for ev in events do yield WireAgui.encode ev
          yield "== encodeCommand"
          for c in commands do yield WireAgui.encodeCommand c
          yield "== round trip"
          for ev in events do yield decoded (WireAgui.encode ev)
          yield "== decode"
          for t in inbound do yield t + " => " + decoded t ]
