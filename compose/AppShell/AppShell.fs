namespace Wanxiang.UI

open System
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open System.Text.Json.Nodes
open Wanxiang2.MotionSwitch

/// 宿主 AppShell.fs 的编排核心（GAP3a）。
///
/// 本组合者只做状态编排与视图组装：认证步骤序列、连接/重连调度、视口轮询处理、
/// 协议事件界面面、对话操作、附件流程、导入导出、草稿策略。
/// 不持 DispatcherTimer、不做文件 IO、不做 Browser 调用、不直接构造 Avalonia 控件——
/// 计时、IO、浏览器与视图端口全部由入口壳经 ShellPorts 注入。
///
/// 命名空间沿用 Wanxiang.UI（与 ShellCommands、ProtocolDispatch、DeletionDesk、DraftSession 同模式）；
/// 类型名避开旧宿主 `MainView`（AGENTS.md 模块命名规则）。
[<RequireQualifiedAccess>]
type ShellToastTone =
    | Info
    | Success
    | Warning
    | Failure

/// 入口壳注入的端口。全部是函数/工厂，本组件不持有任何计时器、文件句柄或浏览器对象。
type ShellPorts =
    { /// 建立连接（入口壳经 WsClient.ConnectWithGenerationAsync）；返回连接代次 epoch
      Connect: unit -> Async<int>
      /// 在指定连接代次上发送（认证事件专用；入口壳经 WsClient.TrySendAtGenerationAsync 编码）
      SendAuth: int -> WireEvent -> unit
      /// 发回主机（字节由 WsClient 编码）
      Send: WireEvent -> unit
      /// 发送命令并跟踪回执（宿主经 WsClient 发送；失败路径也在宿主）
      SendCommand: ClientCommand -> unit
      /// 一次性计时（计时器归入口壳）；返回值可取消
      After: TimeSpan -> (unit -> unit) -> IDisposable
      /// 周期计时（计时器归入口壳）；返回值可取消
      Every: TimeSpan -> (unit -> unit) -> IDisposable
      /// 提示条
      Toast: string -> ShellToastTone -> unit
      /// 带「撤销」动作的提示条
      ToastUndo: string -> (unit -> unit) -> unit
      /// 剪贴板写（成功/失败提示也由入口壳处理）
      CopyToClipboard: string -> unit
      /// 系统浏览器打开链接
      OpenLink: string -> unit
      /// 文件保存（下载落点）
      SaveDownload: string -> byte[] -> unit
      /// 文件选择（附件上传落点）
      PickFiles: unit -> unit
      /// 拖入文件
      DropFiles: obj list -> unit
      /// 剪贴板粘贴（返回是否已处理）
      PasteFromClipboard: unit -> bool
      /// 本地草稿写落点（文件 / IndexedDB；DraftWrite 由 DraftSession 给出）
      ApplyDraftWrite: DraftWrite -> unit
      /// 偏好读写落点
      PrefsLoad: unit -> UiPrefs
      PrefsSave: UiPrefs -> unit
      /// 凭据读写落点（桌面 client.toml / PWA IndexedDB）
      TryLoadClientToml: unit -> (string * string) option
      TryLoadBrowserConnection: unit -> Async<(string * string * string) option>
      SaveBrowserConnection: string -> string -> string -> string -> unit
      /// 浏览器端口（PWA 才用；桌面给空实现）
      BrowserViewportWidth: unit -> float
      BrowserResumeEpoch: unit -> int
      BrowserPageUrl: unit -> string
      BrowserConversationSucceeded: unit -> unit
      BrowserAddHidden: (unit -> unit) -> unit
      /// 系统深色偏好
      SystemPrefersDark: unit -> bool
      /// 视图组装端口：入口壳用 ThemeManager/OverlayDesk 等活实例把各 Desk 建好并接线。
      /// 本组件只持有这些视图引用并驱动它们。
      BuildViews: unit -> ShellViews }

/// 视图组装结果：入口壳负责构造，本组件负责驱动。
and ShellViews =
    { Sidebar: Wanxiang2.SidebarDesk.SidebarDesk
      Chat: Wanxiang2.ChatScreen.ChatScreen
      Composer: Wanxiang2.ComposerDesk.ComposerDesk
      Settings: Wanxiang2.SettingsScreen.SettingsScreen
      Onboarding: Wanxiang2.SettingsScreen.OnboardingDesk
      Workspace: Wanxiang2.Workspace.Workspace
      Approval: Wanxiang2.ApprovalDesk.ApprovalDesk
      Palette: Wanxiang2.PaletteDesk.PaletteDesk
      ModelSwitch: Wanxiang2.ModelSwitchDesk.ModelSwitchDesk
      SettingsHostVisible: bool -> unit
      WorkspaceHostVisible: bool -> unit
      SettingsFocusInitial: unit -> unit
      SettingsShowSection: string option -> unit }

