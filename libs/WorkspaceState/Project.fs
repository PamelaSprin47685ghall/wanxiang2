namespace Wanxiang2.WorkspaceState

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

module Project =

    let project (model: WorkspaceModel) : WorkspaceViewState =
        let shellVS: ShellVS = {
            Compact = model.Nav.CompactMode
            SidebarOpen = not model.Nav.SidebarCollapsed || model.Nav.DrawerOpen
            SidebarWidth = model.Nav.SidebarWidth
            StartingPageVisible = model.Chrome.StartingPageVisible
        }

        let isAuthed, currentCatalog, currentWorkspace =
            match model.Conn with
            | ConnPhase.Authed(_, CatalogPhase.Ready(cat, ws)) -> true, Some cat, Some ws
            | _ -> false, None, None

        let headerVS: ChatHeaderVS =
            match currentWorkspace with
            | Some (WorkspacePhase.Active (conv, run)) ->
                let titleMode =
                    match model.Chrome.HeaderEditingDraft with
                    | Some draft -> TitleMode.Editing draft
                    | None -> TitleMode.Viewing(conv.Title, true)
                let genStatus =
                    match run with
                    | RunState.Generating _ -> Some "生成中…"
                    | RunState.Idle (Some _) -> Some "生成遇到问题"
                    | _ -> None
                let crumb =
                    conv.ParentId
                    |> Option.map (fun pid -> { ParentId = pid; ParentTitle = "上级对话"; Clickable = true })
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
                    Visible = false
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
                let isSelected =
                    match currentWorkspace with
                    | Some (WorkspacePhase.Active(conv, _)) when conv.ConversationId = s.Id -> true
                    | Some (WorkspacePhase.LoadingConversation id) when id = s.Id -> true
                    | _ -> false
                let isBusy =
                    model.Feedback.PendingDeletions
                    |> Map.exists (fun _ b -> List.contains s.Id b.Ids)
                {
                    Id = s.Id
                    Title = s.Title
                    Preview = s.Preview
                    Selected = isSelected
                    Pinned = s.Pinned
                    Archived = s.Archived
                    Running = s.Running
                    Busy = isBusy
                    MessageCount = s.MessageCount
                }
            )

        let emptyKind =
            if not isAuthed then SidebarEmptyKind.NotConnected model.Nav.CompactMode
            elif List.isEmpty model.Summaries then
                if not (String.IsNullOrWhiteSpace model.Chrome.SearchQuery) then
                    SidebarEmptyKind.NoSearchResults model.Chrome.SearchQuery
                else
                    SidebarEmptyKind.NoConversations
            else SidebarEmptyKind.NoConversations

        let searchBoxVS: SearchBoxVS = {
            Text = model.Chrome.SearchQuery
            EmptyHintVisible = false
            Active = model.Chrome.SearchActive
            Debouncing = model.Chrome.SearchDebouncingUntil.IsSome
        }

        let batchBarVS =
            if model.Chrome.BatchMode then
                Some {
                    SelectedCount = Set.count model.Chrome.SelectedRowIds
                    AllSelected = Set.count model.Chrome.SelectedRowIds = model.Summaries.Length && not (List.isEmpty model.Summaries)
                    Busy = false
                    AllPinned = false
                }
            else None

        let busyRows =
            model.Feedback.PendingDeletions
            |> Map.toList
            |> List.collect (fun (_, b) -> b.Ids)
            |> Set.ofList

        let sidebarVS: SidebarVS = {
            Rows = rowsVS
            Empty = emptyKind
            Search = searchBoxVS
            Brand = { PresetsVisible = false }
            Batch = batchBarVS
            Host = {
                ServerUrl = ""
                Connected = isAuthed
                StatusText = if isAuthed then "已连接" else "未连接"
                PaletteVisible = false
            }
            Compact = model.Nav.CompactMode
            BusyRows = busyRows
        }

        // 不变式 1 & 2：
        // 1. RunState.Idle 时投影里 Streaming=false 且无流式光标/停止按钮字段为可见。
        // 2. ConnPhase <> Authed 时投影不存在 Active 对话数据。
        let chatPhase, isStreaming =
            match model.Conn with
            | ConnPhase.Connecting _ ->
                ChatPhase.Empty IdleEmptyKind.NotConnected, false
            | ConnPhase.Pairing p ->
                ChatPhase.Empty (IdleEmptyKind.ConnectionFailed (p.Error |> Option.defaultValue "配对中")), false
            | ConnPhase.Authed(_, CatalogPhase.LoadingCatalog) ->
                ChatPhase.Skeleton, false
            | ConnPhase.Authed(_, CatalogPhase.NeedsOnboarding _) ->
                ChatPhase.Empty IdleEmptyKind.NoProvider, false
            | ConnPhase.Authed(_, CatalogPhase.Ready(_, ws)) ->
                match ws with
                | WorkspacePhase.IdleEmpty ->
                    ChatPhase.Empty IdleEmptyKind.NoConversation, false
                | WorkspacePhase.LoadingConversation _ ->
                    ChatPhase.Skeleton, false
                | WorkspacePhase.Active(conv, run) ->
                    match run with
                    | RunState.Generating(_, _, partial) ->
                        let msgsVS =
                            conv.Messages
                            |> List.mapi (fun i m ->
                                { Role = m.role
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
                                  Index = Some i }
                            )
                        let fullMsgs =
                            match partial with
                            | Some p ->
                                let idx = msgsVS.Length
                                msgsVS @ [ { Role = p.role
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
                                             Index = Some idx } ]
                            | None -> msgsVS
                        ChatPhase.Messages fullMsgs, true
                    | RunState.Idle _ ->
                        if List.isEmpty conv.Messages then
                            ChatPhase.Empty IdleEmptyKind.EmptyConversation, false
                        else
                            let lastAsst = conv.Messages |> List.tryFindIndexBack (fun m -> m.role = "assistant")
                            ChatPhase.Messages (
                                conv.Messages
                                |> List.mapi (fun i m ->
                                    { Role = m.role
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
                                      Index = Some i }
                                )
                            )
                            , false

        let chatVS: ChatVS = {
            Phase = chatPhase
            Streaming = isStreaming
            ScrollHint = Some { CanScrollDown = true; CanLoadOlder = false }
            RevealTarget = model.Chrome.RevealTarget
        }

        let composerEnabled, disabledReason =
            if not isAuthed then false, Some "未连接到万象主机"
            else
                match currentCatalog with
                | None -> false, Some "正在加载模型能力目录…"
                | Some cat when List.isEmpty cat.providers -> false, Some "还没有可用的模型服务"
                | Some _ ->
                    if isStreaming then false, Some "正在生成回复中…"
                    else true, None

        let sendButtonVS: SendButtonVS =
            if isStreaming then
                { State = SendButtonState.GeneratingStop; Label = "停止" }
            elif not (Map.isEmpty model.Feedback.PendingCommands) then
                { State = SendButtonState.Sending; Label = "发送中…" }
            else
                { State = SendButtonState.Idle; Label = "发送" }

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
                }
            )

        let modelChip =
            model.Composer.SelectedModel
            |> Option.map (fun (p, m) -> sprintf "%s / %s" p m)

        let composerContextVS =
            match currentWorkspace with
            | Some (WorkspacePhase.Active(conv, _)) ->
                Some {
                    ProviderId = conv.Config.provider
                    Model = conv.Config.model
                    ContextSummary = sprintf "会话模型：%s" conv.Config.model
                }
            | _ -> None

        let composerVS: ComposerVS = {
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
        }

        let overlayVS: OverlayVS =
            match model.Overlay with
            | OverlayPhase.Closed -> OverlayVS.Closed
            | OverlayPhase.DrawerOpen -> OverlayVS.Drawer { IsOpen = true }
            | OverlayPhase.DialogOpen kind ->
                OverlayVS.Dialog {
                    Kind = kind
                    Title = "对话框"
                    CanDismiss = true
                    Confirm =
                        match kind with
                        | DialogKind.Confirm (_, b, _) ->
                            Some {
                                Body = b
                                ConfirmLabel = "确认"
                                DangerFirst = false
                                IsPending = false
                            }
                        | _ -> None
                    Connect =
                        match kind with
                        | DialogKind.Connect url ->
                            Some {
                                DefaultUrl = url
                                IsPairing = false
                                StatusText = ""
                                StatusVisible = false
                                RetryLabel = None
                                RetryVisible = false
                                CanSubmit = true
                                IsPending = false
                            }
                        | _ -> None
                    Prompt =
                        match kind with
                        | DialogKind.Prompt (_, h, init, label) ->
                            Some {
                                Hint = h
                                InitialText = init
                                ConfirmLabel = label
                                IsPending = false
                                ErrorText = None
                                HelperText = None
                            }
                        | _ -> None
                    SessionPrefs =
                        match kind with
                        | DialogKind.SessionPrefs cfg ->
                            Some {
                                Instructions = defaultArg cfg.instructions ""
                                Temperature = cfg.temperature |> Option.map string |> Option.defaultValue ""
                                TopP = cfg.topP |> Option.map string |> Option.defaultValue ""
                                MaxTokens = cfg.maxTokens |> Option.map string |> Option.defaultValue ""
                                ThinkingBudget = cfg.thinkingBudget |> Option.map string |> Option.defaultValue ""
                                Tools = []
                                Customized = false
                                SummaryError = None
                                IsPending = false
                            }
                        | _ -> None
                    ShortcutHelp =
                        match kind with
                        | DialogKind.ShortcutHelp -> Some { Sections = [] }
                        | _ -> None
                    TextEdit =
                        match kind with
                        | DialogKind.TextEdit (_, init, label, _) ->
                            Some {
                                Initial = init
                                ConfirmLabel = label
                                IsPending = false
                            }
                        | _ -> None
                    Export =
                        match kind with
                        | DialogKind.Export _ ->
                            Some {
                                StatusText = ""
                                Tone = "muted"
                                ProgressVisible = false
                                Indeterminate = false
                                ProgressValue = 0.0
                                RetryVisible = false
                                SaveVisible = false
                                SaveJsonVisible = false
                                SaveEnabled = false
                                SaveJsonEnabled = false
                                CloseText = "取消"
                            }
                        | _ -> None
                    Import =
                        match kind with
                        | DialogKind.Import ->
                            Some {
                                StatusText = ""
                                Tone = "muted"
                                Details = None
                                ImportVisible = false
                                ImportEnabled = false
                                PickEnabled = true
                                PickText = "选择文件"
                            }
                        | _ -> None
                }
            | OverlayPhase.PopupOpen kind ->
                OverlayVS.Popup {
                    Kind = kind
                    AnchorId = ""
                }

        let toastsVS =
            model.Toasts
            |> List.map (fun t ->
                {
                    Id = t.Id
                    Text = t.Text
                    Tone = t.Tone
                    UndoAction = t.UndoAction
                    ExpiresAt = t.ExpiresAt
                }
            )

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
                        }
                    )
            }

        {
            Shell = shellVS
            Header = headerVS
            Sidebar = sidebarVS
            Chat = chatVS
            Composer = composerVS
            Settings = None
            Onboarding = None
            Overlay = overlayVS
            Palette = None
            ModelSwitch = None
            Approval = approvalsVS
            Toasts = toastsVS
        }
