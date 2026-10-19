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
        let cmd = ClientCommand.CreateConversation {| invocationId = invId; conversationId = Guid.NewGuid(); title = "T"; config = SessionConfig.empty |}
        let pending = { InvocationId = invId; Command = cmd; SentAt = now0; SuccessMessage = None }
        let modelWithPending = { model with Feedback = { model.Feedback with PendingCommands = Map.add invId pending Map.empty } }
        Assert.Equal(1, modelWithPending.Feedback.PendingCommands.Count)

        let nextModel, _ = Workspace.update now0 (Msg.ConnectionDown (Some (ConnectionIssue.Disconnected "net down"))) modelWithPending
        Assert.Equal(0, nextModel.Feedback.PendingCommands.Count)
        Assert.Equal(0, nextModel.Feedback.PendingDeletions.Count)
        Assert.Equal(0, nextModel.Feedback.PendingConfigs.Count)

    [<Fact>]
    let ``Invariant 4: Determinism: same (model, msg) twice produces equal results`` () =
        let model = Workspace.init ()
        let msg = Msg.User (UserMsg.Composer (ComposerUserMsg.TextChanged "hello"))
        let (m1, e1) = Workspace.update now0 msg model
        let (m2, e2) = Workspace.update now0 msg model
        Assert.Equal(m1, m2)
        Assert.Equal<Effect list>(e1, e2)

    [<Fact>]
    let ``Full Journey Smoke Test: Pairing -> Onboarding -> Catalog Ready -> Send -> Generation Delta -> Finish -> Disconnect -> Reconnect`` () =
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
        let emptyCatalog: Catalog = { providers = []; tools = []; mcpServers = []; presets = []; generation = Catalog.empty.generation }
        let m2 = { m1 with Conn = ConnPhase.Authed("inst-1", CatalogPhase.NeedsOnboarding OnboardingPhase.Idle) }
        let vs2 = Workspace.project m2
        Assert.Equal(ChatPhase.Empty IdleEmptyKind.NoProvider, vs2.Chat.Phase)
        Assert.False vs2.Composer.Enabled

        // 3. Provider added, Catalog Ready
        let readyCatalog: Catalog = {
            emptyCatalog with
                providers = [
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
        let m3 = { m2 with Conn = ConnPhase.Authed("inst-1", CatalogPhase.Ready(readyCatalog, WorkspacePhase.IdleEmpty)) }
        let vs3 = Workspace.project m3
        Assert.Equal(ChatPhase.Empty IdleEmptyKind.NoConversation, vs3.Chat.Phase)
        Assert.True vs3.Composer.Enabled
        Assert.Equal(SendButtonState.Idle, vs3.Composer.Send.State)

        // 4. Composer text & Send
        let m4, _ = Workspace.update now0 (Msg.User (UserMsg.Composer (ComposerUserMsg.TextChanged "你好"))) m3
        let m5, effs5 = Workspace.update now0 (Msg.User (UserMsg.Composer ComposerUserMsg.Send)) m4
        Assert.True(effs5.Length >= 2)
        Assert.Equal("", m5.Composer.Text)

        // CommandCommitted for the pending commands
        let pendingInvs = m5.Feedback.PendingCommands |> Map.toList |> List.map fst
        let m5Committed =
            pendingInvs
            |> List.fold (fun acc invId ->
                let committedEv = WireEvent.CommandCommitted {| invocationId = invId; commandId = "c1"; commitId = 1UL |}
                let next, _ = Workspace.update now0 (Msg.WireRecv committedEv) acc
                next
            ) m5
        Assert.Equal(0, m5Committed.Feedback.PendingCommands.Count)

        // 5. Conversation Snapshot arrives
        let convId = Guid.NewGuid()
        let convSnapshotEv = WireEvent.ConversationSnapshot {|
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
        let genStartedEv = WireEvent.GenerationStarted {| conversationId = convId; generationId = genId; providerId = "p1"; model = "model-a" |}
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
        let deltaEv = WireEvent.GenerationDelta {
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
        let finishEv = WireEvent.GenerationFinished {|
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
        let mOverlay, _ = Workspace.update now0 (Msg.User (UserMsg.Overlay (OverlayUserMsg.PopupOpened "popup-1"))) m9
        let mEsc, effsEsc = Workspace.update now0 (Msg.User (UserMsg.Overlay OverlayUserMsg.Escape)) mOverlay
        Assert.Equal(OverlayPhase.Closed, mEsc.Overlay)
        Assert.Contains(Effect.Focus FocusTarget.ComposerInput, effsEsc)

        // 10. Connection Down & Reconnect
        let m10, _ = Workspace.update now0 (Msg.ConnectionDown (Some (ConnectionIssue.Disconnected "net lost"))) m9
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
    let ``Pure modules in WorkspaceState: ReconnectBackoff, MessageLink, DraftSummaryText, ForkAnchor, RevealTarget, OlderPageLoad`` () =
        // 1. ReconnectBackoff
        Assert.Equal(2000, ReconnectBackoff.next 1000)
        Assert.Equal(30000, ReconnectBackoff.next 20000)
        Assert.Equal<int list>([ 1000; 2000; 4000 ], ReconnectBackoff.schedule 3)

        // 2. MessageLink
        let convId = Guid.NewGuid()
        let link = MessageLink.format None convId 42UL
        Assert.Equal(Some (convId, 42UL), MessageLink.tryParse link)

        // 3. DraftSummaryText
        Assert.Equal("已关闭：不会在本机保存草稿。", DraftSummaryText.text false 2 1)
        Assert.Equal("当前没有保存的草稿。", DraftSummaryText.text true 0 0)
        Assert.Equal("已保存 2 份草稿、1 条未确认的消息。", DraftSummaryText.text true 2 1)

        // 4. ForkAnchor
        Assert.Equal(Some 2UL, ForkAnchor.forkAfter [ 1UL; 2UL; 3UL ] (Some 3UL))

        // 5. RevealTarget
        Assert.Equal(Some 2UL, RevealTarget.resolve (RevealTarget.RevealMessage 2UL) [ 1UL; 2UL; 3UL ])

        // 6. OlderPageLoad
        Assert.True(OlderPageLoad.timedOut true (Some DateTimeOffset.UtcNow) (DateTimeOffset.UtcNow.AddSeconds 10.0) (TimeSpan.FromSeconds 5.0))
