namespace Wanxiang2.WorkspaceState

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

module Workspace =

    let project = Project.project

    let init () : WorkspaceModel =
        let limits: UiPrefLimits = {
            sidebarWidth = 260.0
            sidebarMinWidth = 200.0
            sidebarMaxWidth = 400.0
            windowMinWidth = 700.0
        }
        let initialPrefs = UiPrefs.defaults limits
        {
            Conn = ConnPhase.Connecting 0
            Nav = {
                CompactMode = false
                DrawerOpen = false
                SidebarCollapsed = false
                SidebarWidth = 260.0
            }
            Overlay = OverlayPhase.Closed
            Feedback = {
                PendingCommands = Map.empty
                PendingDeletions = Map.empty
                PendingConfigs = Map.empty
            }
            Prefs = initialPrefs
            Chrome = {
                Compact = false
                SidebarWidth = 260.0
                StartingPageVisible = true
                HeaderEditingDraft = None
                SearchQuery = ""
                SearchActive = false
                SearchDebouncingUntil = None
                SelectedRowIds = Set.empty
                BatchMode = false
                RevealTarget = None
                RevealTargetExpiresAt = None
            }
            Composer = {
                Text = ""
                Caret = None
                EnterSends = true
                SelectedModel = None
                Attachments = []
            }
            Summaries = []
            Toasts = []
            NextToastId = 1
            NextTimerToken = 1
            PendingApprovals = Map.empty
            PaletteRecent = []
        }

    let private addToast (text: string) (tone: ToastTone) (undo: string option) (now: DateTimeOffset) (model: WorkspaceModel) : WorkspaceModel * Effect list =
        let id = model.NextToastId
        let item: ToastItem = {
            Id = id
            Text = text
            Tone = tone
            UndoAction = undo
            ExpiresAt = now.AddSeconds 6.0
        }
        let nextModel = { model with Toasts = model.Toasts @ [ item ]; NextToastId = id + 1 }
        nextModel, [ Effect.ShowToast(text, tone) ]

    let rec update (now: DateTimeOffset) (msg: Msg) (model: WorkspaceModel) : WorkspaceModel * Effect list =
        match msg with
        | MsgTick tickNow ->
            // 2. 检查搜索防抖
            let modelAfterSearch, searchEffects =
                match model.Chrome.SearchDebouncingUntil with
                | Some due when tickNow >= due ->
                    let q = model.Chrome.SearchQuery
                    let nextChrome = { model.Chrome with SearchDebouncingUntil = None }
                    let queryData: SearchQueryData = {
                        queryId = Guid.NewGuid()
                        query = q
                        includeArchived = false
                        offset = 0
                        limit = 50
                    }
                    { model with Chrome = nextChrome }, [ Effect.SendWire(WireEvent.SearchQuery queryData) ]
                | _ -> model, []
            // 3. 检查 PendingDeletions 到期提交
            let dueBatches =
                modelAfterSearch.Feedback.PendingDeletions
                |> Map.toList
                |> List.filter (fun (_, b) -> tickNow >= b.DueAt)
            let remainingDeletions =
                dueBatches
                |> List.fold (fun acc (token, _) -> Map.remove token acc) modelAfterSearch.Feedback.PendingDeletions
            let deleteEffects =
                dueBatches
                |> List.collect (fun (_, b) ->
                    b.Ids
                    |> List.map (fun id ->
                        let invId = Guid.NewGuid()
                        let cmd = ClientCommand.DeleteConversation {| invocationId = invId; conversationId = id |}
                        Effect.SendWire(WireEvent.Command cmd)
                    )
                )
            let modelAfterDeletions = {
                modelAfterSearch with
                    Feedback = { modelAfterSearch.Feedback with PendingDeletions = remainingDeletions }
                    Toasts = model.Toasts |> List.filter (fun t -> t.ExpiresAt > tickNow)
            }
            // 4. 定位高亮过期
            let finalModel =
                match modelAfterDeletions.Chrome.RevealTargetExpiresAt with
                | Some exp when tickNow >= exp ->
                    { modelAfterDeletions with Chrome = { modelAfterDeletions.Chrome with RevealTarget = None; RevealTargetExpiresAt = None } }
                | _ -> modelAfterDeletions
            finalModel, searchEffects @ deleteEffects

        | WireRecv wireEvent ->
            match wireEvent with
            | WireEvent.Hello _ ->
                model, []
            | WireEvent.UpgradeRequired _ ->
                addToast "主机需要更高版本的协议，请升级客户端。" ToastTone.Warning None now model
            | WireEvent.AuthAccepted d ->
                let nextConn = ConnPhase.Authed(d.instanceId, CatalogPhase.LoadingCatalog)
                let effects = [ Effect.SendWire WireEvent.CatalogRequest; Effect.SendWire WireEvent.ObserveConversationList ]
                { model with Conn = nextConn }, effects
            | WireEvent.PairingStarted d ->
                let pairing = { Code = None; ExpiresAt = Some (now.AddSeconds(float d.expiresInSeconds)); FrozenMinutes = None; Error = None }
                { model with Conn = ConnPhase.Pairing pairing }, []
            | WireEvent.AuthRejected _ ->
                let nextConn = ConnPhase.Connecting 0
                { model with Conn = nextConn }, [ Effect.ShowToast("认证失败，请重新配对。", ToastTone.Warning) ]
            | WireEvent.PairingSucceeded d ->
                let effects = [ Effect.SendWire(WireEvent.AuthPresent {| token = d.token |}) ]
                model, effects
            | WireEvent.PairingFailed d ->
                let pairing = { Code = None; ExpiresAt = None; FrozenMinutes = None; Error = Some d.reason }
                { model with Conn = ConnPhase.Pairing pairing }, [ Effect.ShowToast(sprintf "配对失败：%s" d.reason, ToastTone.Warning) ]
            | WireEvent.CatalogSnapshot d ->
                let catalog = Catalog.parse d.providers d.tools d.generation
                match model.Conn with
                | ConnPhase.Authed(instId, _) ->
                    let nextWs =
                        match model.Conn with
                        | ConnPhase.Authed(_, CatalogPhase.Ready(_, ws)) -> ws
                        | _ -> WorkspacePhase.IdleEmpty
                    let nextCatalog = CatalogPhase.Ready(catalog, nextWs)
                    { model with Conn = ConnPhase.Authed(instId, nextCatalog) }, []
                | _ -> model, []
            | WireEvent.ConversationListSnapshot d ->
                let summaries =
                    d.items
                    |> Seq.choose (fun node ->
                        match node with
                        | :? JsonObject as o ->
                            let idStr = o.["conversationId"] |> string
                            match Guid.TryParse idStr with
                            | true, g ->
                                let title = if isNull o.["title"] then "对话" else o.["title"].GetValue<string>()
                                let preview = if isNull o.["preview"] then "" else o.["preview"].GetValue<string>()
                                Some {
                                    Id = g
                                    Title = title
                                    Preview = preview
                                    Running = false
                                    Pinned = false
                                    Archived = false
                                    UpdatedAt = now
                                    MessageCount = 0
                                    ProviderId = ""
                                    Model = ""
                                    LastCommitId = d.lastCommitId
                                }
                            | _ -> None
                        | _ -> None
                    )
                    |> Seq.toList
                { model with Summaries = summaries }, []
            | WireEvent.ConversationSnapshot d ->
                let msgs =
                    d.messages
                    |> Seq.map MessageView.ofSnapshotItem
                    |> Seq.toList
                let activeConv: ActiveConvState = {
                    ConversationId = d.conversationId
                    Title = d.title
                    Messages = msgs
                    LastCommitId = d.lastCommitId
                    RuntimeState = d.runtimeState
                    Config = d.config
                    PageEarliest = d.snapshotEarliestCommitId
                    PageHasMore = d.snapshotHasMore
                    PageLoading = false
                    ParentId = None
                }
                match model.Conn with
                | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, _)) ->
                    let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, RunState.Idle None))
                    { model with Conn = ConnPhase.Authed(instId, nextCatalog) }, [ Effect.ScrollChat ScrollCommand.End ]
                | _ -> model, []
            | WireEvent.GenerationStarted d ->
                match model.Conn with
                | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, _))) when activeConv.ConversationId = d.conversationId ->
                    let run = RunState.Generating(d.generationId, now, None)
                    let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, run))
                    { model with Conn = ConnPhase.Authed(instId, nextCatalog) }, [ Effect.ScrollChat ScrollCommand.End ]
                | _ -> model, []
            | WireEvent.GenerationDelta d ->
                match model.Conn with
                | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, RunState.Generating(genId, startedAt, _))))
                    when activeConv.ConversationId = d.conversationId && genId = d.generationId ->
                    let partialMsg = MessageView.ofJson d.payload
                    let run = RunState.Generating(genId, startedAt, Some partialMsg)
                    let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, run))
                    { model with Conn = ConnPhase.Authed(instId, nextCatalog) }, [ Effect.ScrollChat ScrollCommand.End ]
                | _ -> model, []
            | WireEvent.GenerationFinished d ->
                match model.Conn with
                | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, _))) when activeConv.ConversationId = d.conversationId ->
                    let run = RunState.Idle d.error
                    let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, run))
                    { model with Conn = ConnPhase.Authed(instId, nextCatalog) }, [ Effect.ScrollChat ScrollCommand.End; Effect.Focus FocusTarget.ComposerInput ]
                | _ -> model, []
            | WireEvent.CommandAccepted _ ->
                model, []
            | WireEvent.CommandCommitted d ->
                let remainingCommands = Map.remove d.invocationId model.Feedback.PendingCommands
                let nextFeedback = { model.Feedback with PendingCommands = remainingCommands }
                { model with Feedback = nextFeedback }, []
            | WireEvent.CommandRejected d ->
                let remainingCommands = Map.remove d.invocationId model.Feedback.PendingCommands
                let nextFeedback = { model.Feedback with PendingCommands = remainingCommands }
                let modelAfter, effs = addToast (sprintf "命令未执行：%s" d.message) ToastTone.Warning None now { model with Feedback = nextFeedback }
                modelAfter, effs
            | WireEvent.ToolApprovalRequested d ->
                let item = {|
                    ToolName = d.toolName
                    CallId = d.callId
                    Summary = d.summary
                    ArgumentsJson = (if isNull d.arguments then "" else d.arguments.ToJsonString())
                    Policy = d.policy
                    ConversationId = d.conversationId
                |}
                let nextApprovals = Map.add d.approvalId item model.PendingApprovals
                let activeConvId =
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) -> Some conv.ConversationId
                    | _ -> None
                let modelWithApproval = { model with PendingApprovals = nextApprovals }
                if activeConvId <> Some d.conversationId then
                    addToast "另一个对话里有工具调用在等你批准。" ToastTone.Info None now modelWithApproval
                else
                    modelWithApproval, []
            | WireEvent.ToolApprovalResolved d ->
                let nextApprovals = Map.remove d.approvalId model.PendingApprovals
                let modelWithout = { model with PendingApprovals = nextApprovals }
                if d.alreadyHandled then
                    addToast (sprintf "这个审批已经处理过了（%s）。" d.status) ToastTone.Info None now modelWithout
                else
                    modelWithout, []
            | _ -> model, []

        | ConnectionUp instanceId ->
            let effects = [ Effect.SendWire WireEvent.CatalogRequest; Effect.SendWire WireEvent.ObserveConversationList ]
            { model with Conn = ConnPhase.Authed(instanceId, CatalogPhase.LoadingCatalog) }, effects

        | ConnectionDown _ ->
            // 不变式 3：ConnectionDown 之后 pending 计数归零（防 Pending Wedge）
            let resetFeedback = {
                PendingCommands = Map.empty
                PendingDeletions = Map.empty
                PendingConfigs = Map.empty
            }
            // 不变式 2：ConnPhase <> Authed 时不存在 Active 对话数据（切走即整体丢弃）
            let nextConn = ConnPhase.Connecting 1
            { model with Conn = nextConn; Feedback = resetFeedback; PendingApprovals = Map.empty }, [ Effect.ShowToast("已断开连接", ToastTone.Warning) ]

        | TimerFired _ ->
            model, []

        | EveryFired _ ->
            model, []

        | PrefsLoaded prefs ->
            { model with Prefs = prefs }, [ Effect.ApplyTheme (match prefs.theme with FollowSystem -> ThemeKind.System | AlwaysLight -> ThemeKind.Light | AlwaysDark -> ThemeKind.Dark) ]

        | User userMsg ->
            match userMsg with
            | UserMsg.Sidebar sidebarMsg ->
                match sidebarMsg with
                | SidebarUserMsg.SelectConv convId ->
                    match model.Conn with
                    | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, _)) ->
                        let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.LoadingConversation convId)
                        let effects = [ Effect.SendWire(WireEvent.ObserveConversation {| conversationId = convId |}) ]
                        { model with Conn = ConnPhase.Authed(instId, nextCatalog) }, effects
                    | _ -> model, []
                | SidebarUserMsg.NewConversation ->
                    match model.Conn with
                    | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, _)) ->
                        let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.IdleEmpty)
                        { model with Conn = ConnPhase.Authed(instId, nextCatalog) }, [ Effect.Focus FocusTarget.ComposerInput ]
                    | _ -> model, []
                | SidebarUserMsg.DeleteConversation convId ->
                    let token = Guid.NewGuid()
                    let batch: PendingBatchDeletion = {
                        Token = token
                        Ids = [ convId ]
                        Label = "已删除对话"
                        DueAt = now.AddSeconds 8.0
                    }
                    let nextDeletions = Map.add token batch model.Feedback.PendingDeletions
                    let modelAfterDeletions = { model with Feedback = { model.Feedback with PendingDeletions = nextDeletions } }
                    addToast "已删除对话" ToastTone.Success (Some (token.ToString())) now modelAfterDeletions
                | SidebarUserMsg.BatchDelete convIds ->
                    let token = Guid.NewGuid()
                    let batch: PendingBatchDeletion = {
                        Token = token
                        Ids = convIds
                        Label = "已删除选中的对话"
                        DueAt = now.AddSeconds 8.0
                    }
                    let nextDeletions = Map.add token batch model.Feedback.PendingDeletions
                    let modelAfterDeletions = { model with Feedback = { model.Feedback with PendingDeletions = nextDeletions } }
                    addToast "已删除选中的对话" ToastTone.Success (Some (token.ToString())) now modelAfterDeletions
                | SidebarUserMsg.SearchTextChanged q ->
                    let nextChrome = { model.Chrome with SearchQuery = q; SearchDebouncingUntil = Some(now.AddMilliseconds 300.0) }
                    { model with Chrome = nextChrome }, []
                | SidebarUserMsg.SearchActivate ->
                    { model with Chrome = { model.Chrome with SearchActive = true } }, [ Effect.Focus FocusTarget.SidebarSearch ]
                | SidebarUserMsg.SearchClear ->
                    { model with Chrome = { model.Chrome with SearchQuery = ""; SearchActive = false; SearchDebouncingUntil = None } }, []
                | _ -> model, []

            | UserMsg.Composer compMsg ->
                match compMsg with
                | ComposerUserMsg.TextChanged text ->
                    { model with Composer = { model.Composer with Text = text } }, []
                | ComposerUserMsg.Send ->
                    if String.IsNullOrWhiteSpace model.Composer.Text && List.isEmpty model.Composer.Attachments then
                        model, []
                    else
                        match model.Conn with
                        | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, currentWorkspace)) ->
                            let text = model.Composer.Text
                            let resetComposer = { model.Composer with Text = ""; Attachments = [] }
                            match currentWorkspace with
                            | WorkspacePhase.Active(activeConv, _) ->
                                let invId = Guid.NewGuid()
                                let msgJson = JsonObject()
                                msgJson["text"] <- text
                                let cmd = ClientCommand.SendUserMessage {|
                                    invocationId = invId
                                    conversationId = activeConv.ConversationId
                                    messageJson = msgJson :> JsonNode
                                |}
                                let pendingCmd = { InvocationId = invId; Command = cmd; SentAt = now; SuccessMessage = None }
                                let nextFeedback = { model.Feedback with PendingCommands = Map.add invId pendingCmd model.Feedback.PendingCommands }
                                let nextModel = { model with Composer = resetComposer; Feedback = nextFeedback }
                                nextModel, [ Effect.SendWire(WireEvent.Command cmd); Effect.ScrollChat ScrollCommand.End ]
                            | WorkspacePhase.IdleEmpty ->
                                let newConvId = Guid.NewGuid()
                                let invId = Guid.NewGuid()
                                let createCmd = ClientCommand.CreateConversation {|
                                    invocationId = invId
                                    conversationId = newConvId
                                    title = if text.Length > 20 then text.Substring(0, 20) else text
                                    config = SessionConfig.empty
                                |}
                                let appendInvId = Guid.NewGuid()
                                let msgJson = JsonObject()
                                msgJson["text"] <- text
                                let appendCmd = ClientCommand.SendUserMessage {|
                                    invocationId = appendInvId
                                    conversationId = newConvId
                                    messageJson = msgJson :> JsonNode
                                |}
                                let p1 = { InvocationId = invId; Command = createCmd; SentAt = now; SuccessMessage = None }
                                let p2 = { InvocationId = appendInvId; Command = appendCmd; SentAt = now; SuccessMessage = None }
                                let nextFeedback = { model.Feedback with PendingCommands = model.Feedback.PendingCommands |> Map.add invId p1 |> Map.add appendInvId p2 }
                                let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.LoadingConversation newConvId)
                                let nextModel = { model with Conn = ConnPhase.Authed(instId, nextCatalog); Composer = resetComposer; Feedback = nextFeedback }
                                nextModel, [
                                    Effect.SendWire(WireEvent.Command createCmd)
                                    Effect.SendWire(WireEvent.Command appendCmd)
                                    Effect.SendWire(WireEvent.ObserveConversation {| conversationId = newConvId |})
                                ]
                            | _ -> model, []
                        | _ -> model, []
                | ComposerUserMsg.StopGeneration ->
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(activeConv, RunState.Generating(genId, _, _)))) ->
                        let effects = [ Effect.SendWire(WireEvent.GenerationCancel {| conversationId = activeConv.ConversationId; generationId = genId |}) ]
                        model, effects
                    | _ -> model, []
                | _ -> model, []

            | UserMsg.Overlay overlayMsg ->
                match overlayMsg with
                | OverlayUserMsg.Escape ->
                    // 浮层 Escape 链：Popup -> Dialog -> Drawer -> Closed，焦点归还发 Effect.Focus
                    match model.Overlay with
                    | OverlayPhase.PopupOpen _ ->
                        { model with Overlay = OverlayPhase.Closed }, [ Effect.Focus FocusTarget.ComposerInput ]
                    | OverlayPhase.DialogOpen _ ->
                        { model with Overlay = OverlayPhase.Closed }, [ Effect.Focus FocusTarget.ComposerInput ]
                    | OverlayPhase.DrawerOpen ->
                        { model with Overlay = OverlayPhase.Closed; Nav = { model.Nav with DrawerOpen = false } }, [ Effect.Focus FocusTarget.ComposerInput ]
                    | OverlayPhase.Closed ->
                        model, []
                | OverlayUserMsg.Dismiss ->
                    { model with Overlay = OverlayPhase.Closed }, [ Effect.Focus FocusTarget.ComposerInput ]
                | OverlayUserMsg.ToastUndo actionId ->
                    match Guid.TryParse actionId with
                    | true, token ->
                        let restoredDeletions = Map.remove token model.Feedback.PendingDeletions
                        let modelRestored = { model with Feedback = { model.Feedback with PendingDeletions = restoredDeletions } }
                        addToast "已恢复删除的对话" ToastTone.Success None now modelRestored
                    | _ -> model, []
                | OverlayUserMsg.PopupOpened popupId ->
                    { model with Overlay = OverlayPhase.PopupOpen (PopupKind.ToastAnchor popupId) }, []
                | OverlayUserMsg.PopupClosed _ ->
                    { model with Overlay = OverlayPhase.Closed }, [ Effect.Focus FocusTarget.ComposerInput ]
                | _ -> model, []

            | UserMsg.Header headerMsg ->
                match headerMsg with
                | HeaderUserMsg.ToggleSidebar ->
                    let nextNav = { model.Nav with SidebarCollapsed = not model.Nav.SidebarCollapsed }
                    { model with Nav = nextNav }, []
                | _ -> model, []

            | UserMsg.Palette paletteMsg ->
                match paletteMsg with
                | PaletteUserMsg.PushRecent itemId ->
                    let updated = itemId :: (model.PaletteRecent |> List.filter ((<>) itemId)) |> List.truncate 8
                    { model with PaletteRecent = updated }, []
                | PaletteUserMsg.SetRecent recent ->
                    { model with PaletteRecent = List.truncate 8 recent }, []
                | _ -> model, []

            | _ -> model, []

        | Internal internalMsg ->
            match internalMsg with
            | InternalMsg.RevealTargetExpired ->
                { model with Chrome = { model.Chrome with RevealTarget = None; RevealTargetExpiresAt = None } }, []
            | _ -> model, []