/// 组合者：旧宿主 AppShell 的编排核心。
type ShellComposer(ports: ShellPorts) =
    let mutable prefs = ports.PrefsLoad()
    let mutable catalog = Catalog.empty
    let mutable authenticated = false
    let mutable instanceId = ""
    let mutable activeConvId: Guid option = None
    let mutable hostFeatures: Set<string> option = None
    let mutable summaries: Wanxiang.UI.ConversationSummary list = []
    let mutable lineage: Map<Guid, ForkLineage> = Map.empty
    let mutable pendingReveal: (Guid * RevealTarget) option = None
    let mutable pendingLink: (Guid * uint64) option = None
    let mutable listReceived = false
    let mutable missingAttachments: Set<string> = Set.empty
    let mutable lastUrl = ""
    let mutable lastToken: string option = None
    let mutable hasSavedCredentials = false
    let mutable connectionError: string option = None
    let mutable connectionIssue: ConnectionIssue option = None
    let mutable pairingRequested = false
    let mutable pageLoading = false
    let mutable pageRequestedAt: DateTimeOffset option = None
    let mutable draftModelSelection: (string * string) option = None
    let mutable paletteRecent: string list = []
    let mutable views: ShellViews option = None
    let state = ClientState()
    let mutable deletion: DeletionDesk option = None
    let mutable protocolDispatch: ProtocolDispatch option = None
    let mutable shellCommands: ShellCommands option = None

    /// 旧 MotionLedger 时长（数值与 MotionSwitch 库的 Timing 初始值相同）
    let ms (t: Timing) = TimeSpan.FromMilliseconds (MotionTimings.initialMs t)

    let drafts = ComposerDrafts()
    let outbox = MessageOutbox()
    let draftSession = DraftSession()
    let downloads = DownloadBuffers()
    let search = SearchInFlight()
    let probes = PendingProbes()
    let runs = ConversationRuns()
    let commandFeedback = CommandFeedbackTracker()
    let configFeedback = ConfigFeedbackTracker()
    let doctorWait = BoundedWait(BoundedWaits.doctorLimit)
    let reconnect = ReconnectLoop(ReconnectBackoff.baseDelayMs, ReconnectBackoff.next)
    let resumeEpoch = ChangeWatch<int>(Some 0)
    let mutable lastPageLink = ""
    let mutable enteredSettingsForFirstProvider = false

    let v () =
        match views with
        | Some views -> views
        | None -> failwith "ShellComposer：视图尚未组装（先调 Build）"

    let toast text tone =
        let mapped =
            match tone with
            | ShellToastTone.Info -> ShellToastTone.Info
            | ShellToastTone.Success -> ShellToastTone.Success
            | ShellToastTone.Warning -> ShellToastTone.Warning
            | ShellToastTone.Failure -> ShellToastTone.Failure
        ports.Toast text mapped

    let send = ports.Send
    let newInvocation () = Guid.CreateVersion7()

    let attachmentDraft () = (drafts.Get(instanceId, activeConvId)).attachments

    // ---- 认证步骤序列（旧 ApplyHostEvent AuthAccepted）----

    /// 连接真的成功：切实例、重置、声明能力、请求目录、观察列表、恢复草稿、渲染。
    member private this.ApplyAuthAccepted(newInstanceId: string) =
        if newInstanceId <> "" && newInstanceId <> instanceId then
            (drafts.Get(instanceId, activeConvId)).text <- ""
            outbox.Disconnect instanceId
            state.Reset()
            runs.Clear()
            catalog <- Catalog.empty
            v().Settings.SetCatalog catalog
            v().Settings.Health.Reset()
            activeConvId <- None
            summaries <- []
            v().Sidebar.SetConversations []
            v().Composer.SetText((drafts.Get(newInstanceId, None)).text)
        authenticated <- true
        hostFeatures <- None
        send (ToolApprovalCapability {| supported = true |})
        hasSavedCredentials <- true
        connectionError <- None
        connectionIssue <- None
        instanceId <- newInstanceId
        reconnect.ResetDelay()
        pairingRequested <- false
        for s in summaries do runs.ClearError s.id
        activeConvId |> Option.iter runs.ClearError
        v().Sidebar.SetConnection(true, "已连接")
        v().Settings.SetConnection(instanceId, lastUrl)
        this.RestoreLocalDrafts()
        if v().Settings.Devices.IsShown then v().Settings.Devices.Refresh()
        send CatalogRequest
        this.ObserveConversationList()
        activeConvId |> Option.iter (fun convId -> send (ObserveConversation {| conversationId = convId |}))
        this.Render()

    // ---- 连接 / 重连调度（计时由入口壳注入）----

    member private this.Connect() =
        let _attempt = reconnect.Connect()
        connectionError <- None
        connectionIssue <- None
        v().Settings.Health.Disconnected()
        this.TearDownConnection()
        this.Render()
        let token = lastToken
        let usePairing = pairingRequested
        async {
            try
                let! epoch = ports.Connect()
                // ADR-001 方案 B：连接后第一个动作即认证。
                match token with
                | Some value -> ports.SendAuth epoch (AuthPresent {| token = value |})
                | None ->
                    if usePairing then ports.SendAuth epoch (PairingRequested {| clientName = Some "" |})
            with _ ->
                ()
        }
        |> Async.Start

    /// 网络恢复 / 回到前台：正在退避的重连立刻执行。
    member this.ResumeReconnectNow() =
        if reconnect.ResumeNow() then
            pairingRequested <- false
            this.Connect()

    /// 断线收尾公共序列。
    member private this.TearDownConnection() =
        authenticated <- false
        runs.Disconnect()
        this.FailOlderPage "连接已断开，更早的消息没有加载。重新连接后点「重试」。"
        outbox.Disconnect instanceId
        commandFeedback.RejectAll()
        configFeedback.RejectAll()
        downloads.Clear()
        if views.IsSome then v().Settings.Devices.NotifyDisconnected()

    // ---- 视口轮询处理（轮询循环由入口壳驱动）----

    /// 浏览器视口轮询的每一拍：喂视口宽、查恢复计数、查页面链接。
    member this.TickViewport() =
        let width = ports.BrowserViewportWidth()
        if width > 0.0 then v().Workspace.ObserveViewport width
        let epoch = ports.BrowserResumeEpoch()
        if resumeEpoch.Observe epoch then this.ResumeReconnectNow()
        this.CheckPageLink()

    /// PWA：页面地址带 `#open?…` 时打开。
    member private this.CheckPageLink() =
        let href = ports.BrowserPageUrl()
        if href <> lastPageLink then
            lastPageLink <- href
            MessageLink.tryParse href
            |> Option.iter (fun link -> this.OpenMessageLink link)

    // ---- 协议事件界面面（模型部分归 ProtocolDispatch，界面面在这里）----

    /// ProtocolDispatch 做完模型部分后交回的界面面（旧 ApplyHostEvent）。
    member this.ApplyHostEvent(ev: WireEvent) =
        // 模型部分归 ProtocolDispatch；界面面在这里。
        this.Dispatch.Handle ev
        match ev with
        | AuthAccepted d -> this.ApplyAuthAccepted(d.instanceId)
        | AuthRejected _ ->
            authenticated <- false
            lastToken <- None
            hasSavedCredentials <- false
            v().Sidebar.SetConnection(false, "未认证")
            this.ReportConnectionIssue ConnectionIssue.TokenInvalid
            this.Render()
        | PairingStarted _ ->
            connectionError <- None
            connectionIssue <- None
        | PairingSucceeded d ->
            lastToken <- Some d.token
            hasSavedCredentials <- true
            connectionError <- None
            connectionIssue <- None
            send (AuthPresent {| token = d.token |})
        | PairingFailed d ->
            this.ReportConnectionIssue(ConnectionIssue.ofPairingFailure d.frozen d.freezeMinutes d.reason)
            this.Render()
        | CatalogSnapshot d ->
            catalog <- Catalog.parse d.providers d.tools d.generation
            v().Settings.SetCatalog catalog
            this.SyncPresetSurfaces()
            if enteredSettingsForFirstProvider && Catalog.isReady catalog then
                enteredSettingsForFirstProvider <- false
                this.CloseSettings()
                v().Composer.Focus()
            this.Render()
            this.CompleteOnboarding()
        | ConfigChanged _ ->
            match ConfigIssueNotice.recovered (v().Settings.Health.ClearConfigIssue()) with
            | Some text -> toast text (ShellToastTone.Success)
            | None -> ()
            if v().Settings.Devices.IsShown then send DevicesRequest
        | HostConfigRejected d ->
            let notice = ConfigIssueNotice.rejected (v().Settings.Health.ConfigIssue.IsSome) d.summary
            v().Settings.Health.SetConfigIssue(d.summary, d.errors)
            notice |> Option.iter (fun text -> toast text (ShellToastTone.Warning))
        | DoctorReport d ->
            doctorWait.Settle() |> ignore
            v().Settings.Health.SetReport(d.ok, DoctorCheck.parseChecks d.checks)
        | DevicesSnapshot d -> v().Settings.Devices.SetDevices(DevicesModel.parse d.items, d.canInvite)
        | PairingInviteIssued d -> v().Settings.Devices.SetInvite(d.code, d.expiresAtUtc, DateTimeOffset.UtcNow)
        | PairingInviteRejected d -> v().Settings.Devices.SetInviteRejected d.reason
        | ToolApprovalRequested _
        | ToolApprovalResolved _ -> v().Approval.Handle ev |> ignore
        | ProviderProbeResult d ->
            let models = ProbeModels.ofJson d.models
            if not (v().Onboarding.ApplyProbe(d.providerId, d.ok, models, d.error)) then
                v().Settings.ApplyProbe(d.providerId, d.ok, models, d.error)
        | ConversationListSnapshot d ->
            summaries <- ConversationSummary.parseList d.items
            lineage <- ForkLineage.parse d.items
            v().Sidebar.SetLineage lineage
            v().Sidebar.SetConversations(PendingDeletion.visible summaries (deletion |> Option.map _.Pending |> Option.defaultValue PendingDeletion.empty))
            v().Sidebar.SetActive activeConvId
            this.PruneDeletedConversationDrafts()
            listReceived <- true
            this.Render()
            pendingLink |> Option.iter this.OpenMessageLink
        | ConversationSnapshot d ->
            if activeConvId = Some d.conversationId then
                pageLoading <- false
                this.Render()
        | HistoryPage d ->
            if activeConvId = Some d.conversationId then
                pageLoading <- false
                pageRequestedAt <- None
                v().Chat.Pane.HideOlderFailure()
                this.Render()
        | SearchResults d ->
            v().Sidebar.ApplySearchResults(
                { queryId = d.queryId; query = d.query; hits = SearchHit.parse d.items; hasMore = d.hasMore; truncated = d.truncated },
                d.offset)
        | SearchFailed d -> v().Sidebar.ApplySearchFailure(v().Sidebar.SearchQueryText, d.message)
        | ConversationExportPage _ -> ()
        | ConversationExportFailed _ -> ()
        | GenerationFinished d ->
            if d.status = "completed" then ports.BrowserConversationSucceeded()
        | HostCapabilities d ->
            hostFeatures <- Some(Set.ofList d.features)
            this.SyncPresetSurfaces()
        | AttachmentCommitted d ->
            match drafts.All |> Seq.tryPick (fun draft -> draft.attachments.Complete(d.attachmentId, d.size)) with
            | Some(_, stillDrafted, key) when stillDrafted && key = (instanceId, activeConvId) ->
                v().Composer.SetAttachments((attachmentDraft ()).Items)
            | _ -> ()
        | AttachmentAborted d ->
            match drafts.All |> Seq.tryPick (fun draft -> draft.attachments.Abort d.attachmentId) with
            | Some(upload, _, _) ->
                v().Composer.SetAttachments((attachmentDraft ()).Items)
                toast (UploadFacts.uploadFailedText upload.fileName) (ShellToastTone.Warning)
            | None -> ()
        | _ -> ()

    // ---- 对话操作编排 ----

    member private this.Render() =
        // 视图驱动：把状态推给各 Desk（旧 AppShell.Render 的编排）。
        let run = runs.Get activeConvId
        let generating = run.running
        v().Sidebar.SetServerUrl lastUrl
        v().Approval.SetConversation activeConvId
        let startReady =
            StartReady.isReady authenticated (Catalog.isReady catalog) generating run.error.IsSome
                (match activeConvId, this.ActiveConversation() with
                 | None, _ -> ActiveConversation.None
                 | Some _, Some view -> ActiveConversation.Loaded view.messages.Count
                 | Some _, None -> ActiveConversation.Loading)
        v().Workspace.SetStarting startReady
        v().Composer.SetContext(this.ActiveConversation() |> Option.map (fun _ -> "会话上下文", ""), authenticated)
        v().Composer.SetGenerating generating
        v().Composer.SetCanStop(authenticated && run.generationId.IsSome)
        v().Composer.SetAttachments((attachmentDraft ()).Items)
        v().Chat.SetGenerating(generating, (if generating then "生成中" else ""))
        v().Chat.SetCanStop(authenticated && run.generationId.IsSome)
        match activeConvId, this.ActiveConversation() with
        | None, _ ->
            v().Chat.SetConversationChrome false
            v().Chat.SetBreadcrumb None
            v().Chat.SetTitle("新对话", false)
            v().Composer.SetEnabled(false, IdleSurface.LoadingReason)
        | Some _, None ->
            v().Chat.SetConversationChrome true
            v().Composer.SetEnabled(false, IdleSurface.LoadingReason)
        | Some convId, Some view ->
            v().Chat.SetConversationChrome true
            v().Chat.SetBreadcrumb(ForkLineage.breadcrumb summaries lineage convId)
            let summary = this.ActiveSummary()
            v().Chat.SetTitle((summary |> Option.map (fun s -> s.title) |> Option.defaultValue "对话"), true)
            v().Chat.Render
                { Messages = this.MessagesOf view
                  Streaming = (runs.Get activeConvId).message
                  Error = None
                  FontSize = prefs.fontScale
                  AutoCollapseReasoning = prefs.autoCollapseReasoning
                  Usage = run.usage
                  MissingAttachments = missingAttachments }
            this.TryReveal(convId, view, this.MessagesOf view)

    member private this.ActiveConversation() =
        activeConvId |> Option.bind (fun id -> state.Conversations.TryFind id)

    member private this.ActiveSummary() : Wanxiang.UI.ConversationSummary option =
        activeConvId |> Option.bind (fun id -> summaries |> List.tryFind (fun s -> s.id = id))

    member private this.MessagesOf(view: ConversationView) =
        view.messages |> Seq.cast<JsonNode> |> Seq.map MessageView.ofSnapshotItem |> List.ofSeq

    member private this.ObserveConversationList() =
        v().Sidebar.SetConversationListLoading true
        send ObserveConversationList

    /// ProtocolDispatch（模型部分）：视图组装后惰性构造。
    member private this.Dispatch: ProtocolDispatch =
        match protocolDispatch with
        | Some d -> d
        | None ->
            let d =
                ProtocolDispatch(
                    { State = state; Runs = runs; Outbox = outbox; Commands = commandFeedback
                      Configs = configFeedback; Downloads = downloads; Search = search; Probes = probes },
                    { ActiveConversation = fun () -> activeConvId
                      InstanceId = fun () -> instanceId
                      Send = send
                      Render = fun () -> this.Render()
                      Toast = fun text tone ->
                          ports.Toast text (match tone with
                                            | DispatchTone.Info -> ShellToastTone.Info
                                            | DispatchTone.Success -> ShellToastTone.Success
                                            | DispatchTone.Warning -> ShellToastTone.Warning
                                            | DispatchTone.Failure -> ShellToastTone.Failure)
                      ObserveList = fun () -> this.ObserveConversationList()
                      DispatchPending = fun item -> this.DispatchPendingMessage item
                      Downloaded = fun name bytes -> ports.SaveDownload name bytes
                      MissingAttachment = fun sha -> missingAttachments <- missingAttachments.Add sha
                      Apply = fun ev -> this.ApplyHostEvent ev })
            protocolDispatch <- Some d
            d

    member private this.SelectConversation(id: Guid option) =
        if activeConvId <> id then
            (drafts.Get(instanceId, activeConvId)).text <- v().Composer.Text
            activeConvId <- id
            v().Composer.SetText((drafts.Get(instanceId, id)).text)
            v().Composer.SetAttachments((attachmentDraft ()).Items)
            v().Chat.CancelHistoryPrependAnchor()
            v().Chat.ResetScrollState()
            pageLoading <- false
            pageRequestedAt <- None
        v().Sidebar.SetActive activeConvId
        this.Render()

    member private this.OpenConversation(id: Guid) =
        this.SelectConversation(Some id)
        send (ObserveConversation {| conversationId = id |})
        v().Composer.Focus()

    member private this.CreateConversation() : Guid option =
        if not authenticated then
            toast "连接主机后才能新建对话；草稿会保留。" (ShellToastTone.Warning)
            None
        elif not (Catalog.isReady catalog) then
            toast "先添加一个模型服务。" (ShellToastTone.Warning)
            this.ShowAddProvider()
            None
        else
            let conversationId = Guid.CreateVersion7()
            draftModelSelection <- None
            this.SelectConversation(Some conversationId)
            let command =
                CreateConversation
                    {| invocationId = newInvocation (); conversationId = conversationId; title = "新对话"; config = this.MakeConfig() |}
            ports.SendCommand command
            send (ObserveConversation {| conversationId = conversationId |})
            v().Composer.Focus()
            Some conversationId

    member private this.MakeConfig() =
        let provider, model =
            draftModelSelection |> Option.orElse (Catalog.defaultSelection catalog) |> Option.defaultValue ("", "")
        { SessionConfig.empty with
            provider = provider; model = model
            instructions = catalog.generation.instructions
            temperature = catalog.generation.temperature
            topP = catalog.generation.topP
            maxTokens = catalog.generation.maxTokens }

    member private this.SendMessage(text: string) : bool =
        let draft = attachmentDraft ()
        if draft.HasFailed then
            toast "有上传失败的附件，请先移除再发送。" (ShellToastTone.Warning)
            false
        elif draft.HasUploading then
            toast "附件上传中，请等待上传完成后再发送。" (ShellToastTone.Warning)
            false
        elif not authenticated then
            toast "连接已断开，草稿已保留。" (ShellToastTone.Warning)
            false
        elif String.IsNullOrWhiteSpace text && not draft.HasReady then false
        elif activeConvId.IsNone && not (Catalog.isReady catalog) then
            toast "还没有可用的模型，先添加一个模型服务。" (ShellToastTone.Warning)
            false
        else
            let isNew = activeConvId.IsNone
            let id = activeConvId |> Option.defaultWith Guid.CreateVersion7
            let creation =
                if isNew then
                    Some(CreateConversation
                            {| invocationId = newInvocation (); conversationId = id; title = "新对话"; config = this.MakeConfig() |})
                else None
            let item = outbox.Stage(instanceId, id, text, draft.Items, creation)
            draft.TryConsumeReady() |> ignore
            if isNew then
                this.SelectConversation(Some id)
                drafts.Move(instanceId, None, Some id)
                v().Composer.SetText text
            runs.ClearError id
            this.DispatchPendingMessage item
            this.Render()
            true

    member private this.DispatchPendingMessage(item: PendingMessage) =
        let id = PendingMessage.invocationId item
        if item.instanceId <> instanceId || not authenticated then
            outbox.SetState(id, UnconfirmedMessage "请连接原主机后重试。")
        else
            match item.creation with
            | Some creation when not item.creationConfirmed ->
                outbox.SetState(id, PreparingConversation)
                ports.SendCommand creation
                outbox.ConversationReady(item.instanceId, item.conversationId)
                send (ObserveConversation {| conversationId = item.conversationId |})
            | _ when not (state.Conversations.ContainsKey item.conversationId) ->
                outbox.SetState(id, PreparingConversation)
                send (ObserveConversation {| conversationId = item.conversationId |})
            | _ ->
                outbox.SetState(id, SendingMessage)
                ports.SendCommand item.command

    member private this.StopGeneration() =
        match activeConvId, (runs.Get activeConvId).generationId with
        | Some convId, Some generationId ->
            send (GenerationCancel {| conversationId = convId; generationId = generationId |})
            v().Composer.Focus()
        | _ -> ()

    member private this.Regenerate() =
        match activeConvId with
        | None -> ()
        | Some convId ->
            runs.ClearError convId
            ports.SendCommand (RegenerateResponse {| invocationId = newInvocation (); conversationId = convId |})
            this.Render()
            v().Composer.Focus()

    member private this.ForkFrom(commitId: uint64 option, seedText: string) =
        match activeConvId, this.ActiveConversation() with
        | Some convId, Some view ->
            let forkAfterId = ForkAnchor.forkAfter (this.MessagesOf view |> List.choose (fun m -> m.commitId)) commitId
            let editedMessage = ForkAnchor.editedMessage seedText
            let newId = Guid.CreateVersion7()
            let command =
                ForkConversation
                    {| invocationId = newInvocation ()
                       conversationId = newId
                       parentConversationId = convId
                       forkAfterId = forkAfterId
                       config = SessionConfig.empty
                       editedMessageJson = editedMessage |}
            ports.SendCommand command
            this.SelectConversation(Some newId)
            send (ObserveConversation {| conversationId = newId |})
            v().Composer.Focus()
        | _ -> toast ForkAnchor.noConversationText (ShellToastTone.Warning)

    // ---- 附件流程 ----

    member private this.BeginUpload(owner: AttachmentDraftController, fileName: string, bytes: byte[]) =
        match MediaTypes.validate fileName (int64 bytes.Length) with
        | Error message -> toast message (ShellToastTone.Warning)
        | Ok() ->
            let sha = UploadFacts.sha256Hex bytes
            let mediaType = MediaTypes.ofFileName fileName
            let attachmentId = Guid.CreateVersion7()
            // TODO(ShellComposer)：图片解码归入口壳（SetThumbnail 需要 IImage）；当前不设缩略图。
            if MediaTypes.isModelImage mediaType then ()
            let upload =
                { attachmentId = attachmentId; fileName = fileName; mediaType = mediaType
                  size = int64 bytes.Length; sha256 = sha }
            owner.Begin upload |> ignore
            this.Render()
            send (AttachmentBegin
                    {| attachmentId = attachmentId; totalBytes = int64 bytes.Length; sha256 = sha
                       mediaType = mediaType; fileName = fileName |})
            for index, dataBase64 in UploadFacts.chunks bytes do
                send (AttachmentChunk {| attachmentId = attachmentId; index = index; dataBase64 = dataBase64 |})
            send (AttachmentComplete {| attachmentId = attachmentId; sha256 = sha |})

    member private this.ImportConversation() =
        if not authenticated then toast "连接主机后才能导入对话。" (ShellToastTone.Warning)
        else
            let conversationId = Guid.CreateVersion7()
            let config = this.MakeConfig()
            let cmd =
                ImportConversation
                    {| invocationId = newInvocation ()
                       conversationId = conversationId
                       title = "导入的对话"
                       config = config
                       messages = System.Text.Json.Nodes.JsonArray() |}
            ports.SendCommand cmd
            this.OpenConversation conversationId
            send (ObserveConversation {| conversationId = conversationId |})

    member private this.ExportConversation(_summary: Wanxiang.UI.ConversationSummary) =
        if not authenticated then toast "连接主机后才能读取完整历史。" (ShellToastTone.Warning)
        else ()

    // ---- 草稿策略 ----

    member private this.SyncActiveDraftText() =
        if instanceId <> "" then
            (drafts.Get(instanceId, activeConvId)).text <- v().Composer.Text

    member private this.LiveDraftSnapshot() : DraftSnapshot =
        this.SyncActiveDraftText()
        DraftPolicy.snapshotOf drafts outbox (fun id -> draftSession.SavedAt(id, DateTimeOffset.UtcNow))

    member private this.CurrentDraftSnapshot() : DraftSnapshot =
        draftSession.ForSave(this.LiveDraftSnapshot())

    /// 保存到本机。计时由入口壳驱动；内容没变不写。
    member this.FlushDrafts() =
        if draftSession.IsLoaded && prefs.persistDrafts then
            let write = draftSession.Flush(prefs.persistDrafts, this.LiveDraftSnapshot())
            ports.ApplyDraftWrite write

    member private this.RestoreLocalDrafts() =
        match draftSession.Restore(prefs.persistDrafts, instanceId) with
        | Some stored ->
            let unsentBefore = outbox.ForInstance instanceId |> List.length
            let restoredCount: int = DraftPolicy.restoreInto stored instanceId drafts outbox
            ignore restoredCount
            let unsentRestored = (outbox.ForInstance instanceId |> List.length) - unsentBefore
            let notice = draftSession.Restored(instanceId, unsentRestored)
            let live = drafts.Get(instanceId, activeConvId)
            if DraftRestore.fillComposer (v().Composer.Text) live.text then v().Composer.SetText live.text
            v().Composer.SetAttachments((attachmentDraft ()).Items)
            notice |> Option.iter (fun message -> toast message (ShellToastTone.Info))
        | None -> ()

    member private this.PruneDeletedConversationDrafts() =
        if instanceId <> "" then
            let listed = summaries |> List.map _.id |> Set.ofList
            let inFlight =
                outbox.ForInstance instanceId
                |> List.filter (fun item -> not (PendingMessage.canRetry item))
                |> List.map _.conversationId
                |> Set.ofList
            let gone = draftSession.Prune(instanceId, listed, inFlight)
            for id in gone do
                if activeConvId <> Some id then drafts.Remove(instanceId, Some id)
                outbox.DiscardConversation(instanceId, id)

    member private this.ClearLocalDrafts() =
        for (instance, conversation), draft in drafts.Entries |> Seq.toList do
            draft.text <- ""
            for item in draft.attachments.Items |> Seq.toList do
                draft.attachments.Remove item.attachmentId |> ignore
            drafts.Remove(instance, conversation)
        for instance in outbox.AllInstances() do outbox.DiscardUnsent instance
        let cleared = draftSession.Clear()
        v().Composer.SetText ""
        v().Composer.SetAttachments []
        ports.ApplyDraftWrite DraftWrite.Delete
        toast cleared (ShellToastTone.Success)

    // ---- 设置 / 首次运行 ----

    member private this.ShowSettings() =
        v().SettingsHostVisible true
        v().WorkspaceHostVisible false
        v().SettingsFocusInitial()
        send CatalogRequest

    member private this.CloseSettings() =
        enteredSettingsForFirstProvider <- false
        v().SettingsHostVisible false
        v().WorkspaceHostVisible true

    member private this.ShowAddProvider() =
        enteredSettingsForFirstProvider <- not (Catalog.isReady catalog)
        this.ShowSettings()
        v().SettingsShowSection None

    member private this.CompleteOnboarding() =
        match v().Onboarding.TakeConnected catalog with
        | Some(providerId, model) ->
            v().Onboarding.Reset()
            draftModelSelection <- Some(providerId, model)
            this.Render()
            if String.IsNullOrWhiteSpace (v().Composer.Text) then v().Composer.SetText OnboardingRules.FirstPrompt
            v().Composer.Focus()
            toast (sprintf "已连接 %s" (Catalog.describeModel providerId model catalog)) (ShellToastTone.Success)
        | None -> ()

    member private this.SyncPresetSurfaces() =
        let entries =
            catalog.presets
            |> List.map (fun p -> p.name, Presets.describe catalog p, (fun () -> ()))
        v().Sidebar.SetPresets(entries, fun _ _ -> ())
        v().Chat.Pane.SetPresets entries

    member private this.ReportConnectionIssue(issue: ConnectionIssue) =
        connectionIssue <- Some issue
        connectionError <- Some(ConnectIssueText.describe issue)

    member private this.ShowSessionContext() =
        match activeConvId, this.ActiveConversation() with
        | Some _, Some _ when authenticated -> ()
        | _ -> toast "连接并选择对话后，可以调整上下文。" (ShellToastTone.Warning)

    member private this.OpenMessageLink((convId: Guid, commitId: uint64)) =
        if not authenticated || List.isEmpty summaries && not listReceived then
            pendingLink <- Some(convId, commitId)
        else
            pendingLink <- None
            if summaries |> List.exists (fun s -> s.id = convId) then
                pendingReveal <- Some(convId, RevealMessage commitId)
                this.OpenConversation convId
            else
                toast "链接指向的对话不存在或已删除。" (ShellToastTone.Warning)

    member private this.TryReveal(convId: Guid, _view: ConversationView, messages: MessageView list) =
        match pendingReveal with
        | Some(target, reveal) when target = convId ->
            let loaded = messages |> List.choose _.commitId
            match RevealTarget.resolve reveal loaded with
            | Some commit ->
                pendingReveal <- None
                match reveal with
                | RevealMessage _ -> v().Chat.RevealAndHighlight commit |> ignore
                | RevealAfter _ -> v().Chat.ScrollToMessage commit |> ignore
            | None -> ()
        | _ -> ()

    member private this.FailOlderPage(message: string) =
        if OlderPageLoad.inFlight pageLoading pageRequestedAt then
            pageLoading <- false
            pageRequestedAt <- None
            v().Chat.CancelHistoryPrependAnchor()
            v().Chat.Pane.ShowOlderFailure(message, fun () ->
                if authenticated then this.RequestOlderHistory())

    member private this.RequestOlderHistory() =
        match activeConvId, this.ActiveConversation() with
        | Some convId, Some view when view.pageHasMore && not pageLoading ->
            pageLoading <- true
            pageRequestedAt <- Some DateTimeOffset.UtcNow
            send (HistoryRequest
                    {| conversationId = convId; beforeCommitId = view.pageEarliest; limit = 100 |})
        | _ -> ()

    member private this.CheckOlderPageTimeout(now: DateTimeOffset) =
        if OlderPageLoad.timedOut pageLoading pageRequestedAt now (ms Timing.OlderPageTimeout) then
            this.FailOlderPage OlderPageLoad.timeoutText

    // ---- 生命周期 / 组装 ----

    /// 入口壳在视图树挂载后调用：组装视图、接事件、启动周期计时。
    /// 返回的 IDisposable 在卸载时调用（停计时、落草稿）。
    member this.Build() : IDisposable =
        views <- Some(ports.BuildViews())
        // 删除协作：视图组装后构造（surface 引用视图）。
        deletion <-
            Some(DeletionDesk(commandFeedback,
                { Now = fun () -> DateTimeOffset.UtcNow
                  ActiveConversation = fun () -> activeConvId
                  TitleOf = fun id -> summaries |> List.tryFind (fun summary -> summary.id = id) |> Option.map _.title
                  VisibleOrder = fun () -> v().Sidebar.GetVisibleOrder()
                  PendingChanged = fun pending -> v().Sidebar.SetConversations(PendingDeletion.visible summaries pending)
                  FocusAfterDelete = fun id -> v().Sidebar.FocusAfterDelete(string id)
                  Open = fun id ->
                      this.SelectConversation id
                      v().Sidebar.SetActive activeConvId
                  Render = fun () -> this.Render()
                  Toast = fun text tone ->
                      ports.Toast text (match tone with
                                        | DeletionTone.Info -> ShellToastTone.Info
                                        | DeletionTone.Success -> ShellToastTone.Success
                                        | DeletionTone.Warning -> ShellToastTone.Warning)
                  ToastUndo = fun label undo -> ports.ToastUndo label undo
                  After = ports.After
                  SendCommand = ports.SendCommand
                  NewInvocation = newInvocation }))
        this.Render()
        // 周期计时：投递确认 / 请求超时 / 草稿自动保存 / 更早一页看门狗 / 视口轮询。
        let delivery = ports.Every (ms Timing.OutboxDeliveryCheckTick) (fun () ->
            let now = DateTimeOffset.UtcNow
            let expired = this.ExpireHostRequests now
            if outbox.Expire(now, (TimeSpan.FromSeconds 30.0)) || expired then this.Render())
        let draftsTimer = ports.Every (TimeSpan.FromMilliseconds 800.0) (fun () -> this.FlushDrafts())
        let viewport = ports.Every (ms Timing.HostViewportPoll) (fun () -> this.TickViewport())
        let watchdog = ports.Every (ms Timing.OlderPageCheckTick) (fun () ->
            this.CheckOlderPageTimeout DateTimeOffset.UtcNow)
        ports.BrowserAddHidden(fun () -> this.FlushDrafts())
        { new IDisposable with
            member _.Dispose() =
                this.FlushDrafts()
                delivery.Dispose()
                draftsTimer.Dispose()
                viewport.Dispose()
                watchdog.Dispose() }

    member private this.ExpireHostRequests(now: DateTimeOffset) : bool =
        let timeout = (ms Timing.HostRequestTimeout)
        let message = HostFeatures.noResponseMessage hostFeatures
        let expiredConfig = configFeedback.Expire(now, timeout)
        let expiredCommands = commandFeedback.Expire(now, timeout)
        for f in expiredConfig do f.onCompleted false
        for f in expiredCommands do f.onCompleted false
        if not (List.isEmpty expiredConfig && List.isEmpty expiredCommands) then toast message (ShellToastTone.Warning)
        if search.Expire(now, timeout) then v().Sidebar.ApplySearchFailure(v().Sidebar.SearchQueryText, message)
        for (_, pid) in probes.Expire(now, timeout) do
            let err = Some message
            if not (v().Onboarding.ApplyProbe(pid, false, [], err)) then v().Settings.ApplyProbe(pid, false, [], err)
        not (List.isEmpty expiredCommands)

    // ---- 快捷键 / 命令面板（转发给 ShellCommands 组合者）----

    member this.Commands: ShellCommands =
        match shellCommands with
        | Some c -> c
        | None ->
            let c =
                ShellCommands(
                    { OverlayEscape = fun () -> false
                      IsDialogOpen = fun () -> false
                      IsPopupOpen = fun () -> false
                      IsSettingsOpen = fun () -> false
                      IsDrawerOpen = fun () -> false
                      IsGenerating = fun () -> (runs.Get activeConvId).generationId.IsSome
                      HasConversation = fun () -> (this.ActiveSummary()).IsSome
                      VisibleOrder = fun () -> v().Sidebar.GetVisibleOrder()
                      HasPreset = fun id -> catalog.presets |> List.exists (fun p -> p.id = id)
                      PaletteInput = fun () -> this.PaletteInput()
                      Theme = fun () -> prefs.theme
                      CloseSettings = fun () -> this.CloseSettings()
                      CloseDrawer = fun () -> ()
                      StopGeneration = fun () -> this.StopGeneration()
                      ToggleSidebar = fun () -> this.ToggleSidebar()
                      CloseWindow = fun () -> false
                      NewConversation = fun () -> this.CreateConversation() |> ignore
                      FocusSearch = fun _ -> ()
                      OpenSettings = fun _ -> this.ShowSettings()
                      OpenModelPicker = fun () -> ()
                      JumpExchange = fun _ -> ()
                      ScrollToBeginning = fun () -> ()
                      ScrollToEnd = fun () -> ()
                      FocusComposer = fun () -> v().Composer.Focus()
                      CopyLastReply = fun () -> ()
                      RegenerateLast = fun () -> ()
                      TryUndo = fun () -> false
                      FocusToast = fun () -> false
                      ExportConversation = fun () -> this.ActiveSummary() |> Option.iter this.ExportConversation
                      OpenConversation = fun id -> this.OpenConversation id
                      ShowShortcuts = fun () -> ()
                      ApplyPreset = fun _ -> ()
                      SavePreset = fun () -> ()
                      Fork = fun () -> ()
                      Import = fun () -> this.ImportConversation()
                      AddProvider = fun () -> this.ShowAddProvider()
                      ShowPalette = fun items recent -> v().Palette.Show(items, recent)
                      SaveRecent = fun recent -> paletteRecent <- recent
                      SaveTheme = fun _ -> ()
                      Toast = fun text tone ->
                          let mapped =
                              match tone with
                              | Wanxiang.UI.ShellTone.Info -> ShellToastTone.Info
                              | Wanxiang.UI.ShellTone.Warning -> ShellToastTone.Warning
                          ports.Toast text mapped },
                    paletteRecent)
            shellCommands <- Some c
            c

    member private this.PaletteInput() : ShellPaletteInput =
        { hasConversation = activeConvId.IsSome
          presets = [ for p in catalog.presets -> p.id, p.name, Presets.describe catalog p ]
          supportsPresets = HostFeatures.supports hostFeatures HostFeatures.Presets
          supportsImport = HostFeatures.supports hostFeatures HostFeatures.ConversationImport
          themeLabel = ThemePreference.label prefs.theme
          sections = [ for s in Wanxiang2.SettingsScreen.SettingsPart.all -> string s, Wanxiang2.SettingsScreen.SettingsPart.label s, Wanxiang2.SettingsScreen.SettingsPart.scope s ]
          conversations = summaries
          showArchived = prefs.showArchived }

    member this.HandleShortcut(shortcut: ShortcutAction) : bool =
        this.Commands.HandleShortcut shortcut

    member this.ShowCommandPalette() = this.Commands.ShowPalette()
    member this.ToggleSidebar() =
        let state = v().Workspace.ToggleSidebar()
        if not state.compactMode then
            prefs <- { prefs with sidebarCollapsed = state.sidebarCollapsed }
            ports.PrefsSave prefs

    // ---- 自动连接 ----

    /// 已有凭据时自动连接（入口壳在 Build 后调用）。
    member this.AutoConnect() =
        match ports.TryLoadClientToml() with
        | Some(url, token) ->
            hasSavedCredentials <- true
            lastUrl <- url
            lastToken <- Some token
            this.Connect()
        | None ->
            async {
                let! stored = ports.TryLoadBrowserConnection()
                // 凭据 IO（文件 / IndexedDB）在后台完成，Async.Start 的续体跑在线程池线程上；
                // 以下凭据落地与视图刷新触碰 Avalonia 控件，必须 marshal 回 UI 线程——
                // 与入口壳 EventReceived → Dispatcher.UIThread.Post 同一约定。
                Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                    match stored with
                    | Some(url, token, _) ->
                        hasSavedCredentials <- true
                        lastUrl <- url
                        lastToken <- Some token
                        this.Connect()
                    | None ->
                        hasSavedCredentials <- false
                        this.Render())
            }
            |> Async.Start

    /// 本地草稿读完（入口壳把落点内容喂进来）。
    member this.LoadLocalDrafts(text: string option) =
        draftSession.Loaded text
        this.RestoreLocalDrafts()
