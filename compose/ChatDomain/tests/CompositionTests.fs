module Wanxiang.Tests.CompositionTests

// 真实组合：线协议命令 → CommandEngine 规划 → 提交外壳编码成文本（磁盘与线上的形状）→ 解码 → 投影折叠。
// 账本的幂等表用一个 Map 代替：它在 LedgerStore 里的行为已由 LedgerStore 自己的测试验证。

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Tests.Helpers

type private Host() =
    let mutable proj = Projection.empty
    let mutable stamps: Map<string, string * CommitId> = Map.empty
    let lines = ResizeArray<string>()
    let at = DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero)
    member _.Projection = proj
    member _.Lines = List.ofSeq lines

    member _.Run(cmd: ClientCommand) : PlanResult =
        let r = CommandEngine.planStamped stamps proj proj.latestCommitId cmd

        match r with
        | Planned p ->
            let id = proj.latestCommitId + 1UL

            let commit =
                Events.Commit.create id (at.AddSeconds(float id)) p.events
                |> Events.Commit.withCommand p.commandId p.commandType p.canonicalHash

            let line = CommitCodec.commitToJsonLine commit

            match CommitCodec.tryCommitFromJsonLine line with
            | None -> failwithf "commit %d does not decode: %s" id line
            | Some back ->
                match Projection.applyCommit proj back with
                | Error e -> failwithf "commit %d does not apply: %s" id (WanxiangError.message e)
                | Ok next ->
                    proj <- next
                    stamps <- stamps.Add(p.commandId, (p.canonicalHash, id))
                    lines.Add line
        | _ -> ()

        r

let private text (p: Projection) =
    (ProjectionCodec.toJson p).ToJsonString()

[<Fact>]
let ``a conversation lives through create, send, retry, fork, regenerate and replays to the same state`` () =
    let h = Host()
    let conv = Guid.NewGuid()

    let create =
        CreateConversation
            {|
                invocationId = Guid.NewGuid()
                conversationId = conv
                title = " 标题 "
                config = testConfig ()
            |}

    let send =
        SendUserMessage
            {|
                invocationId = Guid.NewGuid()
                conversationId = conv
                messageJson = userMessageJson "你好"
            |}

    Assert.True(
        match h.Run create with
        | Planned _ -> true
        | _ -> false
    )

    Assert.True(
        match h.Run send with
        | Planned _ -> true
        | _ -> false
    )
    // 网络重试：同一 invocationId 再来一次，得到原提交，不写新提交
    Assert.Equal(IdempotentReplay 2UL, h.Run send)
    Assert.Equal(IdempotentReplay 1UL, h.Run create)

    let answer =
        SendUserMessage
            {|
                invocationId = Guid.NewGuid()
                conversationId = conv
                messageJson = assistantMessageJson "你好呀"
            |}

    h.Run answer |> ignore

    let regen =
        RegenerateResponse
            {|
                invocationId = Guid.NewGuid()
                conversationId = conv
            |}

    Assert.True(
        match h.Run regen with
        | Planned p -> p.events.Length = 1
        | _ -> false
    )

    let fork = Guid.NewGuid()

    let forkCmd =
        ForkConversation
            {|
                invocationId = Guid.NewGuid()
                conversationId = fork
                parentConversationId = conv
                forkAfterId = None
                config = testConfig ()
                editedMessageJson = userMessageJson "换个问法"
            |}

    Assert.True(
        match h.Run forkCmd with
        | Planned _ -> true
        | _ -> false
    )

    let p = h.Projection
    Assert.Equal("标题", (Projection.tryConversation p conv).Value.title)

    Assert.Equal(
        1,
        Projection.effectiveMessages p (Projection.tryConversation p conv).Value
        |> List.length
    )

    Assert.Equal("标题（分叉）", (Projection.tryConversation p fork).Value.title)
    Assert.Equal(5UL, p.latestCommitId)
    // 从文本重放：结果与在线折叠逐字节相同
    let replayed =
        h.Lines
        |> List.fold
            (fun acc line ->
                match CommitCodec.tryCommitFromJsonLine line with
                | Some c ->
                    (match Projection.applyCommit acc c with
                     | Ok n -> n
                     | Error e -> failwith (WanxiangError.message e))
                | None -> failwith line)
            Projection.empty

    Assert.Equal(text p, text replayed)

[<Fact>]
let ``same command id with a different payload is a conflict, not a replay`` () =
    let h = Host()
    let inv = Guid.NewGuid()
    let conv = Guid.NewGuid()

    h.Run(
        CreateConversation
            {|
                invocationId = inv
                conversationId = conv
                title = "a"
                config = testConfig ()
            |}
    )
    |> ignore
    // 同一 invocationId、不同标题：commandId 变了，所以是新命令；但对话 id 已被占用
    match
        h.Run(
            CreateConversation
                {|
                    invocationId = inv
                    conversationId = conv
                    title = "b"
                    config = testConfig ()
                |}
        )
    with
    | Rejected(ConversationIdTaken id) -> Assert.Equal(conv, id)
    | r -> Assert.Fail(sprintf "%A" r)

