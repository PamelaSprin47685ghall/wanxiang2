module Tests

// 迁自旧 LibraryTests：主机列表快照 → UI 解析 → 面包屑 / 版本切换，端到端用真实的 ChatDomain 与 ServerModel 包。

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Server
open Wanxiang.UI

let private now = DateTimeOffset.UtcNow

let private msg (role: string) (text: string) : JsonNode =
    let o = JsonObject()
    o["role"] <- role
    let contents = JsonArray()
    let tc = JsonObject()
    tc["text"] <- text
    contents.Add tc
    o["contents"] <- contents
    o

let private testConfig () = { SessionConfig.empty with provider = "openai"; model = "test-model" }

/// 顺序提交构造器：commit id 自增，保证投影严格连续。
type private Builder() =
    let mutable next = 0UL
    let commits = ResizeArray<Events.Commit>()
    member _.Add(ev: EventData) : CommitId =
        next <- next + 1UL
        commits.Add(Events.Commit.create next now [ ev ])
        next
    member this.Conversation(title: string) : Guid =
        let id = Guid.NewGuid()
        this.Add(ConversationCreated { conversationId = id; title = title; config = testConfig () }) |> ignore
        id
    member this.User(conv: Guid, text: string) : CommitId =
        this.Add(AgentMessageRecorded { conversationId = conv; payloadJson = msg "user" text })
    member this.Assistant(conv: Guid, text: string) : CommitId =
        this.Add(AgentMessageRecorded { conversationId = conv; payloadJson = msg "assistant" text })
    member this.Fork(parent: Guid, after: CommitId option) : Guid =
        let id = Guid.NewGuid()
        this.Add(ConversationForked { conversationId = id; parentConversationId = parent; forkAfterId = after }) |> ignore
        id
    member this.Delete(conv: Guid) = this.Add(EventData.ConversationDeleted { conversationId = conv }) |> ignore
    member _.Projection : Projection =
        commits
        |> Seq.fold (fun p c -> match Projection.applyCommit p c with Ok p -> p | Error e -> failwith (WanxiangError.message e)) Projection.empty

let private forkFixture () =
    let b = Builder()
    let r = b.Conversation "根对话"
    let _u2 = b.User(r, "问题一")
    let a3 = b.Assistant(r, "回答一")
    let _u4 = b.User(r, "问题二（原文）")
    let _a5 = b.Assistant(r, "回答二")
    let f1 = b.Fork(r, Some a3)
    let f1u = b.User(f1, "问题二（改写 1）")
    let f1a = b.Assistant(f1, "回答二（改写 1）")
    let g = b.Fork(f1, Some f1a)
    let _gu = b.User(g, "追问")
    b, r, f1, g, a3, f1u

let private listRows (proj: Projection) =
    ServerModel.conversationListItems proj (fun _ -> "idle")

let private byId (rows: JsonArray) (id: Guid) =
    rows |> Seq.pick (fun n -> if n["conversationId"].GetValue<string>() = id.ToString("D") then Some(n.AsObject()) else None)

[<Fact>]
let ``list snapshot carries fork lineage derived from existing facts`` () =
    let b, r, f1, g, a3, f1u = forkFixture ()
    let rows = listRows b.Projection
    let root = byId rows r
    Assert.False(root.ContainsKey("parentId"))
    let f1row = byId rows f1
    Assert.Equal(r.ToString("D"), f1row["parentId"].GetValue<string>())
    Assert.Equal(a3, f1row["forkBaseMessageId"].GetValue<uint64>())
    Assert.Equal(f1u, f1row["forkFirstMessageId"].GetValue<uint64>())
    Assert.Equal("根对话", f1row["parentTitle"].GetValue<string>())
    Assert.False(f1row["parentDeleted"].GetValue<bool>())
    // 被编辑的是根对话的第 3 条消息（前缀 u2 a3 → +1）
    Assert.Equal(3, f1row["forkOrdinal"].GetValue<int>())
    let grow = byId rows g
    Assert.Equal(f1.ToString("D"), grow["parentId"].GetValue<string>())
    // 解析：只有分叉入表
    let lineage = ForkLineage.parse rows
    Assert.False(lineage.ContainsKey r)
    Assert.Equal(f1, lineage[g].parentId)

[<Fact>]
let ``breadcrumb names the parent and falls back to deleted-original text`` () =
    let b, r, f1, g, _, _ = forkFixture ()
    let summaries (proj: Projection) = ConversationSummary.parseList (listRows proj)
    let proj = b.Projection
    let lineage = ForkLineage.parse (listRows proj)
    let crumb = ForkLineage.breadcrumb (summaries proj) lineage f1 |> Option.get
    Assert.True crumb.parentAvailable
    Assert.Equal(r, crumb.parentId)
    Assert.Equal("来自「根对话」第 3 条消息的分叉", crumb.text)
    // 多层：G 的父是 F1，不是根
    let gcrumb = ForkLineage.breadcrumb (summaries proj) lineage g |> Option.get
    Assert.Equal(f1, gcrumb.parentId)
    Assert.Contains("根对话（分叉）", gcrumb.text)
    Assert.True((ForkLineage.breadcrumb (summaries proj) lineage r).IsNone)
    // 父对话已删除：显示“原对话已删除”，分叉本身仍在列表里、消息完整
    b.Delete r
    let proj2 = b.Projection
    let rows2 = listRows proj2
    let lineage2 = ForkLineage.parse rows2
    let crumb2 = ForkLineage.breadcrumb (summaries proj2) lineage2 f1 |> Option.get
    Assert.False crumb2.parentAvailable
    Assert.Equal("原对话已删除", crumb2.text)
    Assert.True(byId rows2 f1 |> fun o -> o["parentDeleted"].GetValue<bool>())
    let f1conv = Projection.tryConversation proj2 f1 |> Option.get
    Assert.Equal(4, (Projection.effectiveMessages proj2 f1conv).Length)
    // 祖父已删时，G 的直接父 F1 仍在：面包屑仍可回到 F1
    let gcrumb2 = ForkLineage.breadcrumb (summaries proj2) lineage2 g |> Option.get
    Assert.True gcrumb2.parentAvailable


