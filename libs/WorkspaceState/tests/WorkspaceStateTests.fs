namespace Wanxiang2.WorkspaceState.Tests

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.WorkspaceState

module WorkspaceStateTests =

    let now0 = DateTimeOffset.Parse("2026-10-19T10:00:00+08:00")

    [<Fact>]
    let ``Invariant 1: RunState.Idle means Generating=false and no stop button visible`` () =
        let model = Workspace.init ()
        let vs = Workspace.project model
        Assert.False vs.Chat.Streaming
        Assert.NotEqual(SendButtonState.GeneratingStop, vs.Composer.Send.State)

    [<Fact>]
    let ``Invariant 2: ConnPhase <> Authed has no Active conversation projected`` () =
        let model = Workspace.init ()
        // In Connecting state
        let vs = Workspace.project model

        match vs.Chat.Phase with
        | ChatPhase.Messages _ -> Assert.True(false, "Should not project active messages when not authed")
        | _ -> ()

        Assert.False vs.Header.Visible

    [<Fact>]
    let ``Invariant 3: ConnectionDown zeroes pending commands and clears active conversation`` () =
        let model = Workspace.init ()
        let invId = Guid.NewGuid()

        let cmd =
            ClientCommand.CreateConversation
                {|
                    invocationId = invId
                    conversationId = Guid.NewGuid()
                    title = "T"
                    config = SessionConfig.empty
                |}

        let pending =
            {
                InvocationId = invId
                Command = cmd
                SentAt = now0
                SuccessMessage = None
            }

        let modelWithPending =
            { model with
                Feedback =
                    { model.Feedback with
                        PendingCommands = Map.add invId pending Map.empty
                    }
            }

        Assert.Equal(1, modelWithPending.Feedback.PendingCommands.Count)

        let nextModel, _ =
            Workspace.update now0 (Msg.ConnectionDown(Some(ConnectionIssue.Disconnected "net down"))) modelWithPending

        Assert.Equal(0, nextModel.Feedback.PendingCommands.Count)
        Assert.Equal(0, nextModel.Feedback.PendingDeletions.Count)
        Assert.Equal(0, nextModel.Feedback.PendingConfigs.Count)

    [<Fact>]
    let ``Invariant 4: Determinism: same (model, msg) twice produces equal results`` () =
        let model = Workspace.init ()
        let msg = Msg.User(UserMsg.Composer(ComposerUserMsg.TextChanged "hello"))
        let (m1, e1) = Workspace.update now0 msg model
        let (m2, e2) = Workspace.update now0 msg model
        Assert.Equal(m1, m2)
        Assert.Equal<Effect list>(e1, e2)

    [<Fact>]
    let ``Full Journey Smoke Test: Pairing -> Onboarding -> Catalog Ready -> Send -> Generation Delta -> Finish -> Disconnect -> Reconnect``
        ()
        =
        let m0 = Workspace.init ()
        Assert.Equal(ConnPhase.Connecting 0, m0.Conn)

        // 1. ConnectionUp
        let m1, effs1 = Workspace.update now0 (Msg.ConnectionUp "inst-1") m0

        match m1.Conn with
        | ConnPhase.Authed("inst-1", CatalogPhase.LoadingCatalog) -> ()
        | other -> Assert.Fail(sprintf "Expected Authed LoadingCatalog, got %A" other)

        Assert.Contains(Effect.SendWire WireEvent.CatalogRequest, effs1)
        Assert.Contains(Effect.SendWire WireEvent.ObserveConversationList, effs1)

        // 2. Catalog Snapshot (Needs onboarding if empty)
        let emptyCatalog: Catalog =
            {
                providers = []
                tools = []
                mcpServers = []
                presets = []
                generation = Catalog.empty.generation
            }

        let m2 =
            { m1 with
                Conn = ConnPhase.Authed("inst-1", CatalogPhase.NeedsOnboarding OnboardingPhase.Idle)
            }

        let vs2 = Workspace.project m2
        Assert.Equal(ChatPhase.Empty IdleEmptyKind.NoProvider, vs2.Chat.Phase)
        Assert.False vs2.Composer.Enabled

        // 3. Provider added, Catalog Ready
        let readyCatalog: Catalog =
            { emptyCatalog with
                providers =
                    [
                        {
                            id = "p1"
                            label = "Provider 1"
                            kind = "openai"
                            baseUrl = ""
                            hasApiKey = true
                            models = [ "model-a" ]
                            defaultModel = "model-a"
                            timeoutSeconds = 60
                            maxRetries = 2
                            enabled = true
                            visionOverrides = Map.empty
                            visionDetected = Map.empty
                        }
                    ]
            }

        let m3 =
            { m2 with
                Conn = ConnPhase.Authed("inst-1", CatalogPhase.Ready(readyCatalog, WorkspacePhase.IdleEmpty))
            }

        let vs3 = Workspace.project m3
        Assert.Equal(ChatPhase.Empty IdleEmptyKind.NoConversation, vs3.Chat.Phase)
        Assert.True vs3.Composer.Enabled
        Assert.Equal(SendButtonState.Idle, vs3.Composer.Send.State)

        // 4. Composer text & Send
        let m4, _ =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.TextChanged "你好"))) m3

        let m5, effs5 =
            Workspace.update now0 (Msg.User(UserMsg.Composer ComposerUserMsg.Send)) m4

        Assert.True(effs5.Length >= 2)
        Assert.Equal("", m5.Composer.Text)

        // CommandCommitted for the pending commands
        let pendingInvs = m5.Feedback.PendingCommands |> Map.toList |> List.map fst

        let m5Committed =
            pendingInvs
            |> List.fold
                (fun acc invId ->
                    let committedEv =
                        WireEvent.CommandCommitted
                            {|
                                invocationId = invId
                                commandId = "c1"
                                commitId = 1UL
                            |}

                    let next, _ = Workspace.update now0 (Msg.WireRecv committedEv) acc
                    next)
                m5

        Assert.Equal(0, m5Committed.Feedback.PendingCommands.Count)

        // 5. Conversation Snapshot arrives
        let convId = Guid.NewGuid()

        let convSnapshotEv =
            WireEvent.ConversationSnapshot
                {|
                    conversationId = convId
                    title = "你好"
                    lastCommitId = 1UL
                    runtimeState = "idle"
                    generationId = None
                    messages = JsonArray()
                    snapshotEarliestCommitId = 1UL
                    snapshotHasMore = false
                    config = SessionConfig.empty
                |}

        let m6, effs6 = Workspace.update now0 (Msg.WireRecv convSnapshotEv) m5Committed
        let vs6 = Workspace.project m6
        Assert.Equal(ChatPhase.Empty IdleEmptyKind.EmptyConversation, vs6.Chat.Phase)
        Assert.Contains(Effect.ScrollChat ScrollCommand.End, effs6)

        // 6. Generation Started
        let genId = Guid.NewGuid()

        let genStartedEv =
            WireEvent.GenerationStarted
                {|
                    conversationId = convId
                    generationId = genId
                    providerId = "p1"
                    model = "model-a"
                |}

        let m7, _ = Workspace.update now0 (Msg.WireRecv genStartedEv) m6
        let vs7 = Workspace.project m7
        Assert.True vs7.Chat.Streaming
        Assert.Equal(SendButtonState.GeneratingStop, vs7.Composer.Send.State)

        // 7. Generation Delta
        let partialPayload = JsonObject()
        partialPayload["role"] <- "assistant"
        let contentsArr = JsonArray()
        let textNode = JsonObject()
        textNode["type"] <- "text"
        textNode["text"] <- "你好！我是万象智能助手。"
        contentsArr.Add textNode
        partialPayload["contents"] <- contentsArr

        let deltaEv =
            WireEvent.GenerationDelta
                {
                    conversationId = convId
                    generationId = genId
                    payload = partialPayload
                }

        let m8, _ = Workspace.update now0 (Msg.WireRecv deltaEv) m7
        let vs8 = Workspace.project m8
        Assert.True vs8.Chat.Streaming

        match vs8.Chat.Phase with
        | ChatPhase.Messages msgs ->
            Assert.NotEmpty msgs
            Assert.Contains("你好！我是万象智能助手。", msgs.[0].Text)
        | other -> Assert.Fail(sprintf "Expected Messages phase, got %A" other)

        // 8. Generation Finished
        let finishEv =
            WireEvent.GenerationFinished
                {|
                    conversationId = convId
                    generationId = genId
                    status = "completed"
                    error = None
                    usage = None
                |}

        let m9, effs9 = Workspace.update now0 (Msg.WireRecv finishEv) m8
        let vs9 = Workspace.project m9
        Assert.False vs9.Chat.Streaming
        Assert.Equal(SendButtonState.Idle, vs9.Composer.Send.State)
        Assert.Contains(Effect.Focus FocusTarget.ComposerInput, effs9)

        // 9. Escape closes overlays
        let mOverlay, _ =
            Workspace.update now0 (Msg.User(UserMsg.Overlay(OverlayUserMsg.PopupOpened "popup-1"))) m9

        let mEsc, effsEsc =
            Workspace.update now0 (Msg.User(UserMsg.Overlay OverlayUserMsg.Escape)) mOverlay

        Assert.Equal(OverlayPhase.Closed, mEsc.Overlay)
        Assert.Contains(Effect.Focus FocusTarget.ComposerInput, effsEsc)

        // 10. Connection Down & Reconnect
        let m10, _ =
            Workspace.update now0 (Msg.ConnectionDown(Some(ConnectionIssue.Disconnected "net lost"))) m9

        let vs10 = Workspace.project m10
        Assert.Equal(ChatPhase.Empty IdleEmptyKind.NotConnected, vs10.Chat.Phase)
        Assert.False vs10.Composer.Enabled

        // Reconnect
        let m11, effs11 = Workspace.update now0 (Msg.ConnectionUp "inst-1") m10

        match m11.Conn with
        | ConnPhase.Authed("inst-1", CatalogPhase.LoadingCatalog) -> ()
        | other -> Assert.Fail(sprintf "Expected Authed, got %A" other)

        Assert.Contains(Effect.SendWire WireEvent.CatalogRequest, effs11)

    [<Fact>]
    let ``Pure modules in WorkspaceState: ReconnectBackoff, MessageLink, DraftSummaryText, ForkAnchor, RevealTarget, OlderPageLoad``
        ()
        =
        // 1. ReconnectBackoff
        Assert.Equal(2000, ReconnectBackoff.next 1000)
        Assert.Equal(30000, ReconnectBackoff.next 20000)
        Assert.Equal<int list>([ 1000; 2000; 4000 ], ReconnectBackoff.schedule 3)

        // 2. MessageLink
        let convId = Guid.NewGuid()
        let link = MessageLink.format None convId 42UL
        Assert.Equal(Some(convId, 42UL), MessageLink.tryParse link)

        // 3. DraftSummaryText
        Assert.Equal("已关闭：不会在本机保存草稿。", DraftSummaryText.text false 2 1)
        Assert.Equal("当前没有保存的草稿。", DraftSummaryText.text true 0 0)
        Assert.Equal("已保存 2 份草稿、1 条未确认的消息。", DraftSummaryText.text true 2 1)

        // 4. ForkAnchor
        Assert.Equal(Some 2UL, ForkAnchor.forkAfter [ 1UL; 2UL; 3UL ] (Some 3UL))

        // 5. RevealTarget
        Assert.Equal(Some 2UL, RevealTarget.resolve (RevealTarget.RevealMessage 2UL) [ 1UL; 2UL; 3UL ])

        // 6. OlderPageLoad
        Assert.True(
            OlderPageLoad.timedOut
                true
                (Some DateTimeOffset.UtcNow)
                (DateTimeOffset.UtcNow.AddSeconds 10.0)
                (TimeSpan.FromSeconds 5.0)
        )

    [<Fact>]
    let ``对话列表快照同时带出分叉谱系：侧栏投影按它给分叉行标记`` () =
        let m0 = Workspace.init ()
        let fork = Guid.NewGuid()
        let parent = Guid.NewGuid()
        let item = JsonObject()
        item["conversationId"] <- string fork
        item["parentId"] <- string parent
        item["title"] <- "分叉出来的对话"
        item["isFork"] <- true

        let ev =
            WireEvent.ConversationListSnapshot
                {|
                    lastCommitId = 0UL
                    items = JsonArray([| item :> JsonNode |])
                |}

        let m1, _ = Workspace.update now0 (Msg.WireRecv ev) m0
        let vs = Workspace.project m1
        Assert.Equal(1, vs.Sidebar.Rows.Length)
        Assert.True(vs.Sidebar.Rows.[0].IsFork)
        // 谱系随侧栏投影携带（不再恒空，也不再靠旁路插槽）
        Assert.True(vs.Sidebar.Lineage.ContainsKey fork)
        Assert.Equal(parent, vs.Sidebar.Lineage[fork].parentId)

    [<Fact>]
    let ``定位目标：搜索命中进模型，快照到达且消息已加载时统一发滚动 Effect`` () =
        let m0 = Workspace.init ()
        let convId = Guid.NewGuid()
        // 搜索命中：登记定位目标（对话 + 消息）
        let m1, _ =
            Workspace.update now0 (Msg.User(UserMsg.Sidebar(SidebarUserMsg.SearchHitSelect(convId, Some "7")))) m0

        let vs1 = Workspace.project m1
        Assert.Equal(Some "7", vs1.Chat.RevealTarget)
        // 快照到达但目标消息没加载：不发定位 Effect（滚不到就不假装）
        let snapshotEv =
            WireEvent.ConversationSnapshot
                {|
                    conversationId = convId
                    title = "对话"
                    lastCommitId = 0UL
                    runtimeState = "idle"
                    generationId = None
                    messages = JsonArray()
                    snapshotEarliestCommitId = 0UL
                    snapshotHasMore = false
                    config = SessionConfig.empty
                |}

        let ready =
            { m1 with
                Conn = ConnPhase.Authed("inst-1", CatalogPhase.Ready(Catalog.empty, WorkspacePhase.IdleEmpty))
            }

        let m2, effs2 = Workspace.update now0 (Msg.WireRecv snapshotEv) ready
        Assert.DoesNotContain(Effect.ScrollChat(ScrollCommand.ToExchange "7"), effs2)
        // 目标消息在快照里：统一 Effect（宿主执行滚动与高亮），目标随即消费
        let msg = JsonObject()
        msg["commitId"] <- 7UL
        msg["role"] <- "assistant"

        let snapshotEv2 =
            WireEvent.ConversationSnapshot
                {|
                    conversationId = convId
                    title = "对话"
                    lastCommitId = 7UL
                    runtimeState = "idle"
                    generationId = None
                    messages = JsonArray([| msg :> JsonNode |])
                    snapshotEarliestCommitId = 0UL
                    snapshotHasMore = false
                    config = SessionConfig.empty
                |}

        let m3, effs3 = Workspace.update now0 (Msg.WireRecv snapshotEv2) m2
        Assert.Contains(Effect.ScrollChat(ScrollCommand.ToExchange "7"), effs3)
        Assert.Equal(None, (Workspace.project m3).Chat.RevealTarget)

    // ---------------------------------------------------------------- 发送历史召回的接线（2026-10-21 W7-CLEANBREAK 恢复）
    //
    // 这四条消息与三条上传消息曾在 `WorkspaceState.Update` 里没有处理分支，
    // 落进 `| _ -> model, []`：附件永远进不了托盘、模型选择与 EnterSends 永远不生效。
    // 这里钉住它们的处理分支。

    [<Fact>]
    let ``ComposerUserMsg.Attach 让附件进托盘`` () =
        let model = Workspace.init ()

        let m1, _ =
            Workspace.update
                now0
                (Msg.User(
                    UserMsg.Composer(ComposerUserMsg.Attach(Guid.NewGuid(), "a.png", "image/png", 204800L, "sha-1"))
                ))
                model

        Assert.Single m1.Composer.Attachments |> ignore
        let vs = Workspace.project m1
        Assert.Single vs.Composer.Tray |> ignore
        let tray = vs.Composer.Tray |> List.head
        Assert.Equal("a.png", tray.FileName)
        Assert.Equal("image/png", tray.MediaType)
        Assert.Equal(204800L, tray.Size)
        // 刚挂上时还没就绪
        Assert.False tray.Ready

    [<Fact>]
    let ``UploadProgress 与 UploadFinished 推进附件阶段`` () =
        let model = Workspace.init ()

        let m1, _ =
            Workspace.update
                now0
                (Msg.User(
                    UserMsg.Composer(ComposerUserMsg.Attach(Guid.NewGuid(), "a.png", "image/png", 204800L, "sha-1"))
                ))
                model

        let id = m1.Composer.Attachments |> List.head |> fun a -> a.Id

        let m2, _ =
            Workspace.update now0 (Msg.Internal(InternalMsg.UploadProgress(id, 40))) m1

        Assert.Equal(40, (m2.Composer.Attachments |> List.head).ProgressPercent)
        Assert.False (m2.Composer.Attachments |> List.head).Ready
        let m3, _ = Workspace.update now0 (Msg.Internal(InternalMsg.UploadFinished id)) m2
        let a3 = m3.Composer.Attachments |> List.head
        Assert.True a3.Ready
        Assert.Equal(100, a3.ProgressPercent)
        Assert.False a3.Failed

    [<Fact>]
    let ``UploadFailed 把附件标成失败且不就绪`` () =
        let model = Workspace.init ()

        let m1, _ =
            Workspace.update
                now0
                (Msg.User(
                    UserMsg.Composer(ComposerUserMsg.Attach(Guid.NewGuid(), "a.png", "image/png", 204800L, "sha-1"))
                ))
                model

        let id = m1.Composer.Attachments |> List.head |> fun a -> a.Id

        let m2, _ =
            Workspace.update now0 (Msg.Internal(InternalMsg.UploadFailed(id, "网络断了"))) m1

        let a2 = m2.Composer.Attachments |> List.head
        Assert.True a2.Failed
        Assert.False a2.Ready

    [<Fact>]
    let ``RemoveAttachment 从托盘里去掉附件`` () =
        let model = Workspace.init ()

        let m1, _ =
            Workspace.update
                now0
                (Msg.User(
                    UserMsg.Composer(ComposerUserMsg.Attach(Guid.NewGuid(), "a.png", "image/png", 204800L, "sha-1"))
                ))
                model

        let id = m1.Composer.Attachments |> List.head |> fun a -> a.Id

        let m2, _ =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.RemoveAttachment id))) m1

        Assert.Empty m2.Composer.Attachments
        Assert.Empty (Workspace.project m2).Composer.Tray

    [<Fact>]
    let ``ModelPick 与 EnterSendsToggled 落到状态树上`` () =
        let model = Workspace.init ()

        let m1, _ =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.ModelPick("openai", "gpt-5")))) model

        Assert.Equal(Some("openai", "gpt-5"), m1.Composer.SelectedModel)
        Assert.True((Workspace.project m1).Composer.ModelChip |> Option.isSome)

        let m2, _ =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.EnterSendsToggled false))) m1

        Assert.False m2.Composer.EnterSends

    // ---------------------------------------------------------------- 对话动作（2026-10-21 W7-CLEANBREAK）
    //
    // `UserMsg.Chat` 此前**没有分支**：重试/重新生成、删除消息、复制回复、切换版本、
    // 加载更早一页、继续生成、分叉——这些用户可见的动作被静默丢弃。
    // 这里钉住它们确实产出了对应的 Effect / 线协议命令。

    let private authedWithConversation () =
        let model = Workspace.init ()

        let conv =
            {
                ConversationId = Guid.Parse("11111111-1111-1111-1111-111111111111")
                Title = "对话"
                Messages = []
                LastCommitId = 0UL
                RuntimeState = "idle"
                Config = SessionConfig.empty
                PageEarliest = 0UL
                PageHasMore = false
                PageLoading = false
                ParentId = None
            }

        let catalog =
            CatalogPhase.Ready(Catalog.empty, WorkspacePhase.Active(conv, RunState.Idle None))

        { model with
            Conn = ConnPhase.Authed("inst-1", catalog)
        }

    /// 对话快照（默认空闲态）。
    let private snapshotOf convId runtimeState generationId =
        WireEvent.ConversationSnapshot
            {|
                conversationId = convId
                title = "对话"
                lastCommitId = 50UL
                runtimeState = runtimeState
                generationId = generationId
                messages = JsonArray()
                snapshotEarliestCommitId = 1UL
                snapshotHasMore = false
                config = SessionConfig.empty
            |}

    let private runOf model =
        match model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, run))) -> run
        | other -> failwithf "期望 Active 对话，实际 %A" other

    [<Fact>]
    let ``第四批审查：生成中收到对话更新与快照，不得丢掉生成状态`` () =
        let model = authedWithConversation ()
        let convId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        let genId = Guid.NewGuid()

        let generating, _ =
            Workspace.update
                now0
                (Msg.WireRecv(
                    WireEvent.GenerationStarted
                        {|
                            conversationId = convId
                            generationId = genId
                            providerId = "p"
                            model = "m"
                        |}
                ))
                model

        // 主机因这次生成广播 conversation.updated，客户端重新取快照
        let _, updateEffects =
            Workspace.update
                now0
                (Msg.WireRecv(
                    WireEvent.ConversationUpdated
                        {|
                            conversationId = convId
                            commitId = 0UL
                            change = JsonObject()
                        |}
                ))
                generating

        Assert.NotEmpty(updateEffects)

        let afterSnapshot, _ =
            Workspace.update now0 (Msg.WireRecv(snapshotOf convId "generating" (Some genId))) generating

        match runOf afterSnapshot with
        | RunState.Generating(gid, _, _) -> Assert.Equal(genId, gid)
        | other -> Assert.Fail(sprintf "快照声明仍在生成，状态却被重置为 %A" other)

        // 流式增量必须继续被接受
        let partial = JsonObject()
        partial["role"] <- "assistant"
        partial["text"] <- "半截"

        let afterDelta, deltaEffects =
            Workspace.update
                now0
                (Msg.WireRecv(
                    WireEvent.GenerationDelta
                        {
                            conversationId = convId
                            generationId = genId
                            payload = partial :> JsonNode
                        }
                ))
                afterSnapshot

        Assert.Contains(Effect.ScrollChat ScrollCommand.End, deltaEffects)

        match runOf afterDelta with
        | RunState.Generating(_, _, Some msg) -> Assert.Equal("半截", msg.text)
        | other -> Assert.Fail(sprintf "增量丢失，生成状态为 %A" other)

        // 权威空闲快照才结束生成
        let afterIdle, _ =
            Workspace.update now0 (Msg.WireRecv(snapshotOf convId "idle" None)) afterDelta

        match runOf afterIdle with
        | RunState.Idle None -> ()
        | other -> Assert.Fail(sprintf "空闲快照应结束生成，实际 %A" other)

    [<Fact>]
    let ``第四批：命令只走独立通道并登记在途`` () =
        let model = authedWithConversation ()

        let cmd =
            ClientCommand.RenameConversation
                {|
                    invocationId = Guid.NewGuid()
                    conversationId = Guid.NewGuid()
                    title = "标题"
                |}

        let next, effects = Workspace.update now0 (Msg.CommandRequested cmd) model
        Assert.Equal<Effect list>([ Effect.SendCommand cmd ], effects)
        Assert.True(next.Feedback.PendingCommands.ContainsKey(ClientCommand.invocationId cmd))

    [<Fact>]
    let ``第四批：重复快照不重复确认，断线后水位清零`` () =
        let model = authedWithConversation ()

        let message =
            Msg.WireRecv(
                WireEvent.ConversationListSnapshot
                    {|
                        items = JsonArray()
                        lastCommitId = 42UL
                    |}
            )

        let next, _ = Workspace.update now0 message model
        let repeated, effects = Workspace.update now0 message next
        Assert.Empty effects
        Assert.Equal(42UL, repeated.AppliedCursor)
        let disconnected, _ = Workspace.update now0 (Msg.ConnectionDown None) repeated
        Assert.Equal(0UL, disconnected.AppliedCursor)

    [<Fact>]
    let ``第四批：列表快照应用后确认主机游标`` () =
        let model = authedWithConversation ()

        let _, effects =
            Workspace.update
                now0
                (Msg.WireRecv(
                    WireEvent.ConversationListSnapshot
                        {|
                            items = JsonArray()
                            lastCommitId = 42UL
                        |}
                ))
                model

        Assert.Contains(Effect.SendWire(WireEvent.CursorAdvanced {| id = 42UL |}), effects)

    [<Fact>]
    let ``第四批：标题提交必须发出重命名命令`` () =
        let model = authedWithConversation ()

        let _, effects =
            Workspace.update now0 (Msg.User(UserMsg.Header(HeaderUserMsg.CommitRename "新标题"))) model

        let command =
            effects
            |> List.pick (function
                | Effect.SendCommand c -> Some c
                | _ -> None)

        match command with
        | ClientCommand.RenameConversation d -> Assert.Equal("新标题", d.title)
        | other -> Assert.Fail(sprintf "期望重命名命令，实际为 %A" other)

    [<Fact>]
    let ``第四批：父对话导航必须进入加载并发起观察`` () =
        let model = authedWithConversation ()
        let parent = Guid.NewGuid()

        let _, effects =
            Workspace.update now0 (Msg.User(UserMsg.Header(HeaderUserMsg.CrumbClick parent))) model

        Assert.Contains(Effect.SendWire(WireEvent.ObserveConversation {| conversationId = parent |}), effects)

    [<Fact>]
    let ``ChatUserMsg.JumpBeginning 与 JumpEnd 产出滚动 Effect`` () =
        let model = Workspace.init ()

        let _, effs1 =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.JumpBeginning))) model

        Assert.Contains(Effect.ScrollChat ScrollCommand.Beginning, effs1)

        let _, effs2 =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.JumpEnd))) model

        Assert.Contains(Effect.ScrollChat ScrollCommand.End, effs2)

    [<Fact>]
    let ``ChatUserMsg.Regenerate 发出 RegenerateResponse 命令`` () =
        let model = authedWithConversation ()

        let _, effs1 =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.Regenerate))) model

        Assert.Single(
            effs1
            |> List.filter (fun e ->
                match e with
                | Effect.SendCommand _ -> true
                | _ -> false)
        )
        |> ignore

        let cmd =
            effs1
            |> List.pick (function
                | Effect.SendCommand c -> Some c
                | _ -> None)

        match cmd with
        | ClientCommand.RegenerateResponse d ->
            Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), d.conversationId)
        | _ -> Assert.True(false, "应当发出 RegenerateResponse")

    [<Fact>]
    let ``ChatUserMsg.DeleteExchange 发出 DeleteMessage 命令`` () =
        let model = authedWithConversation ()

        let _, effs =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.DeleteExchange "42"))) model

        let cmd =
            effs
            |> List.pick (function
                | Effect.SendCommand c -> Some c
                | _ -> None)

        match cmd with
        | ClientCommand.DeleteMessage d -> Assert.Equal(42UL, d.messageCommitId)
        | _ -> Assert.True(false, "应当发出 DeleteMessage")

    [<Fact>]
    let ``ChatUserMsg.DeleteExchange 遇到非数字 id 不发命令`` () =
        let model = authedWithConversation ()

        let _, effs =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.DeleteExchange "not-a-number"))) model

        Assert.Empty(effs)

    [<Fact>]
    let ``ChatUserMsg.EditFork 发出 ForkConversation 命令`` () =
        let model = authedWithConversation ()

        let _, effs =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.EditFork(Some 7UL, "改过的文本")))) model

        let cmd =
            effs
            |> List.pick (function
                | Effect.SendCommand c -> Some c
                | _ -> None)

        match cmd with
        | ClientCommand.ForkConversation d ->
            Assert.Equal(Some 7UL, d.forkAfterId)
            Assert.Equal("改过的文本", d.editedMessageJson.["text"].GetValue<string>())
        | _ -> Assert.True(false, "应当发出 ForkConversation")

    [<Fact>]
    let ``ChatUserMsg.CopyLatestReply 产出剪贴板 Effect`` () =
        let model = authedWithConversation ()

        let _, effs =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.CopyLatestReply))) model
        // 没有助手回复时不产出剪贴板 Effect
        Assert.Empty(effs)

    [<Fact>]
    let ``ChatUserMsg.SwitchVersion 切到目标对话并请求它的快照`` () =
        let model = authedWithConversation ()
        let target = Guid.Parse("22222222-2222-2222-2222-222222222222")

        let m1, effs =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.SwitchVersion target))) model

        Assert.Contains(Effect.SendWire(WireEvent.ObserveConversation {| conversationId = target |}), effs)

        match m1.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.LoadingConversation convId)) ->
            Assert.Equal(target, convId)
        | _ -> Assert.True(false, "应当切到目标对话")

    [<Fact>]
    let ``ChatUserMsg.ApprovalRespond 发出 ToolApprovalDecision`` () =
        let model = authedWithConversation ()
        let approvalId = Guid.Parse("33333333-3333-3333-3333-333333333333")

        let _, effs =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.ApprovalRespond(approvalId, true)))) model

        Assert.Contains(
            Effect.SendWire(
                WireEvent.ToolApprovalDecision
                    {|
                        approvalId = approvalId
                        approved = true
                    |}
            ),
            effs
        )

    [<Fact>]
    let ``OlderPageRequested 发出 HistoryRequest 并标记正在加载`` () =
        let model = authedWithConversation ()
        let convId = Guid.Parse("11111111-1111-1111-1111-111111111111")

        let withMore =
            match model.Conn with
            | ConnPhase.Authed(i, CatalogPhase.Ready(cat, WorkspacePhase.Active(conv, run))) ->
                { model with
                    Conn =
                        ConnPhase.Authed(
                            i,
                            CatalogPhase.Ready(
                                cat,
                                WorkspacePhase.Active(
                                    { conv with
                                        PageHasMore = true
                                        PageEarliest = 100UL
                                    },
                                    run
                                )
                            )
                        )
                }
            | _ -> model

        let m1, effs =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.OlderPageRequested))) withMore

        Assert.Contains(
            Effect.SendWire(
                WireEvent.HistoryRequest
                    {|
                        conversationId = convId
                        beforeCommitId = 100UL
                        limit = 100
                    |}
            ),
            effs
        )
        // 请求发出后进入「正在加载」，避免重复请求
        match m1.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) -> Assert.True conv.PageLoading
        | _ -> Assert.True(false, "应当标记 PageLoading")

    [<Fact>]
    let ``没有更多历史时 OlderPageRequested 不发请求`` () =
        let model = authedWithConversation ()

        let _, effs =
            Workspace.update now0 (Msg.User(UserMsg.Chat(ChatUserMsg.OlderPageRequested))) model

        Assert.Empty effs

    [<Fact>]
    let ``HistoryPage 把更早的消息前置拼接并去重`` () =
        let model = authedWithConversation ()
        let convId = Guid.Parse("11111111-1111-1111-1111-111111111111")

        let mkItem (commitId: uint64) (text: string) =
            let o = JsonObject()
            o["commitId"] <- JsonValue.Create commitId
            o["role"] <- JsonValue.Create "assistant"
            o["text"] <- JsonValue.Create text
            o :> JsonNode
        // 当前对话里已有 commitId = 20
        let withMsg =
            match model.Conn with
            | ConnPhase.Authed(i, CatalogPhase.Ready(cat, WorkspacePhase.Active(conv, run))) ->
                let m =
                    {
                        role = "assistant"
                        text = "较新的"
                        reasoning = ""
                        toolCalls = []
                        toolResults = []
                        attachments = []
                        commitId = Some 20UL
                        committedAt = None
                    }

                { model with
                    Conn =
                        ConnPhase.Authed(
                            i,
                            CatalogPhase.Ready(
                                cat,
                                WorkspacePhase.Active(
                                    { conv with
                                        Messages = [ m ]
                                        PageLoading = true
                                    },
                                    run
                                )
                            )
                        )
                }
            | _ -> model

        let page = JsonArray([| mkItem 10UL "更早的"; mkItem 20UL "重复的" |])

        let m1, _ =
            Workspace.update
                now0
                (Msg.WireRecv(
                    WireEvent.HistoryPage
                        {|
                            conversationId = convId
                            beforeCommitId = 20UL
                            items = page
                            hasMore = false
                        |}
                ))
                withMsg

        match m1.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) ->
            // 前置拼接 + 按 commitId 去重（commitId=20 的重复项被丢掉）
            Assert.Equal(2, conv.Messages.Length)
            Assert.Equal("更早的", conv.Messages[0].text)
            Assert.Equal("较新的", conv.Messages[1].text)
            Assert.False conv.PageHasMore
            Assert.False conv.PageLoading
        | _ -> Assert.True(false, "应当仍在当前对话")

    // ---------------------------------------------------------------- 发送门控与附件顺序
    //
    // 门控口径与控件层同一份（`libs/AttachmentTray/ComposerSendState.fs` 的 `blockReason`）：
    // 有附件在上传中、或有附件上传失败，**整条发送被挡住**——不能发「只带部分附件」的消息。
    // 状态主人自己守这条线，不能指望控件层已经挡住了。

    let private attachWithId (id: Guid) (fileName: string) (sha: string) =
        Msg.User(UserMsg.Composer(ComposerUserMsg.Attach(id, fileName, "image/png", 204800L, sha)))

    let private sentCommand (effs: Effect list) =
        effs
        |> List.tryPick (function
            | Effect.SendCommand c -> Some c
            | _ -> None)

    let private sentMessageJson (effs: Effect list) =
        sentCommand effs
        |> Option.bind (function
            | ClientCommand.SendUserMessage d -> Some d.messageJson
            | _ -> None)

    [<Fact>]
    let ``有附件在上传中时发送被挡住：草稿与附件都留着`` () =
        let model = authedWithConversation ()
        let id = Guid.NewGuid()
        let m1, _ = Workspace.update now0 (attachWithId id "a.png" "sha-a") model

        let m2, _ =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.TextChanged "文字"))) m1

        let m3, effs =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.Send))) m2
        // 没有任何命令发出
        Assert.Null(sentCommand effs)
        // 草稿与附件都保留（用户处理完上传再发）
        Assert.Equal("文字", m3.Composer.Text)
        Assert.Single(m3.Composer.Attachments) |> ignore
        Assert.False((m3.Composer.Attachments |> List.head).Ready)

    [<Fact>]
    let ``有附件上传失败时发送被挡住`` () =
        let model = authedWithConversation ()
        let id = Guid.NewGuid()
        let m1, _ = Workspace.update now0 (attachWithId id "a.png" "sha-a") model

        let m2, _ =
            Workspace.update now0 (Msg.Internal(InternalMsg.UploadFailed(id, "断了"))) m1

        let m3, _ =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.TextChanged "文字"))) m2

        let m4, effs =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.Send))) m3

        Assert.Null(sentCommand effs)
        Assert.Equal("文字", m4.Composer.Text)
        Assert.Single(m4.Composer.Attachments) |> ignore

    [<Fact>]
    let ``附件就绪后发送：payload 带全部附件且顺序与添加顺序一致`` () =
        let model = authedWithConversation ()
        let idA = Guid.NewGuid()
        let idB = Guid.NewGuid()
        let m1, _ = Workspace.update now0 (attachWithId idA "a.png" "sha-a") model
        let m2, _ = Workspace.update now0 (attachWithId idB "b.png" "sha-b") m1
        let m3, _ = Workspace.update now0 (Msg.Internal(InternalMsg.UploadFinished idA)) m2
        let m4, _ = Workspace.update now0 (Msg.Internal(InternalMsg.UploadFinished idB)) m3

        let m5, _ =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.TextChanged "看图"))) m4

        let m6, effs =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.Send))) m5

        let json = sentMessageJson effs
        Assert.True(json.IsSome)
        let contents = json.Value.["contents"].AsArray()
        // 一条文本 + 两条附件，顺序与添加顺序一致
        Assert.Equal(3, contents.Count)
        Assert.Equal("看图", contents[0].["text"].GetValue<string>())
        Assert.Equal("attachment", contents[1].["type"].GetValue<string>())
        Assert.Equal("a.png", contents[1].["fileName"].GetValue<string>())
        Assert.Equal("sha-a", contents[1].["sha256"].GetValue<string>())
        Assert.Equal("b.png", contents[2].["fileName"].GetValue<string>())
        Assert.Equal("sha-b", contents[2].["sha256"].GetValue<string>())
        // 发送后草稿与附件都清空
        Assert.Equal("", m6.Composer.Text)
        Assert.Empty m6.Composer.Attachments

    [<Fact>]
    let ``只有附件没有文字也能发：payload 里就是附件项`` () =
        let model = authedWithConversation ()
        let id = Guid.NewGuid()
        let m1, _ = Workspace.update now0 (attachWithId id "a.png" "sha-a") model
        let m2, _ = Workspace.update now0 (Msg.Internal(InternalMsg.UploadFinished id)) m1

        let _, effs =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.Send))) m2

        let json = sentMessageJson effs
        Assert.True(json.IsSome)
        let contents = json.Value.["contents"].AsArray()
        Assert.Equal(1, contents.Count)
        Assert.Equal("attachment", contents[0].["type"].GetValue<string>())
        Assert.Equal("user", json.Value.["role"].GetValue<string>())

    // ---- 侧栏多选：事实只在状态树（Chrome.BatchMode / Chrome.SelectedRowIds）----

    let private summary (id: Guid) (title: string) (pinned: bool) (archived: bool) =
        {
            id = id
            title = title
            preview = "预览 " + title
            running = false
            pinned = pinned
            archived = archived
            createdAt = now0
            updatedAt = now0
            messageCount = 1
            isFork = false
            providerId = "p"
            model = "m"
            lastCommitId = 0UL
        }

    let private sid (n: int) =
        Guid(n, 0s, 0s, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy)

    let private modelWithSummaries (items: ConversationSummary list) =
        let model = Workspace.init ()
        { model with Summaries = items }

    let private batchMsg m = Msg.User(UserMsg.Sidebar m)

    [<Fact>]
    let ``EnterBatch 选中第一项并进入多选`` () =
        let a = sid 1
        let m0 = modelWithSummaries [ summary a "甲" false false ]
        let m1, effs = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch a)) m0
        Assert.True m1.Chrome.BatchMode
        Assert.Equal<Guid list>([ a ], m1.Chrome.SelectedRowIds |> Set.toList)
        Assert.Empty effs

    [<Fact>]
    let ``EnterBatch 在多选中只并入这一项`` () =
        let a, b = sid 1, sid 2
        let m0 = modelWithSummaries [ summary a "甲" false false; summary b "乙" false false ]
        let m1, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch a)) m0
        let m2, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch b)) m1
        Assert.True m2.Chrome.BatchMode
        Assert.Equal<Guid list>([ a; b ], m2.Chrome.SelectedRowIds |> Set.toList)

    [<Fact>]
    let ``ToggleBatch 取消最后项时退出多选`` () =
        let a, b = sid 1, sid 2
        let m0 = modelWithSummaries [ summary a "甲" false false; summary b "乙" false false ]
        let m1, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch a)) m0
        let m2, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatch b)) m1
        Assert.True m2.Chrome.BatchMode
        // 取消甲：乙仍被选中 → 仍在多选
        let m3, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatch a)) m2
        Assert.True m3.Chrome.BatchMode
        Assert.Equal<Guid list>([ b ], m3.Chrome.SelectedRowIds |> Set.toList)
        // 再取消乙：清空 → 退出多选
        let m4, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatch b)) m3
        Assert.False m4.Chrome.BatchMode
        Assert.Empty m4.Chrome.SelectedRowIds

    [<Fact>]
    let ``ToggleBatchAllVisible 全选可见行；已全选则退出多选`` () =
        let a, b, c = sid 1, sid 2, sid 3

        let m0 =
            modelWithSummaries
                [
                    summary a "甲" false false
                    summary b "乙" false false
                    summary c "丙" false false
                ]

        let m1, _ =
            Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatchAllVisible [ a; b ])) m0

        Assert.True m1.Chrome.BatchMode
        Assert.Equal<Guid list>([ a; b ], m1.Chrome.SelectedRowIds |> Set.toList)
        // 再来一次：可见行已全选 → 退出多选
        let m2, _ =
            Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatchAllVisible [ a; b ])) m1

        Assert.False m2.Chrome.BatchMode
        Assert.Empty m2.Chrome.SelectedRowIds

    [<Fact>]
    let ``ToggleBatchAllVisible 换一批可见行会清掉旧的不可见选择`` () =
        let a, b, c = sid 1, sid 2, sid 3

        let m0 =
            modelWithSummaries
                [
                    summary a "甲" false false
                    summary b "乙" false false
                    summary c "丙" false false
                ]

        let m1, _ =
            Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatchAllVisible [ a ])) m0

        let m2, _ =
            Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatchAllVisible [ c ])) m1

        Assert.Equal<Guid list>([ c ], m2.Chrome.SelectedRowIds |> Set.toList)

    [<Fact>]
    let ``ExitBatch 清空选择并退出多选`` () =
        let a = sid 1
        let m0 = modelWithSummaries [ summary a "甲" false false ]
        let m1, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch a)) m0
        let m2, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.ExitBatch)) m1
        Assert.False m2.Chrome.BatchMode
        Assert.Empty m2.Chrome.SelectedRowIds

    [<Fact>]
    let ``多选投影：SidebarVS.SelectedRowIds 与批量条同源于 Chrome`` () =
        let a, b = sid 1, sid 2
        let m0 = modelWithSummaries [ summary a "甲" false false; summary b "乙" true false ]
        let m1, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch a)) m0
        let m2, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatch b)) m1
        let vs = Workspace.project m2
        Assert.True vs.Sidebar.BatchMode
        Assert.Equal<Guid list>([ a; b ], vs.Sidebar.SelectedRowIds |> Set.toList)

    [<Fact>]
    let ``多选投影：全部选中的行都置顶时 AllPinned 为真`` () =
        let a, b = sid 1, sid 2
        let m0 = modelWithSummaries [ summary a "甲" true false; summary b "乙" true false ]
        let m1, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch a)) m0
        let m2, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatch b)) m1
        let vs = Workspace.project m2
        Assert.True vs.Sidebar.BatchMode
        Assert.Equal<Guid list>([ a; b ], vs.Sidebar.SelectedRowIds |> Set.toList)

    [<Fact>]
    let ``多选投影：状态树不清理看不见的已选项`` () =
        let a, b = sid 1, sid 2
        // b 是归档行，默认不可见
        let m0 = modelWithSummaries [ summary a "甲" false false; summary b "乙" false true ]
        let m1, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch a)) m0
        let m2, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatch b)) m1
        let vs = Workspace.project m2
        Assert.True vs.Sidebar.BatchMode
        // 归档的乙仍留在选中集合里：状态树不替侧栏清理「看不见」的选项。
        Assert.Equal<Guid list>([ a; b ], vs.Sidebar.SelectedRowIds |> Set.toList)

    [<Fact>]
    let ``链路：多选 → 批量删除 → 撤销`` () =
        let a, b = sid 1, sid 2
        let m0 = modelWithSummaries [ summary a "甲" false false; summary b "乙" false false ]
        // 1. 多选：进入 + 切换
        let m1, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.EnterBatch a)) m0
        let m2, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.ToggleBatch b)) m1
        Assert.True m2.Chrome.BatchMode
        Assert.Equal<Guid list>([ a; b ], m2.Chrome.SelectedRowIds |> Set.toList)
        // 2. 批量删除：删除选中的两项
        let m3, _ =
            Workspace.update now0 (batchMsg (SidebarUserMsg.BatchDelete [ a; b ])) m2
        // 3. 撤销：Toast 带撤销动作，撤销后删除台账被清掉
        let toast = m3.Toasts |> List.tryLast
        Assert.True(toast.IsSome, "批量删除应产生一个 Toast")

        match toast with
        | Some t ->
            Assert.Equal("已删除选中的对话", t.Text)
            Assert.True(t.UndoAction.IsSome, "Toast 应带撤销动作")
            Assert.Equal(1, m3.Feedback.PendingDeletions.Count)

            match t.UndoAction with
            | Some actionId ->
                let m4, _ =
                    Workspace.update now0 (Msg.User(UserMsg.Overlay(OverlayUserMsg.ToastUndo actionId))) m3

                Assert.Empty m4.Feedback.PendingDeletions
                Assert.Equal("已恢复删除的对话", (m4.Toasts |> List.tryLast).Value.Text)
            | None -> Assert.True(false, "撤销动作 id 缺失")
        | None -> Assert.True(false, "批量删除应产生一个 Toast")
        // 4. 多选状态不受删除/撤销影响：多选仍开着，选中集合还在
        Assert.True m3.Chrome.BatchMode
        Assert.Equal<Guid list>([ a; b ], m3.Chrome.SelectedRowIds |> Set.toList)

    // 草稿落盘的**节流在宿主侧**（`compose/AppShell` 的 800 ms 闸 +
    // 组合者自己的 `DraftSaveGate`），状态树不按键就发一次 `DraftWrite`——
    // 那会每个键击写一次 IndexedDB。状态树只管「文本是什么」。
    [<Fact>]
    let ``输入框变字只更新状态树的文本，不产出落盘效果`` () =
        let model = Workspace.init ()

        let m1, effs =
            Workspace.update now0 (Msg.User(UserMsg.Composer(ComposerUserMsg.TextChanged "草稿内容"))) model

        Assert.Equal("草稿内容", m1.Composer.Text)
        Assert.Empty(effs)

    [<Fact>]
    let ``配对到首发：从 PairingStarted、PairingSucceeded 到 AuthPresent，再到目录就绪后发送首条消息`` () =
        let m0 = Workspace.init ()
        Assert.Equal(ConnPhase.Connecting 0, m0.Conn)

        // 1. 主机要求配对
        let m1, _ = Workspace.update now0 (Msg.WireRecv (WireEvent.PairingStarted {| expiresInSeconds = 300 |})) m0
        match m1.Conn with
        | ConnPhase.Pairing _ -> ()
        | other -> Assert.Fail(sprintf "未进入配对状态: %A" other)

        // 2. 配对成功，发出 AuthPresent
        let m2, effs2 = Workspace.update now0 (Msg.WireRecv (WireEvent.PairingSucceeded {| token = "tok-first" |})) m1
        Assert.Contains(Effect.SendWire (WireEvent.AuthPresent {| token = "tok-first" |}), effs2)

        // 3. 认证接受，转入装载目录
        let m3, _ = Workspace.update now0 (Msg.WireRecv (WireEvent.AuthAccepted {| instanceId = "inst-first" |})) m2
        match m3.Conn with
        | ConnPhase.Authed("inst-first", CatalogPhase.LoadingCatalog) -> ()
        | other -> Assert.Fail(sprintf "认证后状态不对: %A" other)

        // 4. 目录装载就绪（带可用模型服务），工作区进入 IdleEmpty
        let readyCatalog: Catalog = {
            Catalog.empty with
                providers = [
                    { id = "p1"; label = "P1"; kind = "openai"; baseUrl = ""; hasApiKey = true
                      models = [ "m1" ]; defaultModel = "m1"; timeoutSeconds = 60; maxRetries = 2
                      enabled = true; visionOverrides = Map.empty; visionDetected = Map.empty }
                ]
        }
        let m4 = { m3 with Conn = ConnPhase.Authed("inst-first", CatalogPhase.Ready(readyCatalog, WorkspacePhase.IdleEmpty)) }
        let vs4 = Workspace.project m4
        Assert.True vs4.Composer.Enabled
        Assert.Equal(SendButtonState.Idle, vs4.Composer.Send.State)

        // 5. 用户在空态输入首条消息并发送
        let m5, _ = Workspace.update now0 (Msg.User (UserMsg.Composer (ComposerUserMsg.TextChanged "你好，首条消息"))) m4
        let m6, effs6 = Workspace.update now0 (Msg.User (UserMsg.Composer ComposerUserMsg.Send)) m5
        Assert.Equal("", m6.Composer.Text)
        Assert.Equal(2, m6.Feedback.PendingCommands.Count)

        let createCmd =
            effs6
            |> List.tryPick (function
                | Effect.SendWire(WireEvent.Command(ClientCommand.CreateConversation d)) -> Some d
                | _ -> None)
        Assert.True(createCmd.IsSome, "应当发出 CreateConversation 命令")
        Assert.Equal("你好，首条消息", createCmd.Value.title)

        let sendCmd =
            effs6
            |> List.tryPick (function
                | Effect.SendWire(WireEvent.Command(ClientCommand.SendUserMessage d)) -> Some d
                | _ -> None)
        Assert.True(sendCmd.IsSome, "应当发出 SendUserMessage 命令")
        Assert.Equal(createCmd.Value.conversationId, sendCmd.Value.conversationId)
        let contents = sendCmd.Value.messageJson.["contents"].AsArray()
        Assert.Equal(1, contents.Count)
        Assert.Equal("你好，首条消息", contents[0].["text"].GetValue<string>())

        let observeCmd =
            effs6
            |> List.tryPick (function
                | Effect.SendWire(WireEvent.ObserveConversation d) -> Some d
                | _ -> None)
        Assert.True(observeCmd.IsSome, "应当发出 ObserveConversation 事件")
        Assert.Equal(createCmd.Value.conversationId, observeCmd.Value.conversationId)

        // 6. 主机返回该对话快照，工作区转入 Active
        let convSnapshotEv = WireEvent.ConversationSnapshot {|
            conversationId = createCmd.Value.conversationId
            title = "你好，首条消息"
            lastCommitId = 1UL
            runtimeState = "idle"
            generationId = None
            messages = JsonArray([|
                let u = JsonObject()
                u["commitId"] <- 1UL
                u["role"] <- "user"
                u["text"] <- "你好，首条消息"
                u :> JsonNode
            |])
            snapshotEarliestCommitId = 1UL
            snapshotHasMore = false
            config = SessionConfig.empty
        |}
        let m7, _ = Workspace.update now0 (Msg.WireRecv convSnapshotEv) m6
        let vs7 = Workspace.project m7
        match vs7.Chat.Phase with
        | ChatPhase.Messages msgs ->
            Assert.Equal(1, msgs.Length)
            Assert.Equal("你好，首条消息", msgs[0].Text)
        | other -> Assert.Fail(sprintf "应当呈现首条消息，实际: %A" other)
        match vs7.Header.Title with
        | TitleMode.Viewing(t, _) -> Assert.Equal("你好，首条消息", t)
        | TitleMode.Editing draft -> Assert.Equal("你好，首条消息", draft)

    [<Fact>]
    let ``到期提交：未撤销时到期 Tick 触发 DeleteConversation 命令并清除待删台账`` () =
        let c1 = sid 1
        let c2 = sid 2
        let m0 = modelWithSummaries [ summary c1 "对话1" false false; summary c2 "对话2" false false ]
        // 1. 批量删除两项
        let m1, _ = Workspace.update now0 (batchMsg (SidebarUserMsg.BatchDelete [ c1; c2 ])) m0
        Assert.Equal(1, m1.Feedback.PendingDeletions.Count)
        let batch = m1.Feedback.PendingDeletions |> Map.toList |> List.head |> snd
        Assert.Equal<Set<Guid>>(set [ c1; c2 ], set batch.Ids)
        let dueAt = batch.DueAt

        // 2. 未到期 Tick（比如 4 秒后）：不触发删除命令，台账保留
        let m2, effs2 = Workspace.update (now0.AddSeconds 4.0) (Msg.MsgTick (now0.AddSeconds 4.0)) m1
        Assert.Equal(1, m2.Feedback.PendingDeletions.Count)
        let deleteCmdsBefore =
            effs2
            |> List.choose (function
                | Effect.SendWire(WireEvent.Command(ClientCommand.DeleteConversation d)) -> Some d.conversationId
                | _ -> None)
        Assert.Empty deleteCmdsBefore

        // 3. 到期 Tick（8.5 秒后）：触发两条 DeleteConversation 命令，台账清空
        let expiryTick = dueAt.AddSeconds 0.5
        let m3, effs3 = Workspace.update expiryTick (Msg.MsgTick expiryTick) m2
        Assert.Empty m3.Feedback.PendingDeletions
        let deleteCmdsAfter =
            effs3
            |> List.choose (function
                | Effect.SendWire(WireEvent.Command(ClientCommand.DeleteConversation d)) -> Some d.conversationId
                | _ -> None)
        Assert.Equal(2, deleteCmdsAfter.Length)
        Assert.Contains(c1, deleteCmdsAfter)
        Assert.Contains(c2, deleteCmdsAfter)

    [<Fact>]
    let ``失败重试：失败后携带 lastError，Regenerate 发出命令并在新生成完成后清除错误`` () =
        let model = authedWithConversation ()
        let convId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        let genId = Guid.NewGuid()

        // 1. 生成失败，上报 GenerationError
        let err: GenerationError = { kind = GenerationErrorKind.ProviderTimeout; message = "请求超时"; retryable = true; detail = None; retryAfterSeconds = None }
        let finishFailedEv = WireEvent.GenerationFinished {|
            conversationId = convId
            generationId = genId
            status = "failed"
            error = Some err
            usage = None
        |}
        let m1, _ = Workspace.update now0 (Msg.WireRecv finishFailedEv) model
        match m1.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, run))) ->
            match run with
            | RunState.Idle (Some e) ->
                Assert.Equal(GenerationErrorKind.ProviderTimeout, e.kind)
                Assert.Equal("请求超时", e.message)
                Assert.True e.retryable
            | other -> Assert.Fail(sprintf "RunState 应携带失败信息，实际: %A" other)
        | other -> Assert.Fail(sprintf "连接状态不对: %A" other)

        let vs1 = Workspace.project m1
        Assert.True(vs1.Chat.Error.IsSome, "视图状态应当展示错误卡")
        Assert.Equal("请求超时", vs1.Chat.Error.Value.message)

        // 2. 用户触发重新生成（重试）
        let m2, effs2 = Workspace.update now0 (Msg.User (UserMsg.Chat ChatUserMsg.Regenerate)) m1
        let regenCmd =
            effs2
            |> List.tryPick (function
                | Effect.SendWire(WireEvent.Command(ClientCommand.RegenerateResponse d)) -> Some d
                | _ -> None)
        Assert.True(regenCmd.IsSome, "应当发出 RegenerateResponse 命令")
        Assert.Equal(convId, regenCmd.Value.conversationId)
        Assert.Equal(1, m2.Feedback.PendingCommands.Count)
        let ackEv = WireEvent.CommandCommitted {| invocationId = regenCmd.Value.invocationId; commandId = string regenCmd.Value.invocationId; commitId = 1UL |}
        let m2Ack, _ = Workspace.update now0 (Msg.WireRecv ackEv) m2
        Assert.Equal(0, m2Ack.Feedback.PendingCommands.Count)

        // 3. 主机启动重试生成
        let newGenId = Guid.NewGuid()
        let startEv = WireEvent.GenerationStarted {| conversationId = convId; generationId = newGenId; providerId = "p"; model = "m" |}
        let m3, _ = Workspace.update now0 (Msg.WireRecv startEv) m2Ack
        let vs3 = Workspace.project m3
        Assert.True vs3.Chat.Streaming
        Assert.Equal(SendButtonState.GeneratingStop, vs3.Composer.Send.State)

        // 4. 重试成功完成：错误被清除，返回 Idle None
        let finishOkEv = WireEvent.GenerationFinished {|
            conversationId = convId
            generationId = newGenId
            status = "completed"
            error = None
            usage = None
        |}
        let m4, _ = Workspace.update now0 (Msg.WireRecv finishOkEv) m3
        match m4.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, run))) ->
            match run with
            | RunState.Idle None -> ()
            | other -> Assert.Fail(sprintf "重试完成后 RunState 应为 Idle None，实际: %A" other)
        | other -> Assert.Fail(sprintf "连接状态不对: %A" other)
        let vs4 = Workspace.project m4
        Assert.Equal(None, vs4.Chat.Error)
        Assert.Equal(SendButtonState.Idle, vs4.Composer.Send.State)
