module Wanxiang.Tests.WireGoldenTests

// 线格式金样：客户端（其他设备、旧版本）只认这些字节。任何一行变化都意味着协议变更，
// 必须同时提升 protocolVersion 并在根 AGENTS.md 记录，不可只改金样让测试变绿。

open System
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol

let g = Guid("11111111-2222-3333-4444-555555555555")
let cfg = { provider = "p"; model = "m"; instructions = Some "i"; tools = ["t"]; temperature = Some 0.5; topP = None; maxTokens = Some 10; thinkingBudget = None; extraJson = None }

let golden : (WireEvent * string) list =
    [
        AuthPresent {| token = "tok" |}, """{"type":"auth.present","payload":{"token":"tok"}}"""
        PairingFailed {| reason = "r"; frozen = true; freezeMinutes = 5 |}, """{"type":"pairing.failed","payload":{"reason":"r","frozen":true,"freezeMinutes":5}}"""
        ObserveConversation {| conversationId = g |}, """{"type":"conversation.observe","payload":{"threadId":"11111111-2222-3333-4444-555555555555"}}"""
        CommandCommitted {| invocationId = g; commandId = "c"; commitId = 18446744073709551615UL |}, """{"type":"command.committed","payload":{"invocationId":"11111111-2222-3333-4444-555555555555","commandId":"c","commitId":18446744073709551615}}"""
        CommandRejected {| invocationId = g; code = "x"; message = "y"; requiredCommitId = Some 7UL |}, """{"type":"command.rejected","payload":{"invocationId":"11111111-2222-3333-4444-555555555555","code":"x","message":"y","requiredCommitId":7}}"""
        CursorAdvanced {| id = 42UL |}, """{"type":"cursor.advanced","payload":{"id":42}}"""
        GenerationFinished {| conversationId = g; generationId = g; status = "failed"; error = Some { kind = GenerationErrorKind.ProviderTimeout; message = "slow"; detail = None; retryable = true; retryAfterSeconds = Some 3 }; usage = None |}, """{"type":"generation.finished","payload":{"threadId":"11111111-2222-3333-4444-555555555555","runId":"11111111-2222-3333-4444-555555555555","status":"failed","error":{"code":"provider-timeout","message":"slow","retryable":true,"retryAfterSeconds":3}}}"""
        GenerationDelta { conversationId = g; generationId = g; payload = Text.Json.Nodes.JsonNode.Parse """{"text":"好"}""" }, """{"type":"generation.delta","payload":{"threadId":"11111111-2222-3333-4444-555555555555","runId":"11111111-2222-3333-4444-555555555555","content":{"text":"好"}}}"""
        AttachmentChunk {| attachmentId = g; index = 2; dataBase64 = "AA==" |}, """{"type":"attachment.chunk","payload":{"attachmentId":"11111111-2222-3333-4444-555555555555","index":2,"data":"AA=="}}"""
    ]

[<Fact>]
let ``encode matches golden bytes`` () =
    for ev, text in golden do
        Assert.Equal(text, WireCodec.encode ev)

[<Fact>]
let ``golden bytes decode back to the same event`` () =
    for ev, text in golden do
        match WireCodec.tryDecode text with
        | Ok back -> Assert.Equal(text, WireCodec.encode back)
        | Error e -> failwithf "%s: %s" text e
        ignore ev

let createCmd = CreateConversation {| invocationId = g; conversationId = g; title = "t"; config = cfg |}

[<Fact>]
let ``command golden bytes`` () =
    Assert.Equal("""{"type":"conversation.create","payload":{"invocationId":"11111111-2222-3333-4444-555555555555","threadId":"11111111-2222-3333-4444-555555555555","title":"t","config":{"provider":"p","model":"m","instructions":"i","tools":["t"],"temperature":0.5,"maxTokens":10}}}""", WireCodec.encodeCommand createCmd)

[<Fact>]
let ``business payload golden bytes (domain hashes this after JCS)`` () =
    Assert.Equal("""{"conversationId":"11111111-2222-3333-4444-555555555555","title":"t","config":{"provider":"p","model":"m","instructions":"i","tools":["t"],"temperature":0.5,"maxTokens":10}}""", (ClientCommand.businessPayload createCmd).ToJsonString())

[<Fact>]
let ``session config json round trip`` () =
    Assert.Equal(cfg, SessionConfig.fromJson (SessionConfig.toJson cfg))
    let empty = SessionConfig.fromJson (Text.Json.Nodes.JsonObject())
    Assert.Equal("", empty.provider)
    Assert.Empty(empty.tools)
