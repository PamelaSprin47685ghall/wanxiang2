namespace Wanxiang2.WorkspaceState

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

module Workspace =

    let project = Project.project

    /// 用户消息的 JSON 形状：`{"role":"user","contents":[文本项, 附件项…]}`。
    ///
    /// 这与 `compose/Outbox` 的 `MessageOutbox.BuildMessage` 是**同一份形状**
    /// （同一仓库里两条发送路径必须给出同样的 payload，否则服务端与客户端解析会分叉）。
    /// 附件项是 `{"type":"attachment","sha256":…,"size":…,"mediaType":…,"fileName":…}`，
    /// 解析侧是 `libs/MediaContent/MessageSerde.fs` 的 `attachmentRefs`
    /// 与 `libs/MessageView` 的同名分支。
    let private buildMessageJson (text: string) (attachments: ComposerAttachmentDraft list) : JsonNode =
        let contents = JsonArray()

        if not (String.IsNullOrWhiteSpace text) then
            let node = JsonObject()
            node["text"] <- text
            contents.Add node

        for a in attachments do
            let node = JsonObject()
            node["type"] <- "attachment"
            node["sha256"] <- a.Sha256
            node["size"] <- a.Size
            node["mediaType"] <- a.MediaType
            node["fileName"] <- a.FileName
            contents.Add node

        let message = JsonObject()
        message["role"] <- "user"
        message["contents"] <- contents
        message :> JsonNode

    let init () : WorkspaceModel =
        let limits: UiPrefLimits =
            {
                sidebarWidth = 260.0
                sidebarMinWidth = 200.0
                sidebarMaxWidth = 400.0
                windowMinWidth = 700.0
            }

        let initialPrefs = UiPrefs.defaults limits

        {
            Conn = ConnPhase.Connecting 0
            Nav =
                {
                    CompactMode = false
                    DrawerOpen = false
                    SidebarCollapsed = false
                    SidebarWidth = 260.0
                }
            Overlay = OverlayPhase.Closed
            Feedback =
                {
                    PendingCommands = Map.empty
                    PendingDeletions = Map.empty
                    PendingConfigs = Map.empty
                }
            Prefs = initialPrefs
            Chrome =
                {
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
            Composer =
                {
                    Text = ""
                    Caret = None
                    EnterSends = true
                    SelectedModel = None
                    Attachments = []
                }
            Summaries = []
            Lineage = Map.empty
            Onboarding = OnboardingFacts.empty
            Toasts = []
            NextToastId = 1
            NextTimerToken = 1
            PendingApprovals = Map.empty
            PaletteRecent = []
            Settings =
                {
                    Open = false
                    ActiveSection = None
                    Devices = []
                    CanInvite = false
                    Invite = None
                    InviteRejected = None
                    DeviceLoadDue = None
                    DeviceLoadNote = None
                }
            ServerUrl = ""
            Now = DateTimeOffset.UnixEpoch
        }

    let private addToast
        (text: string)
        (tone: ToastTone)
        (undo: string option)
        (now: DateTimeOffset)
        (model: WorkspaceModel)
        : WorkspaceModel * Effect list =
        let id = model.NextToastId

        let item: ToastItem =
            {
                Id = id
                Text = text
                Tone = tone
                UndoAction = undo
                ExpiresAt = now.AddSeconds 6.0
            }

        let nextModel =
            { model with
                Toasts = model.Toasts @ [ item ]
                NextToastId = id + 1
            }

        nextModel, [ Effect.ShowToast(text, tone) ]

    let rec update (now: DateTimeOffset) (msg: Msg) (model: WorkspaceModel) : WorkspaceModel * Effect list =
        match msg with
        | MsgTick tickNow ->
            // 2. 检查搜索防抖
            let modelAfterSearch, searchEffects =
                match model.Chrome.SearchDebouncingUntil with
                | Some due when tickNow >= due ->
                    let q = model.Chrome.SearchQuery

                    let nextChrome =
                        { model.Chrome with
                            SearchDebouncingUntil = None
                        }

                    let queryData: SearchQueryData =
                        {
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

                        let cmd =
                            ClientCommand.DeleteConversation
                                {|
                                    invocationId = invId
                                    conversationId = id
                                |}

                        Effect.SendWire(WireEvent.Command cmd)))

            let modelAfterDeletions =
                { modelAfterSearch with
                    Feedback =
                        { modelAfterSearch.Feedback with
                            PendingDeletions = remainingDeletions
                        }
                    Toasts = model.Toasts |> List.filter (fun t -> t.ExpiresAt > tickNow)
                }
            // 4. 定位高亮过期
            let finalModel =
                match modelAfterDeletions.Chrome.RevealTargetExpiresAt with
                | Some exp when tickNow >= exp ->
                    { modelAfterDeletions with
                        Chrome =
                            { modelAfterDeletions.Chrome with
                                RevealTarget = None
                                RevealTargetExpiresAt = None
                            }
                    }
                | _ -> modelAfterDeletions
            // 5. 设备列表有界等待到点：旧主机不认识 devices.request 时不会回应，不能一直「正在读取」
            let modelAfterDeviceWait =
                match finalModel.Settings.DeviceLoadDue with
                | Some due when tickNow >= due ->
                    { finalModel with
                        Settings =
                            { finalModel.Settings with
                                DeviceLoadDue = None
                                DeviceLoadNote = Some BoundedWaits.devicesTimeoutText
                            }
                    }
                | _ -> finalModel

            { modelAfterDeviceWait with
                Now = tickNow
            },
            searchEffects @ deleteEffects

        | WireRecv wireEvent ->
            match wireEvent with
            | WireEvent.Hello _ -> model, []
            | WireEvent.UpgradeRequired _ -> addToast "主机需要更高版本的协议，请升级客户端。" ToastTone.Warning None now model
            | WireEvent.AuthAccepted d ->
                let nextConn = ConnPhase.Authed(d.instanceId, CatalogPhase.LoadingCatalog)

                let effects =
                    [
                        Effect.SendWire WireEvent.CatalogRequest
                        Effect.SendWire WireEvent.ObserveConversationList
                    ]

                { model with Conn = nextConn }, effects
            | WireEvent.PairingStarted d ->
                let pairing =
                    {
                        Code = None
                        ExpiresAt = Some(now.AddSeconds(float d.expiresInSeconds))
                        FrozenMinutes = None
                        Error = None
                    }

                { model with
                    Conn = ConnPhase.Pairing pairing
                },
                []
            | WireEvent.AuthRejected _ ->
                let nextConn = ConnPhase.Connecting 0
                { model with Conn = nextConn }, [ Effect.ShowToast("认证失败，请重新配对。", ToastTone.Warning) ]
            | WireEvent.PairingSucceeded d ->
                let effects = [ Effect.SendWire(WireEvent.AuthPresent {| token = d.token |}) ]
                model, effects
            | WireEvent.PairingFailed d ->
                let pairing =
                    {
                        Code = None
                        ExpiresAt = None
                        FrozenMinutes = None
                        Error = Some d.reason
                    }

                { model with
                    Conn = ConnPhase.Pairing pairing
                },
                [ Effect.ShowToast(sprintf "配对失败：%s" d.reason, ToastTone.Warning) ]
            | WireEvent.CatalogSnapshot d ->
                let catalog = Catalog.parse d.providers d.tools d.generation

                match model.Conn with
                | ConnPhase.Authed(instId, _) ->
                    let nextWs =
                        match model.Conn with
                        | ConnPhase.Authed(_, CatalogPhase.Ready(_, ws)) -> ws
                        | _ -> WorkspacePhase.IdleEmpty

                    let nextCatalog = CatalogPhase.Ready(catalog, nextWs)

                    { model with
                        Conn = ConnPhase.Authed(instId, nextCatalog)
                    },
                    []
                | _ -> model, []
            | WireEvent.ConversationListSnapshot d ->
                // 全量解析归 ConversationSummary 库（坏 id 行如实丢弃，见 D6）；
                // 谱系出自同一份快照（有 parentId 的行才入表），不另造数据源。
                { model with
                    Summaries = ConversationSummary.parseList d.items
                    Lineage = ForkLineage.parse d.items
                },
                []
            | WireEvent.DevicesSnapshot d ->
                { model with
                    Now = now
                    Settings =
                        { model.Settings with
                            Devices = DevicesModel.parse d.items
                            CanInvite = d.canInvite
                            DeviceLoadDue = None
                            DeviceLoadNote = None
                        }
                },
                []
            | WireEvent.PairingInviteIssued d ->
                { model with
                    Now = now
                    Settings =
                        { model.Settings with
                            Invite =
                                Some
                                    {
                                        code = d.code
                                        expiresAtUtc = d.expiresAtUtc
                                    }
                            InviteRejected = None
                        }
                },
                []
            | WireEvent.PairingInviteRejected d ->
                { model with
                    Now = now
                    Settings =
                        { model.Settings with
                            Invite = None
                            InviteRejected = Some d.reason
                        }
                },
                []
            | WireEvent.ConversationSnapshot d ->
                let msgs = d.messages |> Seq.map MessageView.ofSnapshotItem |> Seq.toList

                let activeConv: ActiveConvState =
                    {
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
                    let nextCatalog =
                        CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, RunState.Idle None))

                    let nextModel =
                        { model with
                            Conn = ConnPhase.Authed(instId, nextCatalog)
                        }
                    // 定位目标（搜索命中 / 消息链接）：消息已加载时统一发 Effect，宿主执行滚动与高亮。
                    let loaded =
                        match model.Chrome.RevealTarget with
                        | Some(targetConv, messageId) when targetConv = d.conversationId ->
                            msgs
                            |> List.exists (fun m -> m.commitId |> Option.exists (fun c -> string c = messageId))
                        | _ -> false

                    if loaded then
                        // 定位优先：滚到那条消息并高亮（不再滚到末尾）
                        let target =
                            match model.Chrome.RevealTarget with
                            | Some(_, messageId) -> messageId
                            | None -> ""

                        let cleared =
                            { nextModel with
                                Chrome =
                                    { nextModel.Chrome with
                                        RevealTarget = None
                                        RevealTargetExpiresAt = None
                                    }
                            }

                        cleared, [ Effect.ScrollChat(ScrollCommand.ToExchange target) ]
                    else
                        nextModel, [ Effect.ScrollChat ScrollCommand.End ]
                | _ -> model, []
            | WireEvent.GenerationStarted d ->
                match model.Conn with
                | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, _))) when
                    activeConv.ConversationId = d.conversationId
                    ->
                    let run = RunState.Generating(d.generationId, now, None)
                    let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, run))

                    { model with
                        Conn = ConnPhase.Authed(instId, nextCatalog)
                    },
                    [ Effect.ScrollChat ScrollCommand.End ]
                | _ -> model, []
            | WireEvent.GenerationDelta d ->
                match model.Conn with
                | ConnPhase.Authed(instId,
                                   CatalogPhase.Ready(cat,
                                                      WorkspacePhase.Active(activeConv,
                                                                            RunState.Generating(genId, startedAt, _)))) when
                    activeConv.ConversationId = d.conversationId && genId = d.generationId
                    ->
                    let partialMsg = MessageView.ofJson d.payload
                    let run = RunState.Generating(genId, startedAt, Some partialMsg)
                    let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, run))

                    { model with
                        Conn = ConnPhase.Authed(instId, nextCatalog)
                    },
                    [ Effect.ScrollChat ScrollCommand.End ]
                | _ -> model, []
            | WireEvent.GenerationFinished d ->
                match model.Conn with
                | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, _))) when
                    activeConv.ConversationId = d.conversationId
                    ->
                    let run = RunState.Idle d.error
                    let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, run))

                    { model with
                        Conn = ConnPhase.Authed(instId, nextCatalog)
                    },
                    [ Effect.ScrollChat ScrollCommand.End; Effect.Focus FocusTarget.ComposerInput ]
                | _ -> model, []
            | WireEvent.CommandAccepted _ -> model, []
            | WireEvent.AttachmentCommitted d ->
                // 主机**确认**收到并落盘了这份附件（`attachment.committed`）。
                // 这才是「附件就绪」的信号——发出 `attachment.complete` 不等于落盘。
                let update (a: ComposerAttachmentDraft) =
                    if a.Id = d.attachmentId then
                        { a with
                            ProgressPercent = 100
                            Ready = true
                            Failed = false
                            Sha256 = d.sha256
                        }
                    else
                        a

                { model with
                    Composer =
                        { model.Composer with
                            Attachments = model.Composer.Attachments |> List.map update
                        }
                },
                []
            | WireEvent.AttachmentAborted d ->
                // 主机拒绝或落盘失败（`attachment.aborted`）：这条附件标为失败，不随消息发出。
                let update (a: ComposerAttachmentDraft) =
                    if a.Id = d.attachmentId then
                        { a with Failed = true; Ready = false }
                    else
                        a

                { model with
                    Composer =
                        { model.Composer with
                            Attachments = model.Composer.Attachments |> List.map update
                        }
                },
                []
            | WireEvent.HistoryPage d ->
                // 更早一页（分页）：按 commitId 升序前置拼接，并按 commitId 去重
                // （快照与分页会交叠，同一条消息不能出现两次）。
                match model.Conn with
                | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, run))) when
                    activeConv.ConversationId = d.conversationId
                    ->
                    let existing = activeConv.Messages |> Seq.choose (fun m -> m.commitId) |> Set.ofSeq

                    let older =
                        d.items
                        |> Seq.map MessageView.ofSnapshotItem
                        |> Seq.filter (fun m ->
                            match m.commitId with
                            | Some c -> not (Set.contains c existing)
                            | None -> false)
                        |> Seq.toList

                    let earliest = older |> Seq.choose (fun m -> m.commitId) |> Seq.tryLast

                    let nextConv =
                        { activeConv with
                            Messages = older @ activeConv.Messages
                            PageEarliest =
                                (match earliest with
                                 | Some c -> c
                                 | None -> activeConv.PageEarliest)
                            PageHasMore = d.hasMore
                            PageLoading = false
                        }

                    let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(nextConv, run))

                    { model with
                        Conn = ConnPhase.Authed(instId, nextCatalog)
                    },
                    []
                | _ -> model, []
            | WireEvent.CommandCommitted d ->
                let remainingCommands = Map.remove d.invocationId model.Feedback.PendingCommands

                let nextFeedback =
                    { model.Feedback with
                        PendingCommands = remainingCommands
                    }

                { model with Feedback = nextFeedback }, []
            | WireEvent.CommandRejected d ->
                let remainingCommands = Map.remove d.invocationId model.Feedback.PendingCommands

                let nextFeedback =
                    { model.Feedback with
                        PendingCommands = remainingCommands
                    }

                let modelAfter, effs =
                    addToast
                        (sprintf "命令未执行：%s" d.message)
                        ToastTone.Warning
                        None
                        now
                        { model with Feedback = nextFeedback }

                modelAfter, effs
            | WireEvent.ToolApprovalRequested d ->
                let item =
                    {|
                        ToolName = d.toolName
                        CallId = d.callId
                        Summary = d.summary
                        ArgumentsJson =
                            (if isNull d.arguments then
                                 ""
                             else
                                 d.arguments.ToJsonString())
                        Policy = d.policy
                        ConversationId = d.conversationId
                    |}

                let nextApprovals = Map.add d.approvalId item model.PendingApprovals

                let activeConvId =
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) ->
                        Some conv.ConversationId
                    | _ -> None

                let modelWithApproval =
                    { model with
                        PendingApprovals = nextApprovals
                    }

                if activeConvId <> Some d.conversationId then
                    addToast "另一个对话里有工具调用在等你批准。" ToastTone.Info None now modelWithApproval
                else
                    modelWithApproval, []
            | WireEvent.ToolApprovalResolved d ->
                let nextApprovals = Map.remove d.approvalId model.PendingApprovals

                let modelWithout =
                    { model with
                        PendingApprovals = nextApprovals
                    }

                if d.alreadyHandled then
                    addToast (sprintf "这个审批已经处理过了（%s）。" d.status) ToastTone.Info None now modelWithout
                else
                    modelWithout, []
            | _ -> model, []

        | ConnectionUp instanceId ->
            let effects =
                [
                    Effect.SendWire WireEvent.CatalogRequest
                    Effect.SendWire WireEvent.ObserveConversationList
                ]

            { model with
                Conn = ConnPhase.Authed(instanceId, CatalogPhase.LoadingCatalog)
            },
            effects

        | ConnectionDown _ ->
            // 不变式 3：ConnectionDown 之后 pending 计数归零（防 Pending Wedge）
            let resetFeedback =
                {
                    PendingCommands = Map.empty
                    PendingDeletions = Map.empty
                    PendingConfigs = Map.empty
                }
            // 不变式 2：ConnPhase <> Authed 时不存在 Active 对话数据（切走即整体丢弃）
            let nextConn = ConnPhase.Connecting 1
            // 设备事实随连接作废（设备数据只属于这台主机）；设置页本身保持打开。
            let resetDevices =
                { model.Settings with
                    Devices = []
                    CanInvite = false
                    Invite = None
                    InviteRejected = None
                    DeviceLoadDue = None
                    DeviceLoadNote =
                        if model.Settings.DeviceLoadDue.IsSome then
                            Some "读取设备列表时连接断开了。如果主机版本较旧，可能还不支持设备管理，请先升级主机。"
                        else
                            None
                }

            { model with
                Conn = nextConn
                Feedback = resetFeedback
                PendingApprovals = Map.empty
                Settings = resetDevices
            },
            [ Effect.ShowToast("已断开连接", ToastTone.Warning) ]

        | ConnectionRequested url -> { model with ServerUrl = url }, []

        | TimerFired _ -> model, []

        | EveryFired _ -> model, []

        | PrefsLoaded prefs ->
            { model with Prefs = prefs },
            [
                Effect.ApplyTheme(
                    match prefs.theme with
                    | FollowSystem -> ThemeKind.System
                    | AlwaysLight -> ThemeKind.Light
                    | AlwaysDark -> ThemeKind.Dark
                )
            ]

        | User userMsg ->
            match userMsg with
            | UserMsg.Sidebar sidebarMsg ->
                match sidebarMsg with
                | SidebarUserMsg.SelectConv convId ->
                    match model.Conn with
                    | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, _)) ->
                        let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.LoadingConversation convId)

                        let effects =
                            [ Effect.SendWire(WireEvent.ObserveConversation {| conversationId = convId |}) ]

                        { model with
                            Conn = ConnPhase.Authed(instId, nextCatalog)
                        },
                        effects
                    | _ -> model, []
                | SidebarUserMsg.NewConversation ->
                    match model.Conn with
                    | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, _)) ->
                        let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.IdleEmpty)

                        { model with
                            Conn = ConnPhase.Authed(instId, nextCatalog)
                        },
                        [ Effect.Focus FocusTarget.ComposerInput ]
                    | _ -> model, []
                | SidebarUserMsg.DeleteConversation convId ->
                    let token = Guid.NewGuid()

                    let batch: PendingBatchDeletion =
                        {
                            Token = token
                            Ids = [ convId ]
                            Label = "已删除对话"
                            DueAt = now.AddSeconds 8.0
                        }

                    let nextDeletions = Map.add token batch model.Feedback.PendingDeletions

                    let modelAfterDeletions =
                        { model with
                            Feedback =
                                { model.Feedback with
                                    PendingDeletions = nextDeletions
                                }
                        }

                    addToast "已删除对话" ToastTone.Success (Some(token.ToString())) now modelAfterDeletions
                | SidebarUserMsg.BatchDelete convIds ->
                    let token = Guid.NewGuid()

                    let batch: PendingBatchDeletion =
                        {
                            Token = token
                            Ids = convIds
                            Label = "已删除选中的对话"
                            DueAt = now.AddSeconds 8.0
                        }

                    let nextDeletions = Map.add token batch model.Feedback.PendingDeletions

                    let modelAfterDeletions =
                        { model with
                            Feedback =
                                { model.Feedback with
                                    PendingDeletions = nextDeletions
                                }
                        }

                    addToast "已删除选中的对话" ToastTone.Success (Some(token.ToString())) now modelAfterDeletions
                | SidebarUserMsg.EnterBatch convId ->
                    // 进入多选：已在多选中则只并入这一项；否则以它为第一项。
                    let selected =
                        if model.Chrome.BatchMode then
                            model.Chrome.SelectedRowIds |> Set.add convId
                        else
                            Set.ofList [ convId ]

                    let nextChrome =
                        { model.Chrome with
                            BatchMode = true
                            SelectedRowIds = selected
                        }

                    { model with Chrome = nextChrome }, []
                | SidebarUserMsg.ToggleBatch convId ->
                    // 切换一行；清空最后一项时退出多选（与旧的多选行为一致）。
                    let selected =
                        if model.Chrome.SelectedRowIds |> Set.contains convId then
                            model.Chrome.SelectedRowIds |> Set.remove convId
                        else
                            model.Chrome.SelectedRowIds |> Set.add convId

                    let nextChrome =
                        { model.Chrome with
                            SelectedRowIds = selected
                            BatchMode = not (Set.isEmpty selected)
                        }

                    { model with Chrome = nextChrome }, []
                | SidebarUserMsg.ExitBatch ->
                    let nextChrome =
                        { model.Chrome with
                            BatchMode = false
                            SelectedRowIds = Set.empty
                        }

                    { model with Chrome = nextChrome }, []
                | SidebarUserMsg.ToggleBatchAllVisible visibleIds ->
                    // 可见行已全选则退出多选，否则选中全部可见行（原有的不可见项被清掉）。
                    let visible = visibleIds |> Set.ofList

                    let allSelected =
                        not (Set.isEmpty visible) && Set.isSubset visible model.Chrome.SelectedRowIds

                    let nextChrome =
                        if allSelected then
                            { model.Chrome with
                                BatchMode = false
                                SelectedRowIds = Set.empty
                            }
                        else
                            { model.Chrome with
                                BatchMode = true
                                SelectedRowIds = visible
                            }

                    { model with Chrome = nextChrome }, []
                | SidebarUserMsg.SearchTextChanged q ->
                    let nextChrome =
                        { model.Chrome with
                            SearchQuery = q
                            SearchDebouncingUntil = Some(now.AddMilliseconds 300.0)
                        }

                    { model with Chrome = nextChrome }, []
                | SidebarUserMsg.SearchActivate ->
                    { model with
                        Chrome =
                            { model.Chrome with
                                SearchActive = true
                            }
                    },
                    [ Effect.Focus FocusTarget.SidebarSearch ]
                | SidebarUserMsg.SearchClear ->
                    { model with
                        Chrome =
                            { model.Chrome with
                                SearchQuery = ""
                                SearchActive = false
                                SearchDebouncingUntil = None
                            }
                    },
                    []
                | SidebarUserMsg.SearchHitSelect(convId, messageId) ->
                    // 打开命中所在对话，并把「滚到哪条」登记为定位目标（到期自动清除，见 MsgTick）
                    let nextChrome =
                        { model.Chrome with
                            RevealTarget = messageId |> Option.map (fun m -> convId, m)
                            RevealTargetExpiresAt = Some(now.AddSeconds 15.0)
                        }

                    match model.Conn with
                    | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, _)) ->
                        let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.LoadingConversation convId)

                        { model with
                            Conn = ConnPhase.Authed(instId, nextCatalog)
                            Chrome = nextChrome
                        },
                        [ Effect.SendWire(WireEvent.ObserveConversation {| conversationId = convId |}) ]
                    | _ -> { model with Chrome = nextChrome }, []
                | SidebarUserMsg.ToggleShowArchived ->
                    let nextPrefs =
                        { model.Prefs with
                            showArchived = not model.Prefs.showArchived
                        }

                    { model with Prefs = nextPrefs }, [ Effect.PrefsSave nextPrefs ]
                | _ -> model, []

            | UserMsg.Composer compMsg ->
                match compMsg with
                | ComposerUserMsg.TextChanged text ->
                    // 草稿落盘的**节流在宿主侧**（`compose/AppShell` 的 800 ms 自动保存闸），
                    // 状态树只管「文本是什么」——不在这里每次按键都发一次 `DraftWrite`，
                    // 否则每个键击都写一次 IndexedDB。
                    { model with
                        Composer = { model.Composer with Text = text }
                    },
                    []
                | ComposerUserMsg.CaretChanged caret ->
                    { model with
                        Composer = { model.Composer with Caret = caret }
                    },
                    []
                | ComposerUserMsg.EnterSendsToggled flag ->
                    { model with
                        Composer =
                            { model.Composer with
                                EnterSends = flag
                            }
                    },
                    []
                | ComposerUserMsg.ModelPick(providerId, modelName) ->
                    { model with
                        Composer =
                            { model.Composer with
                                SelectedModel = Some(providerId, modelName)
                            }
                    },
                    []
                | ComposerUserMsg.Attach(attachmentId, fileName, mediaType, size, sha256) ->
                    // 附件草稿进状态树：上传中（Ready=false），由 UploadProgress/UploadFinished/UploadFailed 推进阶段。
                    // id 由发起方给（客户端开始上传时生成）——它同时是线协议里的上传 id，
                    // 两侧必须是同一个，进度消息才对得上。
                    let draft =
                        {
                            Id = attachmentId
                            FileName = fileName
                            MediaType = mediaType
                            Size = size
                            Sha256 = sha256
                            ProgressPercent = 0
                            Ready = false
                            Failed = false
                        }
                    // 追加而不是前插：托盘里的顺序就是用户选择/添加附件的顺序，
                    // 发出的 payload 必须保持同一顺序。
                    { model with
                        Composer =
                            { model.Composer with
                                Attachments = model.Composer.Attachments @ [ draft ]
                            }
                    },
                    []
                | ComposerUserMsg.RemoveAttachment attachmentId ->
                    { model with
                        Composer =
                            { model.Composer with
                                Attachments = model.Composer.Attachments |> List.filter (fun a -> a.Id <> attachmentId)
                            }
                    },
                    []
                | ComposerUserMsg.Send ->
                    // 发送门控与控件层同一口径（`libs/AttachmentTray/ComposerSendState.fs` 的
                    // `blockReason`）：有附件在上传中、或有附件上传失败，**整条发送被挡住**，
                    // 不发「只带部分附件」的消息。
                    // 状态主人自己守这条线——不能指望控件层已经挡住了。
                    let hasUploading =
                        model.Composer.Attachments |> List.exists (fun a -> not a.Ready && not a.Failed)

                    let hasFailed = model.Composer.Attachments |> List.exists (fun a -> a.Failed)

                    if hasUploading || hasFailed then
                        // 挡住：草稿与附件都留着，用户处理完再发。
                        model, []
                    elif
                        String.IsNullOrWhiteSpace model.Composer.Text
                        && List.isEmpty model.Composer.Attachments
                    then
                        model, []
                    else
                        match model.Conn with
                        | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, currentWorkspace)) ->
                            let text = model.Composer.Text
                            // 上面的门控已经保证没有「上传中 / 失败」的附件，
                            // 所以这里全部附件都是就绪的，全部随消息发出，顺序保持不变。
                            let readyAttachments = model.Composer.Attachments

                            let resetComposer =
                                { model.Composer with
                                    Text = ""
                                    Attachments = []
                                }

                            let messageJson = buildMessageJson text readyAttachments

                            match currentWorkspace with
                            | WorkspacePhase.Active(activeConv, _) ->
                                let invId = Guid.NewGuid()

                                let cmd =
                                    ClientCommand.SendUserMessage
                                        {|
                                            invocationId = invId
                                            conversationId = activeConv.ConversationId
                                            messageJson = messageJson
                                        |}

                                let pendingCmd =
                                    {
                                        InvocationId = invId
                                        Command = cmd
                                        SentAt = now
                                        SuccessMessage = None
                                    }

                                let nextFeedback =
                                    { model.Feedback with
                                        PendingCommands = Map.add invId pendingCmd model.Feedback.PendingCommands
                                    }

                                let nextModel =
                                    { model with
                                        Composer = resetComposer
                                        Feedback = nextFeedback
                                    }

                                nextModel,
                                [ Effect.SendWire(WireEvent.Command cmd); Effect.ScrollChat ScrollCommand.End ]
                            | WorkspacePhase.IdleEmpty ->
                                let newConvId = Guid.NewGuid()
                                let invId = Guid.NewGuid()

                                let createCmd =
                                    ClientCommand.CreateConversation
                                        {|
                                            invocationId = invId
                                            conversationId = newConvId
                                            title = if text.Length > 20 then text.Substring(0, 20) else text
                                            config = SessionConfig.empty
                                        |}

                                let appendInvId = Guid.NewGuid()

                                let appendCmd =
                                    ClientCommand.SendUserMessage
                                        {|
                                            invocationId = appendInvId
                                            conversationId = newConvId
                                            messageJson = messageJson
                                        |}

                                let p1 =
                                    {
                                        InvocationId = invId
                                        Command = createCmd
                                        SentAt = now
                                        SuccessMessage = None
                                    }

                                let p2 =
                                    {
                                        InvocationId = appendInvId
                                        Command = appendCmd
                                        SentAt = now
                                        SuccessMessage = None
                                    }

                                let nextFeedback =
                                    { model.Feedback with
                                        PendingCommands =
                                            model.Feedback.PendingCommands |> Map.add invId p1 |> Map.add appendInvId p2
                                    }

                                let nextCatalog =
                                    CatalogPhase.Ready(cat, WorkspacePhase.LoadingConversation newConvId)

                                let nextModel =
                                    { model with
                                        Conn = ConnPhase.Authed(instId, nextCatalog)
                                        Composer = resetComposer
                                        Feedback = nextFeedback
                                    }

                                nextModel,
                                [
                                    Effect.SendWire(WireEvent.Command createCmd)
                                    Effect.SendWire(WireEvent.Command appendCmd)
                                    Effect.SendWire(WireEvent.ObserveConversation {| conversationId = newConvId |})
                                ]
                            | _ -> model, []
                        | _ -> model, []
                | ComposerUserMsg.StopGeneration ->
                    match model.Conn with
                    | ConnPhase.Authed(_,
                                       CatalogPhase.Ready(_,
                                                          WorkspacePhase.Active(activeConv,
                                                                                RunState.Generating(genId, _, _)))) ->
                        let effects =
                            [
                                Effect.SendWire(
                                    WireEvent.GenerationCancel
                                        {|
                                            conversationId = activeConv.ConversationId
                                            generationId = genId
                                        |}
                                )
                            ]

                        model, effects
                    | _ -> model, []

            | UserMsg.Overlay overlayMsg ->
                match overlayMsg with
                | OverlayUserMsg.Escape ->
                    // 浮层 Escape 链：Popup -> Dialog -> Drawer -> Closed，焦点归还发 Effect.Focus
                    match model.Overlay with
                    | OverlayPhase.PopupOpen _ ->
                        { model with
                            Overlay = OverlayPhase.Closed
                        },
                        [ Effect.Focus FocusTarget.ComposerInput ]
                    | OverlayPhase.DialogOpen _ ->
                        { model with
                            Overlay = OverlayPhase.Closed
                        },
                        [ Effect.Focus FocusTarget.ComposerInput ]
                    | OverlayPhase.DrawerOpen ->
                        { model with
                            Overlay = OverlayPhase.Closed
                            Nav = { model.Nav with DrawerOpen = false }
                        },
                        [ Effect.Focus FocusTarget.ComposerInput ]
                    | OverlayPhase.Closed -> model, []
                | OverlayUserMsg.Dismiss ->
                    { model with
                        Overlay = OverlayPhase.Closed
                    },
                    [ Effect.Focus FocusTarget.ComposerInput ]
                | OverlayUserMsg.ToastUndo actionId ->
                    match Guid.TryParse actionId with
                    | true, token ->
                        let restoredDeletions = Map.remove token model.Feedback.PendingDeletions

                        let modelRestored =
                            { model with
                                Feedback =
                                    { model.Feedback with
                                        PendingDeletions = restoredDeletions
                                    }
                            }

                        addToast "已恢复删除的对话" ToastTone.Success None now modelRestored
                    | _ -> model, []
                | OverlayUserMsg.PopupOpened popupId ->
                    { model with
                        Overlay = OverlayPhase.PopupOpen(PopupKind.ToastAnchor popupId)
                    },
                    []
                | OverlayUserMsg.PopupClosed _ ->
                    { model with
                        Overlay = OverlayPhase.Closed
                    },
                    [ Effect.Focus FocusTarget.ComposerInput ]
                | _ -> model, []

            | UserMsg.Header headerMsg ->
                match headerMsg with
                | HeaderUserMsg.ToggleSidebar ->
                    let nextNav =
                        { model.Nav with
                            SidebarCollapsed = not model.Nav.SidebarCollapsed
                        }

                    { model with Nav = nextNav }, []
                | _ -> model, []

            | UserMsg.Palette paletteMsg ->
                match paletteMsg with
                | PaletteUserMsg.PushRecent itemId ->
                    let updated =
                        itemId :: (model.PaletteRecent |> List.filter ((<>) itemId)) |> List.truncate 8

                    { model with PaletteRecent = updated }, []
                | PaletteUserMsg.SetRecent recent ->
                    { model with
                        PaletteRecent = List.truncate 8 recent
                    },
                    []
                | _ -> model, []

            | UserMsg.Settings settingsMsg ->
                match settingsMsg with
                | SettingsUserMsg.OpenSection key ->
                    { model with
                        Settings =
                            { model.Settings with
                                Open = true
                                ActiveSection = key
                            }
                    },
                    []
                | SettingsUserMsg.Close ->
                    { model with
                        Settings = { model.Settings with Open = false }
                    },
                    []
                | SettingsUserMsg.PrefsChanged prefs -> { model with Prefs = prefs }, [ Effect.PrefsSave prefs ]

            | UserMsg.Devices deviceMsg ->
                match deviceMsg with
                | DeviceUserMsg.LoadDevices ->
                    let next =
                        { model.Settings with
                            DeviceLoadDue = Some(now + BoundedWaits.devicesLimit)
                            DeviceLoadNote = None
                        }

                    { model with
                        Now = now
                        Settings = next
                    },
                    [ Effect.SendWire WireEvent.DevicesRequest ]
                | DeviceUserMsg.RequestInvite -> model, [ Effect.SendWire WireEvent.PairingInvite ]
                | DeviceUserMsg.RenameDevice(deviceId, newName) ->
                    let cmd =
                        WireEvent.DeviceRename
                            {|
                                requestId = Guid.NewGuid()
                                deviceId = deviceId
                                name = newName
                            |}

                    model, [ Effect.SendWire cmd ]
                | DeviceUserMsg.RevokeDevice deviceId ->
                    let cmd =
                        WireEvent.DeviceRevoke
                            {|
                                requestId = Guid.NewGuid()
                                deviceId = deviceId
                            |}

                    model, [ Effect.SendWire cmd ]

            | UserMsg.Onboarding onboardingMsg ->
                // 引导卡回话的三项在途事实：进模型，宿主再投影回卡片
                match onboardingMsg with
                | OnboardingUserMsg.SelectPreset key ->
                    { model with
                        Onboarding =
                            { model.Onboarding with
                                SelectedPresetKey = Some key
                            }
                    },
                    []
                | OnboardingUserMsg.Discovered models ->
                    { model with
                        Onboarding =
                            { model.Onboarding with
                                DiscoveredModels = models
                            }
                    },
                    []
                | OnboardingUserMsg.SwitchOffered flag ->
                    { model with
                        Onboarding =
                            { model.Onboarding with
                                SwitchOffered = flag
                            }
                    },
                    []

            | UserMsg.Chat chatMsg ->
                // 对话动作此前**没有分支**，全部落进下面的 `| _ -> model, []`：
                // 重试/重新生成、删除消息、复制回复、上下切换版本、加载更早一页、
                // 继续生成、分叉——这些用户可见的动作被静默丢弃。
                // 这里按动作本来的语义接线（与 MVU 重构前的宿主实现一一对应）。
                match chatMsg with
                | ChatUserMsg.JumpBeginning -> model, [ Effect.ScrollChat ScrollCommand.Beginning ]
                | ChatUserMsg.JumpEnd -> model, [ Effect.ScrollChat ScrollCommand.End ]
                | ChatUserMsg.Regenerate ->
                    // 重新生成：主机删掉尾部整段回复链再重跑。
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(activeConv, RunState.Idle _))) ->
                        let invId = Guid.NewGuid()

                        let cmd =
                            ClientCommand.RegenerateResponse
                                {|
                                    invocationId = invId
                                    conversationId = activeConv.ConversationId
                                |}

                        let pending =
                            {
                                InvocationId = invId
                                Command = cmd
                                SentAt = now
                                SuccessMessage = None
                            }

                        let nextFeedback =
                            { model.Feedback with
                                PendingCommands = Map.add invId pending model.Feedback.PendingCommands
                            }

                        { model with Feedback = nextFeedback }, [ Effect.SendWire(WireEvent.Command cmd) ]
                    | _ -> model, []
                | ChatUserMsg.ContinueReply ->
                    // 继续生成（决策 230）：把中途失败留下的半截助手回复落盘，再从它接着生成。
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(activeConv, RunState.Idle _))) ->
                        let invId = Guid.NewGuid()
                        let partialJson = JsonObject()
                        partialJson["role"] <- "assistant"
                        partialJson["text"] <- ""

                        let cmd =
                            ClientCommand.ContinueResponse
                                {|
                                    invocationId = invId
                                    conversationId = activeConv.ConversationId
                                    messageJson = partialJson :> JsonNode
                                |}

                        let pending =
                            {
                                InvocationId = invId
                                Command = cmd
                                SentAt = now
                                SuccessMessage = None
                            }

                        let nextFeedback =
                            { model.Feedback with
                                PendingCommands = Map.add invId pending model.Feedback.PendingCommands
                            }

                        { model with Feedback = nextFeedback }, [ Effect.SendWire(WireEvent.Command cmd) ]
                    | _ -> model, []
                | ChatUserMsg.DeleteExchange messageId ->
                    // 删除一条消息：messageId 是消息的 commit id（十进制串）。
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(activeConv, _))) ->
                        match UInt64.TryParse messageId with
                        | false, _ -> model, []
                        | true, commitId ->
                            let invId = Guid.NewGuid()

                            let cmd =
                                ClientCommand.DeleteMessage
                                    {|
                                        invocationId = invId
                                        conversationId = activeConv.ConversationId
                                        messageCommitId = commitId
                                    |}

                            let pending =
                                {
                                    InvocationId = invId
                                    Command = cmd
                                    SentAt = now
                                    SuccessMessage = None
                                }

                            let nextFeedback =
                                { model with
                                    Feedback =
                                        { model.Feedback with
                                            PendingCommands = Map.add invId pending model.Feedback.PendingCommands
                                        }
                                }

                            nextFeedback, [ Effect.SendWire(WireEvent.Command cmd) ]
                    | _ -> model, []
                | ChatUserMsg.EditFork(commitId, seedText) ->
                    // 编辑并分叉：从当前对话分出新对话，把被编辑的那条消息换成编辑后的文本。
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(activeConv, _))) ->
                        let newConvId = Guid.NewGuid()
                        let invId = Guid.NewGuid()
                        let editedMessage = JsonObject()
                        editedMessage["role"] <- "user"
                        editedMessage["text"] <- seedText

                        let cmd =
                            ClientCommand.ForkConversation
                                {|
                                    invocationId = invId
                                    conversationId = newConvId
                                    parentConversationId = activeConv.ConversationId
                                    forkAfterId = commitId
                                    config = SessionConfig.empty
                                    editedMessageJson = editedMessage :> JsonNode
                                |}

                        let pending =
                            {
                                InvocationId = invId
                                Command = cmd
                                SentAt = now
                                SuccessMessage = None
                            }

                        let nextFeedback =
                            { model with
                                Feedback =
                                    { model.Feedback with
                                        PendingCommands = Map.add invId pending model.Feedback.PendingCommands
                                    }
                            }

                        nextFeedback,
                        [
                            Effect.SendWire(WireEvent.Command cmd)
                            Effect.SendWire(WireEvent.ObserveConversation {| conversationId = newConvId |})
                        ]
                    | _ -> model, []
                | ChatUserMsg.SwitchVersion targetConvId ->
                    // 在同一消息位置的另一个版本（原文或某个分叉）之间切换。
                    match model.Conn with
                    | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, _)) ->
                        let nextCatalog =
                            CatalogPhase.Ready(cat, WorkspacePhase.LoadingConversation targetConvId)

                        { model with
                            Conn = ConnPhase.Authed(instId, nextCatalog)
                        },
                        [
                            Effect.SendWire(WireEvent.ObserveConversation {| conversationId = targetConvId |})
                        ]
                    | _ -> model, []
                | ChatUserMsg.ApprovalRespond(approvalId, approved) ->
                    // 工具审批：回应线协议的工具审批请求。
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, _))) ->
                        model,
                        [
                            Effect.SendWire(
                                WireEvent.ToolApprovalDecision
                                    {|
                                        approvalId = approvalId
                                        approved = approved
                                    |}
                            )
                        ]
                    | _ -> model, []
                | ChatUserMsg.CopyLatestReply ->
                    // 复制最新一条有正文的回复。
                    match model.Conn with
                    | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(activeConv, _))) ->
                        let latest =
                            activeConv.Messages
                            |> List.filter (fun m -> m.role = "assistant" && not (String.IsNullOrWhiteSpace m.text))
                            |> List.tryLast

                        match latest with
                        | Some m -> model, [ Effect.Clipboard m.text ]
                        | None -> model, []
                    | _ -> model, []
                | ChatUserMsg.OlderPageRequested ->
                    // 加载更早一页：向主机要「这条之前的一页」。
                    // 只有「还有更多」且「当前没在加载」时才发（与重构前的宿主实现同一口径）。
                    match model.Conn with
                    | ConnPhase.Authed(instId, CatalogPhase.Ready(cat, WorkspacePhase.Active(activeConv, run))) when
                        activeConv.PageHasMore && not activeConv.PageLoading
                        ->
                        let nextConv = { activeConv with PageLoading = true }
                        let nextCatalog = CatalogPhase.Ready(cat, WorkspacePhase.Active(nextConv, run))

                        { model with
                            Conn = ConnPhase.Authed(instId, nextCatalog)
                        },
                        [
                            Effect.SendWire(
                                WireEvent.HistoryRequest
                                    {|
                                        conversationId = activeConv.ConversationId
                                        beforeCommitId = activeConv.PageEarliest
                                        limit = 100
                                    |}
                            )
                        ]
                    | _ -> model, []

            | _ -> model, []

        | Internal internalMsg ->
            match internalMsg with
            | InternalMsg.RevealTargetExpired ->
                { model with
                    Chrome =
                        { model.Chrome with
                            RevealTarget = None
                            RevealTargetExpiresAt = None
                        }
                },
                []
            | InternalMsg.UploadProgress(attachmentId, percent) ->
                // 上传进度：按附件 id 找到那条草稿，更新百分比。找不到就忽略（上传早于草稿入树）。
                let update (a: ComposerAttachmentDraft) =
                    if a.Id = attachmentId then
                        { a with ProgressPercent = percent }
                    else
                        a

                { model with
                    Composer =
                        { model.Composer with
                            Attachments = model.Composer.Attachments |> List.map update
                        }
                },
                []
            | InternalMsg.UploadFinished attachmentId ->
                // 上传完成：这条附件进入「就绪」，可随消息发出。
                let update (a: ComposerAttachmentDraft) =
                    if a.Id = attachmentId then
                        { a with
                            ProgressPercent = 100
                            Ready = true
                            Failed = false
                        }
                    else
                        a

                { model with
                    Composer =
                        { model.Composer with
                            Attachments = model.Composer.Attachments |> List.map update
                        }
                },
                []
            | InternalMsg.UploadFailed(attachmentId, _) ->
                // 上传失败：这条附件标记为失败（Ready 保持 false，不随消息发出）。
                let update (a: ComposerAttachmentDraft) =
                    if a.Id = attachmentId then
                        { a with Failed = true; Ready = false }
                    else
                        a

                { model with
                    Composer =
                        { model.Composer with
                            Attachments = model.Composer.Attachments |> List.map update
                        }
                },
                []
            | _ -> model, []
