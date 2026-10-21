module ConversationsTests

// 配置用 string 代替：领域只保存、复制配置，不解读它。

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core

let at = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

let msg role =
    JsonNode.Parse(sprintf """{"role":"%s"}""" role)

let run (events: EventData<string> list list) : Projection<string> =
    events
    |> List.indexed
    |> List.fold
        (fun p (i, evs) ->
            match Conversations.apply p (uint64 i + 1UL) (at.AddMinutes(float i)) evs with
            | Ok n -> n
            | Error e -> failwith e)
        Conversations.empty

let cfgToJson (s: string) =
    JsonObject(dict [ "v", JsonValue.Create(s) :> JsonNode ])

let cfgOfJson (o: JsonObject) = o["v"].GetValue<string>()

[<Fact>]
let ``fork copies the parent config untouched and inherits messages up to the fork point`` () =
    let a, b, c = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()

    let p =
        run
            [
                [
                    ConversationCreated
                        {
                            conversationId = a
                            title = "A"
                            config = "cfg-a"
                        }
                ]
                [
                    AgentMessageRecorded
                        {
                            conversationId = a
                            payloadJson = msg "user"
                        }
                ]
                [
                    AgentMessageRecorded
                        {
                            conversationId = a
                            payloadJson = msg "assistant"
                        }
                ]
                [
                    ConversationForked
                        {
                            conversationId = b
                            parentConversationId = a
                            forkAfterId = Some 2UL
                        }
                ]
                [
                    ConversationForked
                        {
                            conversationId = c
                            parentConversationId = a
                            forkAfterId = None
                        }
                ]
            ]

    let fb = (Conversations.tryConversation p b).Value
    Assert.Equal("cfg-a", fb.config)
    Assert.Equal("A（分叉）", fb.title)
    Assert.Equal("A（分叉 2）", (Conversations.tryConversation p c).Value.title)
    Assert.Equal<uint64 list>([ 2UL ], Conversations.effectiveMessages p fb |> List.map _.commitId)

[<Fact>]
let ``later changes in the parent do not leak into a fork`` () =
    let a, b = Guid.NewGuid(), Guid.NewGuid()

    let p =
        run
            [
                [
                    ConversationCreated
                        {
                            conversationId = a
                            title = "A"
                            config = "x"
                        }
                ]
                [
                    AgentMessageRecorded
                        {
                            conversationId = a
                            payloadJson = msg "user"
                        }
                ]
                [
                    ConversationForked
                        {
                            conversationId = b
                            parentConversationId = a
                            forkAfterId = Some 2UL
                        }
                ]
                [
                    MessageDeleted
                        {
                            conversationId = a
                            messageCommitId = 2UL
                        }
                ]
            ]

    Assert.Empty(Conversations.effectiveMessages p (Conversations.tryConversation p a).Value)

    Assert.Single(Conversations.effectiveMessages p (Conversations.tryConversation p b).Value)
    |> ignore

[<Fact>]
let ``commit ids must be contiguous and targets must exist`` () =
    let a = Guid.NewGuid()

    match
        Conversations.apply
            Conversations.empty
            2UL
            at
            [
                ConversationCreated
                    {
                        conversationId = a
                        title = "A"
                        config = "x"
                    }
            ]
    with
    | Error e -> Assert.Contains("out of order", e)
    | Ok _ -> Assert.Fail "gap accepted"

    match
        Conversations.apply Conversations.empty 1UL at [ ConversationRenamed { conversationId = a; title = "B" } ]
    with
    | Error e -> Assert.Contains("not found", e)
    | Ok _ -> Assert.Fail "rename of missing conversation accepted"

[<Fact>]
let ``a failing event leaves the projection unchanged`` () =
    let a = Guid.NewGuid()

    let p =
        run
            [
                [
                    ConversationCreated
                        {
                            conversationId = a
                            title = "A"
                            config = "x"
                        }
                ]
            ]

    match
        Conversations.apply
            p
            2UL
            at
            [
                ConversationRenamed { conversationId = a; title = "B" }
                MessageDeleted
                    {
                        conversationId = a
                        messageCommitId = 9UL
                    }
            ]
    with
    | Error _ -> Assert.Equal("A", (Conversations.tryConversation p a).Value.title)
    | Ok _ -> Assert.Fail "missing message accepted"

[<Fact>]
let ``trailing model messages stop at the last user message`` () =
    let a = Guid.NewGuid()

    let p =
        run
            [
                [
                    ConversationCreated
                        {
                            conversationId = a
                            title = "A"
                            config = "x"
                        }
                ]
                [
                    AgentMessageRecorded
                        {
                            conversationId = a
                            payloadJson = msg "user"
                        }
                ]
                [
                    AgentMessageRecorded
                        {
                            conversationId = a
                            payloadJson = msg "assistant"
                        }
                ]
                [
                    AgentMessageRecorded
                        {
                            conversationId = a
                            payloadJson = msg "tool"
                        }
                ]
            ]

    let visible =
        Conversations.effectiveMessages p (Conversations.tryConversation p a).Value

    Assert.Equal<uint64 list>([ 3UL; 4UL ], Conversations.trailingModelMessages visible)

[<Fact>]
let ``list puts pinned first and hides deleted`` () =
    let a, b, c = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()

    let p =
        run
            [
                [
                    ConversationCreated
                        {
                            conversationId = a
                            title = "A"
                            config = "x"
                        }
                ]
                [
                    ConversationCreated
                        {
                            conversationId = b
                            title = "B"
                            config = "x"
                        }
                ]
                [
                    ConversationCreated
                        {
                            conversationId = c
                            title = "C"
                            config = "x"
                        }
                ]
                [
                    ConversationFlagsChanged
                        {
                            conversationId = a
                            pinned = true
                            archived = false
                        }
                ]
                [ ConversationDeleted { conversationId = c } ]
            ]

    Assert.Equal<Guid list>([ a; b ], Conversations.conversationList p |> List.map _.conversationId)

[<Fact>]
let ``events and projection round trip through JSON with any config codec`` () =
    let a = Guid.NewGuid()

    let events =
        [
            ConversationCreated
                {
                    conversationId = a
                    title = "A"
                    config = "cfg"
                }
            AgentMessageRecorded
                {
                    conversationId = a
                    payloadJson = msg "user"
                }
            ConversationConfigUpdated { conversationId = a; config = "cfg2" }
        ]

    for ev in events do
        let o = EventJson.toJson cfgToJson ev
        let back = (EventJson.tryFromJson cfgOfJson o).Value
        Assert.Equal(o.ToJsonString(), (EventJson.toJson cfgToJson back).ToJsonString())

    let p = run [ events ]
    let text = (ProjectionJson.toJson cfgToJson p).ToJsonString()

    let back =
        (ProjectionJson.tryFromJson cfgOfJson (ProjectionJson.toJson cfgToJson p)).Value

    Assert.Equal(text, (ProjectionJson.toJson cfgToJson back).ToJsonString())
    Assert.Equal("cfg2", (Conversations.tryConversation back a).Value.config)

[<Fact>]
let ``unknown event version is refused instead of guessed`` () =
    let o =
        EventJson.toJson cfgToJson (ConversationDeleted { conversationId = Guid.NewGuid() })

    o["version"] <- 2
    Assert.True((EventJson.tryFromJson cfgOfJson o).IsNone)