[<Fact>]
let ``three versions of one message show one-of-three and switching lands on the matching conversation`` () =
    let b = Builder()
    let r = b.Conversation "根"
    b.User(r, "开场") |> ignore
    let a3 = b.Assistant(r, "回答")
    let u4 = b.User(r, "原文")
    b.Assistant(r, "原回答") |> ignore
    let f1 = b.Fork(r, Some a3)
    let u4a = b.User(f1, "改写一")
    let f2 = b.Fork(r, Some a3)
    let u4b = b.User(f2, "改写二")
    let proj = b.Projection
    let rows = listRows proj
    let summaries = ConversationSummary.parseList rows
    let lineage = ForkLineage.parse rows
    // 原文版本：3 个版本（原文 + 2 个分叉），当前是第 1 个
    let onOriginal = ForkLineage.siblings summaries lineage r u4 (Some a3) |> Option.get
    Assert.Equal(0, onOriginal.index)
    Assert.Equal(3, onOriginal.count)
    Assert.Equal<Guid list>([ r; f1; f2 ], onOriginal.targets)
    // 在第一个分叉里：2/3；在第二个分叉里：3/3
    let onF1 = ForkLineage.siblings summaries lineage f1 u4a (Some a3) |> Option.get
    Assert.Equal((1, 3), (onF1.index, onF1.count))
    Assert.Equal<Guid list>(onOriginal.targets, onF1.targets)
    let onF2 = ForkLineage.siblings summaries lineage f2 u4b (Some a3) |> Option.get
    Assert.Equal((2, 3), (onF2.index, onF2.count))
    // 切换目标就是 targets[index±1]：从 F2 往前到 F1
    Assert.Equal(f1, onF2.targets[onF2.index - 1])
    // 没有分叉的消息、前面的消息：不显示控件
    Assert.True((ForkLineage.siblings summaries lineage r (u4 - 2UL) (Some(u4 - 3UL))).IsNone)
    // 分叉被删除后版本数减少；只剩原文与一个分叉时仍是 2 个版本
    b.Delete f2
    let proj2 = b.Projection
    let s2 = ConversationSummary.parseList (listRows proj2)
    let onOriginal2 = ForkLineage.siblings s2 (ForkLineage.parse (listRows proj2)) r u4 (Some a3) |> Option.get
    Assert.Equal(2, onOriginal2.count)

[<Fact>]
let ``editing the first message makes siblings at the start of the conversation`` () =
    let b = Builder()
    let r = b.Conversation "根"
    let u2 = b.User(r, "第一句")
    b.Assistant(r, "回") |> ignore
    let f = b.Fork(r, None)
    let fu = b.User(f, "第一句改写")
    let proj = b.Projection
    let rows = listRows proj
    let summaries = ConversationSummary.parseList rows
    let lineage = ForkLineage.parse rows
    let onOriginal = ForkLineage.siblings summaries lineage r u2 None |> Option.get
    Assert.Equal<Guid list>([ r; f ], onOriginal.targets)
    let onFork = ForkLineage.siblings summaries lineage f fu None |> Option.get
    Assert.Equal(1, onFork.index)

[<Fact>]
let ``when the original conversation is deleted the fork has no switch control`` () =
    let b = Builder()
    let r = b.Conversation "根"
    b.User(r, "x") |> ignore
    let a = b.Assistant(r, "y")
    let u = b.User(r, "z")
    let f = b.Fork(r, Some a)
    let fu = b.User(f, "z'")
    b.Delete r
    let proj = b.Projection
    let rows = listRows proj
    Assert.True((ForkLineage.siblings (ConversationSummary.parseList rows) (ForkLineage.parse rows) f fu (Some a)).IsNone)
    ignore u

[<Fact>]
let ``visible hides rows pending deletion until undo`` () =
    let b = Builder()
    let r = b.Conversation "甲"
    let s = b.Conversation "乙"
    let summaries = ConversationSummary.parseList (listRows b.Projection)
    let state, batch = PendingDeletion.schedule [ r, "甲" ] PendingDeletion.empty
    Assert.Equal<Guid list>([ s ], PendingDeletion.visible summaries state |> List.map _.id)
    let state2, _ = PendingDeletion.undo batch.Value.token state
    Assert.Equal(2, (PendingDeletion.visible summaries state2).Length)
