namespace Wanxiang2.WorkspaceState

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

module Project =

    let project (model: WorkspaceModel) : WorkspaceViewState =
        // 起步页是「没有消息、也没在生成」。Chrome.StartingPageVisible 只在 init 里写成 true，不能当事实。
        let starting =
            match model.Conn with
            | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, RunState.Generating _))) -> false
            | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) -> List.isEmpty conv.Messages
            | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.LoadingConversation _)) -> false
            | _ -> true

        let shellVS: ShellVS =
            {
                Compact = model.Nav.CompactMode
                SidebarOpen = not model.Nav.SidebarCollapsed || model.Nav.DrawerOpen
                SidebarWidth = model.Nav.SidebarWidth
                StartingPageVisible = starting
            }

        let isAuthed, currentCatalog, currentWorkspace =
            match model.Conn with
            | ConnPhase.Authed(_, CatalogPhase.Ready(cat, ws)) -> true, Some cat, Some ws
            | _ -> false, None, None

        let headerVS: ChatHeaderVS =
            match currentWorkspace with
            | Some(WorkspacePhase.Active(conv, run)) ->
                let titleMode =
                    match model.Chrome.HeaderEditingDraft with
                    | Some draft -> TitleMode.Editing draft
                    | None -> TitleMode.Viewing(conv.Title, true)

                let genStatus =
                    match run with
                    | RunState.Generating _ -> Some "生成中…"
                    | RunState.Idle(Some _) -> Some "生成遇到问题"
                    | _ -> None

                let crumb =
                    conv.ParentId
                    |> Option.map (fun pid ->
                        {
                            ParentId = pid
                            ParentTitle = "上级对话"
                            Clickable = true
                        })

                {
                    Visible = true
                    Compact = model.Nav.CompactMode
                    InsetLeft = 0.0
                    Title = titleMode
                    GeneratingStatus = genStatus
                    Crumb = crumb
                    ShowBack = model.Nav.CompactMode
                    ShowMore = true
                }
            | _ ->
                {
                    Visible = true
                    Compact = model.Nav.CompactMode
                    InsetLeft = 0.0
                    Title = TitleMode.Viewing("新对话", false)
                    GeneratingStatus = None
                    Crumb = None
                    ShowBack = false
                    ShowMore = false
                }

        let rowsVS =
            model.Summaries
            |> List.map (fun s ->
                let isBusy =
                    model.Feedback.PendingDeletions
                    |> Map.exists (fun _ b -> List.contains s.id b.Ids)

                {
                    Id = s.id
                    Title = s.title
                    Preview = s.preview
                    Pinned = s.pinned
                    Archived = s.archived
                    Running = s.running
                    MessageCount = s.messageCount
                    CreatedAt = s.createdAt
                    UpdatedAt = s.updatedAt
                    IsFork = s.isFork
                    ProviderId = s.providerId
                    Model = s.model
                    LastCommitId = s.lastCommitId
                    Busy = isBusy
                })

        let emptyKind =
            if not isAuthed then
                SidebarEmptyKind.NotConnected model.Nav.CompactMode
            elif List.isEmpty model.Summaries then
                if not (String.IsNullOrWhiteSpace model.Chrome.SearchQuery) then
                    SidebarEmptyKind.NoSearchResults model.Chrome.SearchQuery
                else
                    SidebarEmptyKind.NoConversations
            else
                SidebarEmptyKind.NoConversations

        let searchBoxVS: SearchBoxVS =
            {
                Text = model.Chrome.SearchQuery
                EmptyHintVisible = false
                Active = model.Chrome.SearchActive
                Debouncing = model.Chrome.SearchDebouncingUntil.IsSome
            }

        // 多选的可见性相关的派生（可见计数、「可见行全选」、「是否全部置顶」）**不在这里**：
        // 「可见行」是侧栏的事实（搜索框与归档开关都在侧栏），状态树看不到。
        // 这里只投影模型知道的事实：是否多选、选中了哪些 id。

        let busyRows =
            model.Feedback.PendingDeletions
            |> Map.toList
            |> List.collect (fun (_, b) -> b.Ids)
            |> Set.ofList

        let sidebarVS: SidebarVS =
            {
                Rows = rowsVS
                Empty = emptyKind
                Search = searchBoxVS
                Brand =
                    {
                        PresetsVisible = currentCatalog |> Option.exists (fun c -> not (List.isEmpty c.presets))
                        Compact = model.Nav.CompactMode
                    }
                BatchMode = model.Chrome.BatchMode
                SelectedRowIds = model.Chrome.SelectedRowIds
                Host =
                    {
                        ServerUrl = model.ServerUrl
                        Connected = isAuthed
                        StatusText = if isAuthed then "已连接" else "未连接"
                    }
                Compact = model.Nav.CompactMode
                BusyRows = busyRows
                ActiveId =
                    match currentWorkspace with
                    | Some(WorkspacePhase.Active(conv, _)) -> Some conv.ConversationId
                    | Some(WorkspacePhase.LoadingConversation id) -> Some id
                    | _ -> None
                ShowArchived = model.Prefs.showArchived
                Lineage = model.Lineage
            }

        // 不变式 1 & 2：
        // 1. RunState.Idle 时投影里 Streaming=false 且无流式光标/停止按钮字段为可见。
        // 2. ConnPhase <> Authed 时投影不存在 Active 对话数据。
        let chatPhase, isStreaming =
            match model.Conn with
            | ConnPhase.Connecting _ -> ChatPhase.Empty IdleEmptyKind.NotConnected, false
            | ConnPhase.Pairing p ->
                ChatPhase.Empty(IdleEmptyKind.ConnectionFailed(p.Error |> Option.defaultValue "配对中")), false
            | ConnPhase.Authed(_, CatalogPhase.LoadingCatalog) -> ChatPhase.Skeleton, false
            | ConnPhase.Authed(_, CatalogPhase.NeedsOnboarding _) -> ChatPhase.Empty IdleEmptyKind.NoProvider, false
            | ConnPhase.Authed(_, CatalogPhase.Ready(_, ws)) ->
                match ws with
                | WorkspacePhase.IdleEmpty -> ChatPhase.Empty IdleEmptyKind.NoConversation, false
                | WorkspacePhase.LoadingConversation _ -> ChatPhase.Skeleton, false
                | WorkspacePhase.Active(conv, run) ->
                    match run with
                    | RunState.Generating(_, _, partial) ->
                        let msgsVS =
                            conv.Messages
                            |> List.mapi (fun i m ->
                                {
                                    Role = m.role
                                    Text = m.text
                                    Reasoning = m.reasoning
                                    CommitId = m.commitId
                                    ToolCalls = m.toolCalls
                                    Attachments = m.attachments
                                    CommittedAt = m.committedAt
                                    Usage = None
                                    IsLastAssistant = false
                                    Streaming = false
                                    AutoCollapseReasoning = false
                                    MissingAttachments = Set.empty
                                    AwaitingApproval = Set.empty
                                    Index = Some i
                                })

                        let fullMsgs =
                            match partial with
                            | Some p ->
                                let idx = msgsVS.Length

                                msgsVS
                                @ [
                                    {
                                        Role = p.role
                                        Text = p.text
                                        Reasoning = p.reasoning
                                        CommitId = None
                                        ToolCalls = p.toolCalls
                                        Attachments = p.attachments
                                        CommittedAt = p.committedAt
                                        Usage = None
                                        IsLastAssistant = true
                                        Streaming = true
                                        AutoCollapseReasoning = false
                                        MissingAttachments = Set.empty
                                        AwaitingApproval = Set.empty
                                        Index = Some idx
                                    }
                                ]
                            | None -> msgsVS

                        ChatPhase.Messages fullMsgs, true
                    | RunState.Idle _ ->
                        if List.isEmpty conv.Messages then
                            ChatPhase.Empty IdleEmptyKind.EmptyConversation, false
                        else
                            let lastAsst =
                                conv.Messages |> List.tryFindIndexBack (fun m -> m.role = "assistant")

                            ChatPhase.Messages(
                                conv.Messages
                                |> List.mapi (fun i m ->
                                    {
                                        Role = m.role
                                        Text = m.text
                                        Reasoning = m.reasoning
                                        CommitId = m.commitId
                                        ToolCalls = m.toolCalls
                                        Attachments = m.attachments
                                        CommittedAt = m.committedAt
                                        Usage = None
                                        IsLastAssistant = (Some i = lastAsst)
                                        Streaming = false
                                        AutoCollapseReasoning = false
                                        MissingAttachments = Set.empty
                                        AwaitingApproval = Set.empty
                                        Index = Some i
                                    })
                            ),
                            false

        let chatVS: ChatVS =
            {
                Phase = chatPhase
                Streaming = isStreaming
                ScrollHint =
                    Some
                        {
                            CanScrollDown = true
                            CanLoadOlder = false
                        }
                RevealTarget = model.Chrome.RevealTarget |> Option.map snd
                // 生成错误：唯一的来源是 `RunState.Idle (Some err)`（线协议的 generation.finished 带错误）。
                // 这是错误卡的**单一归属**——之前错误由宿主从 `RunState` 直接读、经
                // `ChatScreen.Render` 的 `Error` 字段传下去，状态树里没有它。
                Error =
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, RunState.Idle err))) -> err
                    | _ -> None
            }

        let composerEnabled, disabledReason =
            if not isAuthed then
                false, Some "未连接到万象主机"
            else
                match currentCatalog with
                | None -> false, Some "正在加载模型能力目录…"
                | Some cat when List.isEmpty cat.providers -> false, Some "还没有可用的模型服务"
                | Some _ -> if isStreaming then false, Some "正在生成回复中…" else true, None

        let sendButtonVS: SendButtonVS =
            if isStreaming then
                {
                    State = SendButtonState.GeneratingStop
                    Label = "停止"
                }
            elif not (Map.isEmpty model.Feedback.PendingCommands) then
                {
                    State = SendButtonState.Sending
                    Label = "发送中…"
                }
            else
                {
                    State = SendButtonState.Idle
                    Label = "发送"
                }

        let attachmentsVS =
            model.Composer.Attachments
            |> List.map (fun a ->
                {
                    Id = a.Id
                    FileName = a.FileName
                    MediaType = a.MediaType
                    Size = a.Size
                    ProgressPercent = a.ProgressPercent
                    Ready = a.Ready
                    Failed = a.Failed
                })

        let modelChip =
            model.Composer.SelectedModel |> Option.map (fun (p, m) -> sprintf "%s / %s" p m)

        // 决策 320/330/331：看图能力按「当前所选模型」判定；未选时用目录默认（与空闲页芯片同一口径）。
        // 没有目录（未连接/加载中）时未知：门控放行，主机决定。
        let visionVS, visionLabel =
            match currentCatalog with
            | None -> VisionUnknown, ""
            | Some cat ->
                let sel =
                    model.Composer.SelectedModel |> Option.orElse (Catalog.defaultSelection cat)

                match sel with
                | Some(providerId, modelId) ->
                    fst (Catalog.visionOf providerId modelId cat), Catalog.describeModel providerId modelId cat
                | None -> VisionUnknown, ""

        let composerContextVS =
            match currentWorkspace with
            | Some(WorkspacePhase.Active(conv, _)) ->
                Some
                    {
                        ProviderId = conv.Config.provider
                        Model = conv.Config.model
                        ContextSummary = sprintf "会话模型：%s" conv.Config.model
                    }
            | _ -> None

        let composerVS: ComposerVS =
            {
                Enabled = composerEnabled
                DisabledReason = disabledReason
                Text = model.Composer.Text
                Caret = model.Composer.Caret
                EnterSends = model.Composer.EnterSends
                Send = sendButtonVS
                Tray = attachmentsVS
                ModelChip = modelChip
                Context = composerContextVS
                Compact = model.Nav.CompactMode
                Vision = visionVS
                VisionLabel = visionLabel
            }

        let overlayVS: OverlayVS =
            match model.Overlay with
            | OverlayPhase.Closed -> OverlayVS.Closed
            | OverlayPhase.DrawerOpen -> OverlayVS.Drawer { IsOpen = true }
            | OverlayPhase.PopupOpen kind -> OverlayVS.Popup { Kind = kind; AnchorId = "" }

        let toastsVS =
            model.Toasts
            |> List.map (fun t ->
                {
                    Id = t.Id
                    Text = t.Text
                    Tone = t.Tone
                    UndoAction = t.UndoAction
                    ExpiresAt = t.ExpiresAt
                })

        let approvalsVS =
            {
                Items =
                    model.PendingApprovals
                    |> Map.toList
                    |> List.map (fun (id, a) ->
                        {
                            ApprovalId = id
                            ToolName = a.ToolName
                            CallId = a.CallId
                            Summary = a.Summary
                            ArgumentsJson = a.ArgumentsJson
                            Policy = a.Policy
                            ConversationId = a.ConversationId
                        })
            }

        // 设置页的唯一投影（W7-B）：每个字段都来自模型事实，无壳参数、无旁路。
        let instanceId =
            match model.Conn with
            | ConnPhase.Authed(inst, _) -> inst
            | _ -> ""

        let settingsVS: SettingsVS =
            {
                ActiveSection = model.Settings.ActiveSection
                Catalog = currentCatalog
                InstanceId = instanceId
                ServerUrl = model.ServerUrl
                Prefs = model.Prefs
                Devices =
                    {
                        ServerUrl = model.ServerUrl
                        Devices = model.Settings.Devices
                        CanInvite = model.Settings.CanInvite
                        Invite = model.Settings.Invite
                        InviteRejected = model.Settings.InviteRejected
                        LoadNote = model.Settings.DeviceLoadNote
                        IsLoading = model.Settings.DeviceLoadDue.IsSome
                        Now = model.Now
                    }
            }

        {
            Shell = shellVS
            Header = headerVS
            Sidebar = sidebarVS
            Chat = chatVS
            Composer = composerVS
            Settings = if model.Settings.Open then Some settingsVS else None
            Overlay = overlayVS
            Palette = None
            ModelSwitch = None
            Approval = approvalsVS
            Toasts = toastsVS
        }
