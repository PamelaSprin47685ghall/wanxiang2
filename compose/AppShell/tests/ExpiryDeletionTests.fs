module Wanxiang2.AppShell.Tests.ExpiryDeletionTests

// 旅程 6 的另一半：**到期真删**。
//
// 原来的旅程 6 只验了「待删批次」与「撤销把批次清空」——撤销那条路走完，
// 对话从来没被删过，于是「宽限期过后系统真的把对话删了」这件事一直没证据。
//
// 这里的判据是三段：
//   1. 到期前：批次还在，客户端**没有**发出任何删除命令，存储里的对话还在。
//   2. 推进假时钟过宽限期后：客户端发出**恰好一条** `conversation.delete` 命令，
//      批次从待删表里消失。
//   3. 这条命令真的能让存储里的对话消失（服务端按同一条命令规划并提交，
//      投影里的对话被标记删除、列表里不再出现）。
//
open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang2.WorkspaceState
open Wanxiang2.AppShell.Tests

let private tempDir () =
    let dir =
        Path.Combine(Path.GetTempPath(), "wanxiang-expiry-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory dir |> ignore
    dir

let private testConfig () : SessionConfig =
    {
        provider = "openai"
        model = "test-model"
        instructions = None
        tools = []
        temperature = None
        topP = None
        maxTokens = None
        thinkingBudget = None
        extraJson = None
    }

/// 服务端：建一段对话，提交进事件存储。
let private seedConversation (store: Wanxiang.Store.EventStore) (convId: Guid) =
    let createCmd =
        ClientCommand.CreateConversation
            {|
                invocationId = Guid.CreateVersion7()
                conversationId = convId
                title = "待删的对话"
                config = testConfig ()
            |}

    match CommandEngine.plan store.Projection store.LastCommitId createCmd with
    | PlanResult.Planned plan ->
        match
            store.Submit
                {
                    events = plan.events
                    commandId = Some plan.commandId
                    commandType = Some plan.commandType
                    commandHash = Some plan.canonicalHash
                    nowUtc = None
                }
        with
        | Wanxiang.Store.SubmitResult.Committed _ -> ()
        | other -> failwithf "建对话提交失败: %A" other
    | other -> failwithf "建对话规划失败: %A" other

/// 客户端发出的删除命令（`WireEvent.Command (ClientCommand.DeleteConversation …)`）。
let private deleteCommands (rig: Rig) =
    rig.Fake.RecordedEffects
    |> List.choose (fun e ->
        match e with
        | Effect.SendWire(WireEvent.Command(ClientCommand.DeleteConversation d)) -> Some d
        | _ -> None)

[<Fact>]
let ``旅程 6：宽限期过后到期真删——删除命令真的把对话从存储里删掉`` () =
    Headless.run (fun () ->
        let dir = tempDir ()
        let store = new Wanxiang.Store.EventStore(dir)
        let convId = Guid.NewGuid()
        seedConversation store convId
        // 前置：对话确实在存储里（后面要证明它被删掉，所以先证明它在）。
        Assert.True(
            Projection.conversationList store.Projection
            |> List.exists (fun c -> c.conversationId = convId)
        )

        Assert.True(
            Projection.tryConversation store.Projection convId
            |> Option.exists (fun c -> not c.deleted)
        )

        let rig = Rig.create ()
        // 假时钟：组合者的时间来源（生产环境是系统时钟）。
        rig.Fake.Now <- DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero)
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))
        let item = JsonObject()
        item["conversationId"] <- string convId
        item["title"] <- "待删的对话"

        rig.Shell.Dispatch(
            WireRecv(
                ConversationListSnapshot
                    {|
                        lastCommitId = 0UL
                        items = JsonArray([| item :> JsonNode |])
                    |}
            )
        )

        // 删除：进入待删批次，宽限期 8 秒。
        rig.Shell.Dispatch(User(Sidebar(SidebarUserMsg.BatchDelete [ convId ])))
        Assert.Equal(1, rig.Shell.Model.Feedback.PendingDeletions.Count)
        // 到期前：一条删除命令都没发出去，存储里的对话也还在。
        Assert.Empty(deleteCommands rig)

        Assert.True(
            Projection.conversationList store.Projection
            |> List.exists (fun c -> c.conversationId = convId)
        )

        // 推进假时钟过宽限期，再打一拍 tick。
        rig.Fake.AdvanceSeconds 9.0
        rig.Shell.Dispatch(MsgTick rig.Fake.Now)

        // 到期后：恰好一条删除命令，批次清空。
        let deletes = deleteCommands rig
        Assert.Equal(1, deletes.Length)
        Assert.Equal(convId, deletes.Head.conversationId)
        Assert.Empty(rig.Shell.Model.Feedback.PendingDeletions |> Map.toList)

        // 这条命令真的能让存储里的对话消失：按同一条命令规划并提交。
        let deleteCmd =
            ClientCommand.DeleteConversation
                {|
                    invocationId = deletes.Head.invocationId
                    conversationId = deletes.Head.conversationId
                |}

        match CommandEngine.plan store.Projection store.LastCommitId deleteCmd with
        | PlanResult.Planned plan ->
            match
                store.Submit
                    {
                        events = plan.events
                        commandId = Some plan.commandId
                        commandType = Some plan.commandType
                        commandHash = Some plan.canonicalHash
                        nowUtc = None
                    }
            with
            | Wanxiang.Store.SubmitResult.Committed _ -> ()
            | other -> failwithf "删除提交失败: %A" other
        | other -> failwithf "删除规划失败: %A" other

        // 真删了：列表里不再出现，投影里标记为已删除。
        Assert.False(
            Projection.conversationList store.Projection
            |> List.exists (fun c -> c.conversationId = convId)
        )

        match Projection.tryConversation store.Projection convId with
        | Some c -> Assert.True(c.deleted)
        | None -> Assert.Fail "删除后投影里应仍保留已删除的对话记录（墓碑）"

        store.Shutdown()
        Directory.Delete(dir, true))