[<Fact>]
let ``stale client cursor is rejected with the required watermark`` () =
    let h = Host()
    let conv = Guid.NewGuid()

    h.Run(
        CreateConversation
            {|
                invocationId = Guid.NewGuid()
                conversationId = conv
                title = "a"
                config = testConfig ()
            |}
    )
    |> ignore

    let cmd =
        RenameConversation
            {|
                invocationId = Guid.NewGuid()
                conversationId = conv
                title = "b"
            |}

    Assert.Equal(Rejected(StaleProjection 1UL), CommandEngine.plan h.Projection 0UL cmd)

// ---- 金样（由旧仓库 extract-ledger@1a1a4d7 的 Wanxiang.Core 生成）：日志行与快照正文写在用户磁盘上，提交行也发给客户端。字节一变，旧数据或旧客户端就读不懂。

let private g = Guid("11111111-2222-3333-4444-555555555555")
let private at = DateTimeOffset(2026, 1, 2, 3, 4, 5, 678, TimeSpan.Zero)

let private goldenCommit =
    Events.Commit.create
        7UL
        at
        [
            ConversationCreated
                {
                    conversationId = g
                    title = "t"
                    config = testConfig ()
                }
            ConversationForked
                {
                    conversationId = g
                    parentConversationId = g
                    forkAfterId = Some 3UL
                }
            ConversationFlagsChanged
                {
                    conversationId = g
                    pinned = true
                    archived = false
                }
            AgentMessageRecorded
                {
                    conversationId = g
                    payloadJson = JsonNode.Parse """{"role":"user"}"""
                }
            MessageDeleted
                {
                    conversationId = g
                    messageCommitId = 5UL
                }
        ]
    |> Events.Commit.withCommand "cid" "conversation.create" "hash"

[<Fact>]
let ``commit line matches golden bytes`` () =
    Assert.Equal(
        """{"formatVersion":1,"id":"7","bootId":"00000000-0000-0000-0000-000000000000","source":"wanxiang","committedAtUtc":"2026-01-02T03:04:05.678Z","commandId":"cid","commandType":"conversation.create","commandHash":"hash","events":[{"type":"conversation.created","version":1,"data":{"conversationId":"11111111-2222-3333-4444-555555555555","title":"t","config":{"provider":"openai","model":"test-model"}}},{"type":"conversation.forked","version":1,"data":{"conversationId":"11111111-2222-3333-4444-555555555555","parentConversationId":"11111111-2222-3333-4444-555555555555","forkAfterId":3}},{"type":"conversation.flags-changed","version":1,"data":{"conversationId":"11111111-2222-3333-4444-555555555555","pinned":true,"archived":false}},{"type":"agent-message-recorded","version":1,"data":{"conversationId":"11111111-2222-3333-4444-555555555555","payload":{"role":"user"}}},{"type":"message.deleted","version":1,"data":{"conversationId":"11111111-2222-3333-4444-555555555555","messageCommitId":5}}]}""",
        CommitCodec.commitToJsonLine goldenCommit
    )

[<Fact>]
let ``projection snapshot body matches golden bytes`` () =
    let p =
        [
            Events.Commit.create
                1UL
                at
                [
                    ConversationCreated
                        {
                            conversationId = g
                            title = "t"
                            config = testConfig ()
                        }
                ]
            Events.Commit.create
                2UL
                at
                [
                    AgentMessageRecorded
                        {
                            conversationId = g
                            payloadJson = JsonNode.Parse """{"role":"user"}"""
                        }
                ]
        ]
        |> List.fold
            (fun acc c ->
                match Projection.applyCommit acc c with
                | Ok n -> n
                | Error e -> failwith (WanxiangError.message e))
            Projection.empty

    Assert.Equal(
        """{"conversations":[{"conversationId":"11111111-2222-3333-4444-555555555555","title":"t","createdAtUtc":"2026-01-02T03:04:05.6780000Z","lastActivityAtUtc":"2026-01-02T03:04:05.6780000Z","deleted":false,"pinned":false,"archived":false,"parent":null,"config":{"provider":"openai","model":"test-model"},"messages":[{"commitId":"2","conversationId":"11111111-2222-3333-4444-555555555555","payloadJson":{"role":"user"},"committedAtUtc":"2026-01-02T03:04:05.6780000Z"}],"lastCommitId":"2"}],"latestCommitId":"2"}""",
        text p
    )

[<Fact>]
let ``command id equals the one the old implementation computed`` () =
    // 已记录的 commandId 在用户日志的幂等表里；值一变，重试会被当成新命令。
    let cmd =
        CreateConversation
            {|
                invocationId = g
                conversationId = g
                title = "标题"
                config =
                    { testConfig () with
                        temperature = Some 0.5
                        tools = [ "b"; "a" ]
                    }
            |}

    let cp = CommandEngine.canonicalPayload cmd

    Assert.Equal(
        """{"config":{"model":"test-model","provider":"openai","temperature":0.5,"tools":["b","a"]},"conversationId":"11111111-2222-3333-4444-555555555555","title":"标题"}""",
        cp
    )

    Assert.Equal("kQENH2UAAPp2XiCthWRZ3p8cNZdXDHC3xJ1DHGBGnNY", CommandId.compute g (ClientCommand.commandType cmd) cp)
