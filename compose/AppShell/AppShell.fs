namespace Wanxiang.UI

open System
open System.Threading.Tasks
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open System.Text.Json.Nodes
open Avalonia.Controls
open Avalonia.Platform.Storage
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
    { /// 建立连接（入口壳经 WsClient.ConnectWithGenerationAsync）；参数是主机地址，返回连接代次 epoch
      Connect: string -> Async<int>
      /// 在指定连接代次上发送（认证事件专用；入口壳经 WsClient.TrySendAtGenerationAsync 编码）
      SendAuth: int -> WireEvent -> unit
      /// 发回主机（字节由 WsClient 编码）
      Send: WireEvent -> unit
      /// 发送命令（入口壳经 WsClient.TrySendCommandAsync）；返回是否真的发出，失败路径在组合者
      SendCommand: ClientCommand -> Task<bool>
      /// 按当前连接代次发送一条协议事件并返回是否真的发出（导出分页读取用）
      SendAtGeneration: WireEvent -> Task<bool>
      /// 一次性计时（计时器归入口壳）；返回值可取消
      After: TimeSpan -> (unit -> unit) -> IDisposable
      /// 周期计时（计时器归入口壳）；返回值可取消
      Every: TimeSpan -> (unit -> unit) -> IDisposable
      /// 把回调投递到 UI 线程（后台续体不直接碰控件）
      Post: (unit -> unit) -> unit
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
      /// 未保存凭据时的默认主机地址（桌面 loopback / 浏览器同源）
      DefaultServerUrl: unit -> string
      /// 主题：dark=true → Dark
      ApplyTheme: bool -> unit
      /// 减弱动效开关
      SetReducedMotion: bool -> unit
      /// 代码折行偏好（入口把它转交 MessageCards 用的 CodeWrapMemory 实例）
      SetCodeWrap: bool -> unit
      /// 捕获当前焦点并返回「恢复焦点」闭包；恢复成功返回 true，失败（控件已不可用）false
      CaptureFocus: unit -> (unit -> bool)
      /// 焦点是否在有文字的输入框里（Ctrl+Z 让位给文本撤销）
      IsEditingText: unit -> bool
      /// 当前 WebSocket 连接代次
      ConnectionGeneration: unit -> int
      /// 当前连接是否活着
      IsConnected: unit -> bool
      /// 订阅协议事件（入口负责代次过滤与 UI 线程 marshal）。返回值用于退订
      SubscribeHostEvents: (WireEvent -> unit) -> IDisposable
      /// 订阅连接关闭（入口负责代次过滤与 UI 线程 marshal；参数是错误说明 option）
      SubscribeClosed: (string option -> unit) -> IDisposable
      /// 关窗；桌面返回是否真的关了窗，PWA 恒 false
      CloseWindow: unit -> bool
      /// 本客户端名（配对请求里的 clientName）
      ClientName: unit -> string
      /// 图片缩略图：入口解码 bytes → IImage 并调 Composer.SetThumbnail；失败忽略
      MakeThumbnail: Guid -> byte[] -> unit
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
      /// 浮层（入口构造的那个 OverlayDesk）
      Overlay: Wanxiang2.OverlayDesk.OverlayDesk
      /// 对话框（入口构造：DialogsDesk(theme, overlay)）
      Dialogs: Wanxiang2.DialogsDesk.DialogsDesk
      /// 导出面板工厂（每个对话一份；参数由组合者给）
      NewExportDesk: (ConversationExportQuery -> Task<bool>) -> ExportLineage -> Guid -> string -> Wanxiang2.TransferDesk.ExportDesk
      /// 导入面板工厂
      NewImportDesk: (ImportPreview -> (bool -> unit) -> unit) -> Wanxiang2.TransferDesk.ImportDesk
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
    let mutable lastUrl = ports.DefaultServerUrl()
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
    /// 设置关闭后焦点的回家点（打开设置时捕获）。
    let mutable settingsReturnFocus: (unit -> bool) option = None
    /// 对话快照等待中的骨架屏计时器（归 DeferredReveal 武装，一对话只武装一次）。
    let mutable skeletonTimer: IDisposable option = None
    let mutable exportDialog: Wanxiang2.TransferDesk.ExportDesk option = None
    let mutable importDialog: Wanxiang2.TransferDesk.ImportDesk option = None
    /// 入口壳未经 Dispatch 直接调 ApplyHostEvent 时补上模型部分（避免重复进入 Dispatch）。
    let mutable dispatching = false
    let state = ClientState()
    let mutable deletion: DeletionDesk option = None
    let mutable protocolDispatch: ProtocolDispatch option = None
    let mutable shellCommands: ShellCommands option = None
    let mutable setConnectStatus: string -> unit = ignore
    let mutable failConnectStatus: ConnectionIssue -> unit = ignore

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
    let skeletonReveal = DeferredReveal<Guid>(DeferredReveal<Guid>.SkeletonDelay)
    let mutable lastPageLink = ""
    let mutable enteredSettingsForFirstProvider = false

    let v () =
        match views with
        | Some views -> views
        | None -> failwith "ShellComposer：视图尚未组装（先调 Build）"

    let toast text tone = ports.Toast text tone

    let send = ports.Send
    let newInvocation () = Guid.CreateVersion7()

    let attachmentDraft () = (drafts.Get(instanceId, activeConvId)).attachments

    let activeConversation () =
        activeConvId |> Option.bind (fun id -> state.Conversations.TryFind id)

    let activeSummary () =
        activeConvId |> Option.bind (fun id -> summaries |> List.tryFind (fun s -> s.id = id))

    let messagesOf (view: ConversationView) =
        view.messages |> Seq.cast<JsonNode> |> Seq.map MessageView.ofSnapshotItem |> List.ofSeq

    /// 输入区的对话上下文摘要（旧 Composer.SetSessionContext）：工具开关不算「特殊设置」。
    let contextLabelOf (config: SessionConfig option) : (string * string) option =
        let isCustomized (value: SessionConfig) =
            value.instructions.IsSome
            || value.temperature.IsSome
            || value.topP.IsSome
            || value.maxTokens.IsSome
            || value.thinkingBudget.IsSome
            || value.extraJson.IsSome
        match config with
        | Some value when isCustomized value ->
            let label = if value.instructions.IsSome then "系统指令" else "生成参数"
            let detail =
                sprintf "%s，%d 项工具。修改只影响当前对话。"
                    (if value.instructions.IsSome then "已设系统指令" else "默认系统指令")
                    value.tools.Length
            Some(label, detail)
        | _ -> None

    let systemPrefersDark () =
        try ports.SystemPrefersDark () with _ -> false

    /// 一次性延时：入口壳的 After 是普通计时器，回来时先自停（旧 DispatcherTimer.RunOnce 的语义）。
    let afterOnce (delay: TimeSpan) (action: unit -> unit) : IDisposable =
        let mutable handle: IDisposable = null
        let timer =
            ports.After delay (fun () ->
                let h = handle
                if not (isNull h) then h.Dispose()
                action ())
        handle <- timer
        timer

    // ---- 发送与回执（旧 sendCommand / sendCommandWithFeedback / sendCommandWithCompletion / sendConfigWithFeedback）----

    /// 旧 sendCommand：登记发送时代次，未发出就拒绝回执等待并把发件箱标未确认。
    member private this.SendCommand(cmd: ClientCommand) =
        let epoch = ports.ConnectionGeneration()
        let sending = ports.SendCommand cmd
        async {
            let! sent = sending |> Async.AwaitTask
            if not sent then
                ports.Post(fun () ->
                    if epoch = ports.ConnectionGeneration() then
                        let id = ClientCommand.invocationId cmd
                        commandFeedback.Reject id
                        |> Option.iter (fun feedback -> feedback.onCompleted false)
                        outbox.SetState(id, UnconfirmedMessage "连接已断开；内容已保留，可重试。")
                        outbox.RejectCreation(id, "连接已断开；内容已保留，可重试。")
                        this.Render())
        }
        |> Async.Start

    member private this.SendCommandWithFeedback
        (cmd: ClientCommand) (successMessage: string option) (onCommitted: unit -> unit)
        =
        commandFeedback.Track(cmd, successMessage, onCommitted)
        this.SendCommand cmd

    member private this.SendCommandWithCompletion
        (cmd: ClientCommand) (successMessage: string option) (onCommitted: unit -> unit) (onCompleted: bool -> unit)
        =
        commandFeedback.TrackWithCompletion(cmd, successMessage, onCommitted, onCompleted)
        this.SendCommand cmd

    member private this.SendConfigWithFeedback
        (successMessage: string) (eventOf: Guid -> WireEvent) (onCompleted: bool -> unit)
        =
        let requestId = newInvocation ()
        configFeedback.Track(requestId, successMessage, onCompleted)
        send (eventOf requestId)

    // ---- 认证步骤序列（旧 ApplyHostEvent AuthAccepted）----

    /// 连接真的成功：切实例、重置、声明能力、请求目录、观察列表、恢复草稿、渲染。
    member private this.ApplyAuthAccepted(newInstanceId: string) =
        if instanceId <> "" && instanceId <> newInstanceId then
            if exportDialog |> Option.exists (fun dialog -> dialog.IsOpen) then v().Overlay.CloseDialog()
            (drafts.Get(instanceId, activeConvId)).text <- v().Composer.Text
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
        // 新连接：能力表等主机重新声明；旧主机不会声明，保持「未知」。
        hostFeatures <- None
        // 声明本客户端能处理工具审批；旧客户端不发这条，主机据此对需要审批的调用直接拒绝。
        send (ToolApprovalCapability {| supported = true |})
        hasSavedCredentials <- true
        connectionError <- None
        connectionIssue <- None
        instanceId <- newInstanceId
        // 连接真的成功了，退避归零。
        reconnect.ResetDelay()
        pairingRequested <- false
        // 重连成功：上一代连接残留的失败卡片已过期，清掉免得误导。
        for s in summaries do runs.ClearError s.id
        activeConvId |> Option.iter runs.ClearError
        if not (exportDialog |> Option.exists (fun dialog -> dialog.IsOpen)) then v().Overlay.CloseDialog()
        v().Sidebar.SetConnection(true, "已连接")
        v().Settings.SetConnection(instanceId, lastUrl)
        this.RestoreLocalDrafts()
        if v().Settings.Devices.IsShown then v().Settings.Devices.Refresh()
        // 浏览器（PWA）记住这次连接；桌面入口给空实现。
        match lastToken with
        | Some token -> ports.SaveBrowserConnection newInstanceId lastUrl token (ports.ClientName())
        | None -> ()
        send CatalogRequest
        this.ObserveConversationList()
        activeConvId |> Option.iter (fun convId -> send (ObserveConversation {| conversationId = convId |}))
        this.Render()

    // ---- 连接 / 重连调度（计时由入口壳注入）----

    member private this.Connect() =
        // 新的连接让挂起的重连作废（票据随之失效）。
        let attempt = reconnect.Connect()
        connectionError <- None
        connectionIssue <- None
        v().Settings.Health.Disconnected()
        this.TearDownConnection()
        this.Render()
        let url = lastUrl
        let token = lastToken
        let usePairing = pairingRequested
        async {
            try
                let! epoch = ports.Connect url
                // ADR-001 方案 B：连接后第一个动作即认证。
                match token with
                | Some value -> ports.SendAuth epoch (AuthPresent {| token = value |})
                | None ->
                    if usePairing then
                        ports.SendAuth epoch (PairingRequested {| clientName = Some(ports.ClientName()) |})
                ports.Post(fun () ->
                    if reconnect.IsCurrent attempt && not authenticated then
                        v().Sidebar.SetConnection(false, "正在连接"))
            with ex ->
                ports.Post(fun () ->
                    if reconnect.IsCurrent attempt then
                        this.ReportConnectionIssue(ConnectionIssue.ofException ex)
                        v().Sidebar.SetConnection(false, "连接已断开")
                        this.ScheduleReconnect()
                        this.Render())
        }
        |> Async.Start

    /// 网络恢复 / 回到前台：正在退避的重连立刻执行。
    member this.ResumeReconnectNow() =
        if reconnect.ResumeNow() then
            pairingRequested <- false
            this.Connect()

    member private this.ScheduleReconnect() =
        match reconnect.Schedule lastToken.IsSome with
        | None -> ()
        | Some(ticket, delay) ->
            // 连接状态一个词：倒计时不进壳状态栏，重连中只报「正在重连」。
            v().Sidebar.SetConnection(false, "正在重连")
            async {
                do! Async.Sleep delay
                ports.Post(fun () ->
                    if reconnect.Fire ticket then
                        pairingRequested <- false
                        this.Connect())
            }
            |> Async.Start

    /// 记录并呈现一次类型化的连接失败（对话框内联 + 空态入口）。
    member private this.ReportConnectionIssue(issue: ConnectionIssue) =
        connectionIssue <- Some issue
        connectionError <- Some(ConnectIssueText.describe issue)
        failConnectStatus issue

    /// 断线收尾公共序列：主动 Connect 前的重置与 ConnectionClosed 的被动收尾共享。
    /// 入口各自的语义（健康状态、审批面板、设备通知、UI 呈现、重连调度）留在调用方。
    member private this.TearDownConnection() =
        authenticated <- false
        runs.Disconnect()
        exportDialog |> Option.iter (fun dialog -> dialog.Disconnect())
        this.FailOlderPage "连接已断开，更早的消息没有加载。重新连接后点「重试」。"
        outbox.Disconnect instanceId
        commandFeedback.RejectAll()
        configFeedback.RejectAll()
        this.FailUploadingAttachments()
        // 断连后底层下载流已死，不会有 Complete 事件；缓冲必须整体丢弃（R1）。
        downloads.Clear()

    /// 入口壳的连接关闭回调（旧 client.ConnectionClosed 的界面面）。
    member private this.HandleConnectionClosed(detail: string option) =
        v().Approval.Disconnected()
        v().Chat.SetAwaitingApproval Set.empty
        this.TearDownConnection()
        v().Settings.Devices.NotifyDisconnected()
        v().Sidebar.SetConnection(false, "连接已断开")
        match detail with
        | Some text when not (String.IsNullOrWhiteSpace text) -> toast (sprintf "连接已断开：%s" text) ShellToastTone.Warning
        | _ -> ()
        this.Render()
        this.ScheduleReconnect()

    /// 连接对话框（旧 ShowConnectDialog）：URL/令牌/配对与状态行都归它。
    member this.ShowConnectDialog() =
        let handle =
            v().Dialogs.ConnectWithClosed(
                lastUrl,
                (fun request ->
                    lastUrl <- request.Url
                    lastToken <- request.Token
                    pairingRequested <- request.UsePairing
                    // 用户手动发起：从最短等待重新开始。
                    reconnect.ResetDelay()
                    this.Connect()),
                (fun code ->
                    send (PairingAttempted {| code = code; clientName = Some(ports.ClientName()) |})),
                (fun () ->
                    setConnectStatus <- ignore
                    failConnectStatus <- ignore))
        setConnectStatus <- handle.Info
        failConnectStatus <- handle.Fail
        match connectionIssue with
        | Some issue -> handle.Fail issue
        | None ->
            connectionError |> Option.iter handle.Info
            // 决策 260：本机从没连过（无令牌、无保存凭据）时，配对码就是唯一可走的路。
            if PairingHint.pairingFirst lastToken hasSavedCredentials connectionError then
                handle.StartPairing ()

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

    // ---- 协议事件 ----

    /// 协议事件入口：模型部分归 ProtocolDispatch，界面面回到 ApplyHostEvent。
    member this.HandleEvent(ev: WireEvent) =
        dispatching <- true
        try this.Dispatch.Handle ev
        finally dispatching <- false

    /// 界面面。经 Dispatch 回来时只做界面面；入口壳直接调用时补上模型部分（顺序与旧 HandleEvent 相同）。
    member this.ApplyHostEvent(ev: WireEvent) =
        if dispatching then this.ApplyHostFace ev else this.HandleEvent ev

    /// ProtocolDispatch 做完模型部分后交回的界面面（旧 ApplyHostEvent）。
    member private this.ApplyHostFace(ev: WireEvent) =
        match ev with
        | AuthAccepted d -> this.ApplyAuthAccepted d.instanceId
        | AuthRejected _ ->
            authenticated <- false
            exportDialog |> Option.iter (fun dialog -> dialog.Disconnect())
            lastToken <- None
            hasSavedCredentials <- false
            v().Sidebar.SetConnection(false, "未认证")
            // 失败只报一次：连接对话框内联呈现原因（紧邻重连入口），不再叠 toast。
            // 认证被拒就是令牌失效：入口是「重新配对」，不是「连接失败」。
            this.ReportConnectionIssue ConnectionIssue.TokenInvalid
            this.Render()
        | PairingStarted _ ->
            connectionError <- None
            connectionIssue <- None
            setConnectStatus ConnectionIssue.pairingStartedStatus
        | PairingSucceeded d ->
            lastToken <- Some d.token
            hasSavedCredentials <- true
            connectionError <- None
            connectionIssue <- None
            setConnectStatus ConnectionIssue.pairingSucceededStatus
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
            // 配置重新加载成功：之前的「配置有误」随之消失。只在从有误恢复时提示一次。
            match ConfigIssueNotice.recovered (v().Settings.Health.ClearConfigIssue()) with
            | Some text ->
                this.UpdateHostStatusLine()
                toast text ShellToastTone.Success
            | None -> ()
            // 设备列表可见时跟着刷新（别的设备被吊销、重命名）。
            if v().Settings.Devices.IsShown then send DevicesRequest
        | HostConfigRejected d ->
            // 主机拒绝了一份配置并继续用上一份有效的；客户端必须看得见（决策 82）。
            let notice = ConfigIssueNotice.rejected (v().Settings.Health.ConfigIssue.IsSome) d.summary
            v().Settings.Health.SetConfigIssue(d.summary, d.errors)
            this.UpdateHostStatusLine()
            notice |> Option.iter (fun text -> toast text ShellToastTone.Warning)
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
            // 首次运行卡片自己发起的探测由它处理；其余交给设置页。
            if not (v().Onboarding.ApplyProbe(d.providerId, d.ok, models, d.error)) then
                v().Settings.ApplyProbe(d.providerId, d.ok, models, d.error)
        | ConversationListSnapshot d ->
            summaries <- ConversationSummary.parseList d.items
            lineage <- ForkLineage.parse d.items
            v().Sidebar.SetLineage lineage
            // SetConversations 即快照到达：侧栏加载中状态随数据一并解除。
            v().Sidebar.SetConversations(PendingDeletion.visible summaries (deletion |> Option.map _.Pending |> Option.defaultValue PendingDeletion.empty))
            v().Sidebar.SetActive activeConvId
            this.PruneDeletedConversationDrafts()
            listReceived <- true
            this.Render()
            pendingLink |> Option.iter this.OpenMessageLink
            if StartupTrace.mark "listReady" && StartupTrace.exitAfter then
                afterOnce (TimeSpan.FromSeconds 3.0) (fun () -> StartupTrace.requestExit ()) |> ignore
        | ConversationSnapshot d ->
            // 发件箱放行与第二次重画在 ProtocolDispatch 里（次数与旧代码相同）。
            if activeConvId = Some d.conversationId then
                pageLoading <- false
                this.Render()
                if StartupTrace.enabled then
                    ports.Post(fun () ->
                        if StartupTrace.mark "chatReady" && StartupTrace.exitAfter then StartupTrace.requestExit ())
        | HistoryPage d ->
            if activeConvId = Some d.conversationId then
                pageLoading <- false
                pageRequestedAt <- None
                v().Chat.Pane.HideOlderFailure()
                this.Render()
                v().Chat.RestoreHistoryPrependAnchorDeferred()
        | SearchResults d ->
            // 迟到的旧结果已由 ProtocolDispatch 丢掉。
            v().Sidebar.ApplySearchResults(
                { queryId = d.queryId
                  query = d.query
                  hits = SearchHit.parse d.items
                  hasMore = d.hasMore
                  truncated = d.truncated },
                d.offset)
        | SearchFailed d -> v().Sidebar.ApplySearchFailure(v().Sidebar.SearchQueryText, d.message)
        | ConversationExportPage page -> exportDialog |> Option.iter (fun dialog -> dialog.Handle page)
        | ConversationExportFailed d -> exportDialog |> Option.iter (fun dialog -> dialog.Fail(d.exportId, d.message))
        | GenerationFinished d ->
            // PWA 安装引导推迟到第一次成功对话之后（第 210 节）。
            if d.status = "completed" then ports.BrowserConversationSucceeded()
        | HostCapabilities d ->
            hostFeatures <- Some(Set.ofList d.features)
            this.SyncPresetSurfaces()
        | AttachmentCommitted d ->
            match drafts.All |> Seq.tryPick (fun draft -> draft.attachments.Complete(d.attachmentId, d.size)) with
            // 决策 351：上传完成不再 toast——附件条上的 chip 已自证完成；
            // 失败仍恰报一次（C6）。
            | Some(_, stillDrafted, key) when stillDrafted && key = (instanceId, activeConvId) ->
                v().Composer.SetAttachments((attachmentDraft ()).Items)
            | _ -> ()
        | AttachmentAborted d ->
            match drafts.All |> Seq.tryPick (fun draft -> draft.attachments.Abort d.attachmentId) with
            | Some(upload, _, _) ->
                v().Composer.SetAttachments((attachmentDraft ()).Items)
                toast (UploadFacts.uploadFailedText upload.fileName) ShellToastTone.Warning
            | None -> ()
        | _ -> ()

    // ---- 对话操作编排 ----

    /// 加载时的骨架屏：一对话只武装一次，超时才挂 chrome。
    member private this.ShowConversationLoadingDeferred(convId: Guid) =
        if skeletonReveal.Arm(convId, DateTimeOffset.UtcNow) then
            skeletonTimer |> Option.iter (fun timer -> timer.Dispose())
            skeletonTimer <-
                Some(
                    afterOnce skeletonReveal.Delay (fun () ->
                        if skeletonReveal.Expired convId && activeConvId = Some convId && (activeConversation ()).IsNone then
                            v().Chat.SetTitle("加载中…", false)
                            v().Chat.Pane.ShowSkeleton()))

    /// 快照到达或切换对话：解除骨架屏武装并停表。
    member private this.DisarmSkeletonLoading() =
        skeletonTimer |> Option.iter (fun timer -> timer.Dispose())
        skeletonTimer <- None
        skeletonReveal.Disarm()

    /// 断线时把所有上传中的附件标成失败：底层流已死，不会再有 Complete/Abort（C4）。
    member private this.FailUploadingAttachments() =
        for draft in drafts.All do
            draft.attachments.MarkAllUploadingAsFailed() |> ignore
        v().Composer.SetAttachments((attachmentDraft ()).Items)

    member private this.Render() =
        // 视图驱动：把状态推给各 Desk（旧 AppShell.Render 的编排）。
        let run = runs.Get activeConvId
        let generating = run.running
        v().Sidebar.SetServerUrl lastUrl
        v().Approval.SetConversation activeConvId
        let startReady =
            StartReady.isReady authenticated (Catalog.isReady catalog) generating run.error.IsSome
                (match activeConvId, activeConversation () with
                 | None, _ -> ActiveConversation.None
                 | Some _, Some view -> ActiveConversation.Loaded view.messages.Count
                 | Some _, None -> ActiveConversation.Loading)
        v().Workspace.SetStarting startReady
        v().Composer.SetContext(
            contextLabelOf (activeConversation () |> Option.map (fun view -> view.config)),
            authenticated)
        match activeConvId, activeConversation () with
        | None, _ ->
            v().Chat.SetConversationChrome false
            v().Chat.SetBreadcrumb None
            v().Chat.SetTitle("新对话", false)
            let rePair =
                match connectionIssue |> Option.bind ConnectionIssue.retry with
                | Some RePair -> true
                | _ -> false
            let idle =
                IdleSurface.decide authenticated connectionError rePair hasSavedCredentials
                    (Catalog.isReady catalog) (v().Onboarding.IsBusy)
            let primary = idle.primary |> Option.map (fun label -> label, fun () -> this.ShowConnectDialog())
            match idle.empty with
            | IdleEmpty.ConnectionFailed err -> v().Chat.Pane.ShowEmpty(ChatEmptyState.ConnectionFailed err, primary)
            | IdleEmpty.NotConnected -> v().Chat.Pane.ShowEmpty(ChatEmptyState.NotConnected, primary)
            | IdleEmpty.Onboarding ->
                // 首次运行：卡片就在聊天界面里，不必先进设置页。
                v().Chat.Pane.ShowEmbedded(v().Onboarding.View)
                v().Onboarding.StartDiscovery()
            | IdleEmpty.NoConversation -> v().Chat.Pane.ShowEmpty(ChatEmptyState.NoConversation, None)
            v().Composer.SetEnabled(idle.composerEnabled, idle.composerReason)
            let chip = Catalog.idleModelChip idle.modelLabel draftModelSelection catalog
            v().Composer.SetModelLabel chip.label
            chip.vision
            |> Option.iter (fun (support, label) -> v().Composer.SetModelVision(support, label))
        | Some convId, None ->
            v().Chat.SetConversationChrome true
            // Loading 分级：快照 300ms 内到达就不闪任何 loading chrome，超时才在本地挂骨架。
            this.ShowConversationLoadingDeferred convId
            v().Composer.SetEnabled(false, IdleSurface.LoadingReason)
        | Some convId, Some view ->
            this.DisarmSkeletonLoading()
            let messages = messagesOf view
            let streaming = run.message
            let summary = activeSummary ()
            v().Chat.SetConversationChrome true
            v().Chat.SetBreadcrumb(ForkLineage.breadcrumb summaries lineage convId)
            v().Chat.SetTitle((summary |> Option.map (fun s -> s.title) |> Option.defaultValue "对话"), true)
            v().Composer.SetModelLabel(Catalog.describeModel view.config.provider view.config.model catalog)
            v().Composer.SetModelVision(
                fst (Catalog.visionOf view.config.provider view.config.model catalog),
                Catalog.describeModel view.config.provider view.config.model catalog)
            let surface =
                ConversationSurface.decide
                    (List.isEmpty messages) streaming.IsSome run.error.IsSome run.partial.IsSome generating
                    (ReplyTail.isUnanswered DateTimeOffset.UtcNow messages) authenticated run.generationId.IsSome
            if surface.showEmpty then
                v().Chat.Pane.HideSkeleton()
                v().Chat.Pane.ShowEmpty(ChatEmptyState.EmptyConversation, None)
            else
                v().Chat.Pane.HideSkeleton()
                v().Chat.Pane.HideEmpty()
                // 决策 300：「上一轮」越过已加载历史时要知道主机上还有没有更早的页。
                v().Chat.Pane.SetOlderAvailable view.pageHasMore
                let retry () = this.Regenerate()
                // 先同步生成状态：渲染时要据此区分工具调用「执行中」与「结果未知」。
                v().Chat.SetGenerating(generating, surface.generatingText)
                v().Chat.SetPartialReply(run.partial |> Option.map (fun p -> p, fun () -> this.ContinueGeneration(convId, p)))
                v().Chat.Render
                    { Messages = messages
                      Streaming = streaming
                      Error =
                        (match surface.tailError, run.error with
                         | TailError.RunError, Some e -> Some(e, retry)
                         | TailError.Unanswered, _ -> Some(UnansweredReply.error, retry)
                         | _ -> None)
                      FontSize = prefs.fontScale
                      AutoCollapseReasoning = prefs.autoCollapseReasoning
                      Usage = run.usage
                      MissingAttachments = missingAttachments }
                this.TryReveal(convId, view, messages)
            v().Composer.SetEnabled(surface.composerEnabled, surface.composerReason)
        v().Composer.SetGenerating generating
        v().Composer.SetCanStop(authenticated && run.generationId.IsSome)
        v().Composer.SetAttachments((attachmentDraft ()).Items)
        v().Composer.SetPendingMessages(
            outbox.ForInstance instanceId,
            activeConvId,
            authenticated,
            (fun id -> this.RetryPendingMessage id),
            (fun id ->
                this.SelectConversation(Some id)
                if outbox.Items(instanceId, Some id) |> List.forall _.creationConfirmed then
                    send (ObserveConversation {| conversationId = id |})))
        v().Chat.SetGenerating(generating, (if generating then "生成中" else ""))
        v().Chat.SetCanStop(authenticated && run.generationId.IsSome)

    /// 主机状态行：连接正常时带上「配置有误」提示。
    member private this.UpdateHostStatusLine() =
        if authenticated then
            // 主机地址已在上一行（serverCaption）显示，状态行只说状态，不重复主机名。
            v().Sidebar.SetConnection(true, ConversationSurface.hostStatusLine (v().Settings.Health.ConfigIssue.IsSome))

    member private this.ApplyTheme() =
        ports.ApplyTheme(UiPrefsFile.resolveTheme prefs (systemPrefersDark ()))

    /// 系统深色变化：只有「跟随系统」才重应用主题。
    member this.OnSystemThemeChanged() =
        if prefs.theme = ThemePreference.FollowSystem then this.ApplyTheme()

    member this.SavePrefs(next: UiPrefs) =
        let themeChanged = next.theme <> prefs.theme
        let draftsToggled = next.persistDrafts <> prefs.persistDrafts
        let motionChanged = next.reduceMotion <> prefs.reduceMotion
        prefs <- next
        ports.PrefsSave prefs
        if themeChanged then this.ApplyTheme()
        if motionChanged then ports.SetReducedMotion prefs.reduceMotion
        // 代码块折行是渲染期读取的默认值，改完要立刻生效。
        ports.SetCodeWrap prefs.codeWrap
        v().Sidebar.SetShowArchived prefs.showArchived
        v().Composer.SetEnterSends prefs.enterSends
        v().Settings.SetPrefs prefs
        // 关闭保存：立即删除本机已存的内容（之后不再写）；重新打开：下一次保存即生效。
        if draftsToggled then
            if not prefs.persistDrafts then
                ports.ApplyDraftWrite DraftWrite.Delete
                draftSession.StorageCleared()
            else this.FlushDrafts()
            this.UpdateDraftSummary()
        this.Render()

    // ---- 本地草稿 ----

    member private this.SyncActiveDraftText() =
        if instanceId <> "" then
            (drafts.Get(instanceId, activeConvId)).text <- v().Composer.Text

    /// 内存里的草稿与未确认消息（先把输入框文字同步进去）。
    member private this.LiveDraftSnapshot() : DraftSnapshot =
        this.SyncActiveDraftText()
        DraftPolicy.snapshotOf drafts outbox (fun id -> draftSession.SavedAt(id, DateTimeOffset.UtcNow))

    member private this.CurrentDraftSnapshot() : DraftSnapshot =
        draftSession.ForSave(this.LiveDraftSnapshot())

    member private this.UpdateDraftSummary() =
        let snapshot = this.CurrentDraftSnapshot()
        let texts, unsent = DraftRestore.summaryCounts snapshot
        v().Settings.LocalDrafts.Summary <- DraftSummaryText.text prefs.persistDrafts texts unsent

    /// 保存到本机。关闭保存、尚未读完磁盘、或内容没变时什么也不做。
    member this.FlushDrafts() =
        // 读完之前连快照都不算（不记保存时刻），与旧时序相同。
        if draftSession.IsLoaded && prefs.persistDrafts then
            let write = draftSession.Flush(prefs.persistDrafts, this.LiveDraftSnapshot())
            ports.ApplyDraftWrite write

    /// 把本机保存的内容放回内存：只处理当前主机，且不覆盖用户这次已经输入的文字。
    member private this.RestoreLocalDrafts() =
        match draftSession.Restore(prefs.persistDrafts, instanceId) with
        | Some stored ->
            let unsentBefore = outbox.ForInstance instanceId |> List.length
            DraftPolicy.restoreInto stored instanceId drafts outbox |> ignore
            let unsentRestored = (outbox.ForInstance instanceId |> List.length) - unsentBefore
            let notice = draftSession.Restored(instanceId, unsentRestored)
            let live = drafts.Get(instanceId, activeConvId)
            if DraftRestore.fillComposer (v().Composer.Text) live.text then v().Composer.SetText live.text
            v().Composer.SetAttachments((attachmentDraft ()).Items)
            notice |> Option.iter (fun message -> toast message ShellToastTone.Info)
            this.UpdateDraftSummary()
        | None -> ()

    /// 清除本地草稿：本机落盘内容、内存里各对话的草稿、未确认的消息一并清空。
    /// 已发送并保存的对话不受影响。
    member this.ClearLocalDrafts() =
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
        this.UpdateDraftSummary()
        this.Render()
        toast cleared ShellToastTone.Success

    /// 列表里消失的对话 = 已被删除：它的草稿与未确认消息已无处可发，随之清理。
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

    // ---- 决策 440：能力表闸门 ----

    member private _.RequireFeature(feature: string) : bool =
        if HostFeatures.supports hostFeatures feature then true
        else
            toast (HostFeatures.unsupportedMessage feature) ShellToastTone.Warning
            false

    member this.ProbeProvider(providerId: string) =
        let requestId = newInvocation ()
        probes.Track(requestId, providerId, DateTimeOffset.UtcNow)
        send (ProviderProbeRequest {| requestId = requestId; providerId = providerId |})

    /// 决策 443/444：到点仍没回应的请求统一收尾。
    member private this.ExpireHostRequests(now: DateTimeOffset) : bool =
        let timeout = (ms Timing.HostRequestTimeout)
        let message = HostFeatures.noResponseMessage hostFeatures
        let expiredConfig = configFeedback.Expire(now, timeout)
        let expiredCommands = commandFeedback.Expire(now, timeout)
        for f in expiredConfig do f.onCompleted false
        for f in expiredCommands do f.onCompleted false
        if not (List.isEmpty expiredConfig && List.isEmpty expiredCommands) then toast message ShellToastTone.Warning
        if search.Expire(now, timeout) then v().Sidebar.ApplySearchFailure(v().Sidebar.SearchQueryText, message)
        for (_, pid) in probes.Expire(now, timeout) do
            let err = Some message
            if not (v().Onboarding.ApplyProbe(pid, false, [], err)) then v().Settings.ApplyProbe(pid, false, [], err)
        not (List.isEmpty expiredCommands)

    // ---- 对话 ----

    member private this.SelectConversation(id: Guid option) =
        if activeConvId <> id then
            (drafts.Get(instanceId, activeConvId)).text <- v().Composer.Text
            activeConvId <- id
            v().Composer.SetText((drafts.Get(instanceId, id)).text)
            v().Composer.SetAttachments((attachmentDraft ()).Items)
            v().Chat.CancelHistoryPrependAnchor()
            // 换对话：滚动状态归零（上一段对话的定位钉住不能带进新对话）。
            v().Chat.ResetScrollState()
            pageLoading <- false
            pageRequestedAt <- None
        this.DisarmSkeletonLoading()
        let navBefore = v().Workspace.State
        v().Workspace.SetCompactNavigation false |> ignore
        v().Sidebar.SetActive activeConvId
        this.Render()
        // compact 下收起导航会带走焦点（掉到页顶）：交还给输入区。
        if navBefore.compactMode && navBefore.compactNavigationOpen then v().Composer.Focus()

    member this.OpenConversation(id: Guid) =
        this.SelectConversation(Some id)
        send (ObserveConversation {| conversationId = id |})
        // 对话激活（键盘/可见顺序/分叉入口统一走这里）：焦点回家到输入区（D3/D4）。
        v().Composer.Focus()

    member private this.MakeConversationCommand(conversationId: Guid) = this.MakeConversationCommandWith(conversationId, None)

    member private this.MakeConversationCommandWith(conversationId: Guid, preset: SessionConfig option) =
        match preset with
        | Some config ->
            CreateConversation
                {| invocationId = newInvocation (); conversationId = conversationId; title = "新对话"; config = config |}
        | None ->
            let provider, model =
                // 草稿里显式挑过的模型优先：没有对话时的选择不该被「默认」抹掉。
                draftModelSelection |> Option.orElse (Catalog.defaultSelection catalog) |> Option.defaultValue ("", "")
            let config =
                { SessionConfig.empty with
                    provider = provider
                    model = model
                    instructions = catalog.generation.instructions
                    temperature = catalog.generation.temperature
                    topP = catalog.generation.topP
                    maxTokens = catalog.generation.maxTokens }
            CreateConversation
                {| invocationId = newInvocation (); conversationId = conversationId; title = "新对话"; config = config |}

    member this.NewConversation() : Guid option = this.CreateConversation()

    member private this.CreateConversationWith(preset: SessionConfig option) : Guid option =
        if not authenticated then
            toast "连接主机后才能新建对话；草稿会保留。" ShellToastTone.Warning
            None
        elif not (Catalog.isReady catalog) then
            toast "先添加一个模型服务。" ShellToastTone.Warning
            this.ShowAddProvider()
            None
        else
            let conversationId = Guid.CreateVersion7()
            // 草稿选择已在新对话的 config 里落地，暂存位清掉。
            draftModelSelection <- None
            this.SelectConversation(Some conversationId)
            this.SendCommandWithCompletion
                (this.MakeConversationCommandWith(conversationId, preset))
                None
                (fun () -> send (ObserveConversation {| conversationId = conversationId |}))
                (fun ok ->
                    if not ok && activeConvId = Some conversationId then this.SelectConversation None)
            // 新对话落定：焦点有家，直接进输入区（D3/D4）。
            v().Composer.Focus()
            Some conversationId

    member private this.CreateConversation() : Guid option = this.CreateConversationWith None

    member this.RenameConversation(summary: ConversationSummary) =
        v().Dialogs.Prompt(
            "重命名对话",
            "对话标题",
            summary.title,
            "保存",
            fun title onCompleted ->
                let command =
                    RenameConversation
                        {| invocationId = newInvocation (); conversationId = summary.id; title = title |}
                this.SendCommandWithCompletion command None ignore onCompleted)
        |> ignore

    /// §214：删除即撤销——不再弹「无法撤销」确认框；先隐去，提示条给「撤销」。
    member private this.ScheduleDeletion(ids: Guid list) =
        match deletion with
        | Some desk -> desk.Schedule ids
        | None -> ()

    member this.DeleteConversation(summary: ConversationSummary) = this.ScheduleDeletion [ summary.id ]

    member this.SetPinned(summary: ConversationSummary, pinned: bool) =
        let command =
            SetConversationFlags
                {| invocationId = newInvocation ()
                   conversationId = summary.id
                   pinned = pinned
                   archived = summary.archived |}
        this.SendCommandWithFeedback
            command
            (Some(if pinned then sprintf "已置顶「%s」" summary.title else sprintf "已取消置顶「%s」" summary.title))
            ignore

    member this.SetArchived(summary: ConversationSummary, archived: bool) =
        let command =
            SetConversationFlags
                {| invocationId = newInvocation ()
                   conversationId = summary.id
                   pinned = summary.pinned
                   archived = archived |}
        this.SendCommandWithFeedback
            command
            (Some(if archived then sprintf "已归档「%s」" summary.title else sprintf "已取消归档「%s」" summary.title))
            ignore

    member this.SelectionAllPinned(ids: Guid list) : bool =
        let selected = summaries |> List.filter (fun s -> List.contains s.id ids)
        not (List.isEmpty selected) && selected |> List.forall (fun s -> s.pinned)

    /// 批量选择：置顶 / 归档按当前选择逐个发命令——每条命令独立可确认。
    member this.SetPinnedMany(ids: Guid list, pin: bool) =
        for id in ids do
            match summaries |> List.tryFind (fun s -> s.id = id) with
            | Some summary ->
                let command =
                    SetConversationFlags
                        {| invocationId = newInvocation ()
                           conversationId = summary.id
                           pinned = pin
                           archived = summary.archived |}
                this.SendCommand command
            | None -> ()
        toast (sprintf "%s %d 个对话" (if pin then "已置顶" else "已取消置顶") (List.length ids)) ShellToastTone.Info
        v().Sidebar.ExitSelection()

    member this.SetArchivedMany(ids: Guid list, archived: bool) =
        for id in ids do
            match summaries |> List.tryFind (fun s -> s.id = id) with
            | Some summary ->
                let command =
                    SetConversationFlags
                        {| invocationId = newInvocation ()
                           conversationId = summary.id
                           pinned = summary.pinned
                           archived = archived |}
                this.SendCommand command
            | None -> ()
        toast (sprintf "%s %d 个对话" (if archived then "已归档" else "已取消归档") (List.length ids)) ShellToastTone.Info
        v().Sidebar.ExitSelection()

    member this.DeleteMany(ids: Guid list) =
        if not (List.isEmpty ids) then
            v().Sidebar.ExitSelection()
            this.ScheduleDeletion ids

    member this.DuplicateAsFork(summary: ConversationSummary) =
        this.OpenConversation summary.id
        toast "已打开对话，可从任意消息分叉。" ShellToastTone.Info

    member private this.DispatchPendingMessage(item: PendingMessage) =
        let id = PendingMessage.invocationId item
        if item.instanceId <> instanceId || not authenticated then
            outbox.SetState(id, UnconfirmedMessage "请连接原主机后重试。")
        else
            match item.creation with
            | Some creation when not item.creationConfirmed ->
                outbox.SetState(id, PreparingConversation)
                this.SendCommandWithCompletion
                    creation
                    None
                    (fun () ->
                        outbox.ConversationReady(item.instanceId, item.conversationId)
                        send (ObserveConversation {| conversationId = item.conversationId |}))
                    (fun ok ->
                        if not ok then
                            outbox.SetState(id, UnconfirmedMessage "对话尚未确认创建；原文已保留。")
                            this.Render())
            | _ when not (state.Conversations.ContainsKey item.conversationId) ->
                outbox.SetState(id, PreparingConversation)
                send (ObserveConversation {| conversationId = item.conversationId |})
            | _ ->
                outbox.SetState(id, SendingMessage)
                this.SendCommand item.command

    /// 决策 230：把半截回复落盘并从它接着生成。与「重试」相反，已写出的部分保留。
    member this.ContinueGeneration(convId: Guid, partial: MessageView) =
        if not authenticated then toast "连接已断开，重新连接后可继续生成。" ShellToastTone.Warning
        elif not (this.RequireFeature HostFeatures.ContinueResponse) then ()
        else
            runs.ClearError convId
            this.SendCommandWithCompletion
                (ContinueResponse
                    {| invocationId = newInvocation ()
                       conversationId = convId
                       messageJson = ConversationRuns.PartialMessageJson partial |})
                None
                ignore
                ignore
            this.Render()
            v().Composer.Focus()

    member private this.RetryPendingMessage(id: Guid) =
        match outbox.TryFind id with
        | Some item when PendingMessage.canRetry item && item.instanceId = instanceId ->
            this.DispatchPendingMessage item
            this.Render()
            // 重试发出：错误行/重试按钮随状态机翻转，焦点先回家（D3/D4）。
            v().Composer.Focus()
        | _ -> ()

    member this.SendMessage(text: string) : bool =
        let draft = attachmentDraft ()
        // 失败态优先：失败的附件不会自动变好，继续报「上传中」等于指错路（C6）。
        if draft.HasFailed then
            toast "有上传失败的附件，请先移除再发送。" ShellToastTone.Warning
            false
        elif draft.HasUploading then
            toast "附件上传中，请等待上传完成后再发送。" ShellToastTone.Warning
            false
        elif not authenticated || not (ports.IsConnected()) then
            toast "连接已断开，草稿已保留。" ShellToastTone.Warning
            false
        elif String.IsNullOrWhiteSpace text && not draft.HasReady then false
        elif activeConvId.IsNone && not (Catalog.isReady catalog) then
            toast "还没有可用的模型，先添加一个模型服务。" ShellToastTone.Warning
            false
        else
            let isNew = activeConvId.IsNone
            let id = activeConvId |> Option.defaultWith Guid.CreateVersion7
            let creation = if isNew then Some(this.MakeConversationCommand id) else None
            // 先建立完整的保留副本，再消费附件和清输入框。
            let item = outbox.Stage(instanceId, id, text, draft.Items, creation)
            draft.TryConsumeReady() |> ignore
            if isNew then
                this.SelectConversation(Some id)
                drafts.Move(instanceId, None, Some id)
                // SelectConversation 会载入新草稿，Submit 只清自己提交的文本。
                v().Composer.SetText text
            runs.ClearError id
            this.DispatchPendingMessage item
            this.Render()
            true

    member this.Regenerate() =
        match activeConvId with
        | None -> ()
        | Some convId ->
            let fire () =
                runs.ClearError convId
                this.SendCommand (RegenerateResponse {| invocationId = newInvocation (); conversationId = convId |})
                this.Render()
                // 错误卡片随重试卸载：焦点无处可去会掉到页顶，先交还给输入区。
                v().Composer.Focus()
            // 重新生成不可恢复：主机会删掉尾部整段回复链再重新生成，所以有内容时必须确认。
            // 生成失败、尾部什么都没留下时直接重试（决策 221）。
            let losable =
                activeConversation ()
                |> Option.map (fun view -> ReplyTail.hasLosableContent (messagesOf view))
                |> Option.defaultValue false
            if not losable then fire ()
            else
                v().Dialogs.Confirm(
                    "重新生成",
                    "将丢弃当前这条回复并重新生成。此操作无法撤销。",
                    "重新生成",
                    fire)
                |> ignore

    /// Ctrl+Shift+G：与卡片菜单的「重新生成」同一路径；生成中、未连接、尾部不是回复时不动作。
    member this.RegenerateLatest() =
        let gate = ReplyGate.regenerate (activeConversation () |> Option.map messagesOf) (runs.Get activeConvId).generationId.IsSome
        match gate, ReplyGate.regenerateText gate with
        | RegenerateGate.Allowed, _ -> this.Regenerate()
        | RegenerateGate.Generating, Some text -> toast text ShellToastTone.Info
        | _, Some text -> toast text ShellToastTone.Warning
        | _, None -> ()

    /// Ctrl+Shift+C：复制最新一条有正文的回复。
    member this.CopyLatestReply() =
        match activeConversation () |> Option.bind (fun view -> ReplyTail.latestText (messagesOf view)) with
        | Some text -> ports.CopyToClipboard text
        | None -> toast ReplyGate.nothingToCopyText ShellToastTone.Warning

    member this.StopGeneration() =
        match activeConvId, (runs.Get activeConvId).generationId with
        | Some convId, Some generationId ->
            send (GenerationCancel {| conversationId = convId; generationId = generationId |})
            // 停止按钮随生成结束隐藏：焦点先回家。
            v().Composer.Focus()
        | _ -> ()

    member this.ForkFrom(commitId: uint64 option, seedText: string) =
        match activeConvId, activeConversation () with
        | Some convId, Some view ->
            v().Dialogs.EditText(
                "编辑并分叉",
                seedText,
                "创建分叉",
                fun edited ->
                    let forkAfterId =
                        ForkAnchor.forkAfter (messagesOf view |> List.choose (fun m -> m.commitId)) commitId
                    let editedMessage = ForkAnchor.editedMessage edited
                    let newId = Guid.CreateVersion7()
                    let command =
                        ForkConversation
                            {| invocationId = newInvocation ()
                               conversationId = newId
                               parentConversationId = convId
                               forkAfterId = forkAfterId
                               config = SessionConfig.empty
                               editedMessageJson = editedMessage |}
                    this.SendCommandWithFeedback command None (fun () ->
                        this.SelectConversation(Some newId)
                        send (ObserveConversation {| conversationId = newId |}))
                    // 分叉落定与新建/打开对话同一归宿：焦点回家到输入区（D3/D4）。
                    v().Composer.Focus())
            |> ignore
        | _ -> toast ForkAnchor.noConversationText ShellToastTone.Warning

    /// 卡片菜单「编辑并分叉」：以这条消息的文本做种子。
    member this.EditAndFork(message: MessageView) = this.ForkFrom(message.commitId, message.text)

    /// 从当前对话的最后一条用户消息分叉（聊天标题菜单与命令面板共用）。
    member this.ForkFromHere() =
        match activeConversation () with
        | Some view ->
            let seed =
                messagesOf view
                |> List.filter MessageView.isUser
                |> List.tryLast
                |> Option.map (fun m -> m.text)
                |> Option.defaultValue ""
            this.ForkFrom(None, seed)
        | None -> toast ForkAnchor.noConversationText ShellToastTone.Warning

    member this.Fork() = this.ForkFromHere()

    member this.RenameTitle(title: string) =
        match activeConvId with
        | Some convId ->
            this.SendCommand (RenameConversation {| invocationId = newInvocation (); conversationId = convId; title = title |})
        | None -> ()

    member this.DeleteMessage(commitId: uint64) =
        match activeConvId with
        | Some convId ->
            v().Dialogs.Confirm(
                "删除消息",
                "这条消息会从对话中移除，后续生成不再看到它。",
                "删除",
                fun () ->
                    let command =
                        DeleteMessage
                            {| invocationId = newInvocation (); conversationId = convId; messageCommitId = commitId |}
                    this.SendCommandWithFeedback command (Some "消息已删除") ignore)
            |> ignore
        | None -> ()

    member this.DownloadAttachment(sha: string) = send (AttachmentDownloadRequest {| sha256 = sha |})

    member this.CopyMessageLink(commitId: uint64) =
        match activeConvId with
        | Some convId ->
            let pageUrl = ports.BrowserPageUrl()
            let pageUrl = if String.IsNullOrWhiteSpace pageUrl then None else Some pageUrl
            ports.CopyToClipboard (MessageLink.format pageUrl convId commitId)
        | None -> ()

    /// §215：生成失败就地恢复。OpenProviderSettings → 设置页分区；SwitchModel → 模型切换器。
    member this.Recover(recovery: ErrorRecovery) =
        match recovery with
        | OpenProviderSettings ->
            v().Settings.ShowSection Wanxiang2.SettingsScreen.SettingsPart.Providers
            this.ShowSettings None
        | OpenConversationSettings -> this.ShowSessionContext()
        | SwitchModel -> this.ShowModelPicker(v().Composer.ModelAnchor())
        | StartNewConversation -> this.CreateConversation() |> ignore

    member this.ShowSessionContext() =
        match activeConvId, activeConversation () with
        | Some convId, Some view when authenticated ->
            v().Dialogs.SessionSettings(
                catalog,
                view.config,
                fun config onCompleted ->
                    let command =
                        UpdateConversationConfig
                            {| invocationId = newInvocation (); conversationId = convId; config = config |}
                    this.SendCommandWithCompletion command (Some "对话设置已更新") ignore onCompleted)
            |> ignore
        | _ -> toast "连接并选择对话后，可以调整上下文。" ShellToastTone.Warning

    /// 打开消息链接（决策 231）：与搜索命中同一路径（滚到那条消息并短暂着色）。
    member private this.OpenMessageLink((convId: Guid, commitId: uint64)) =
        if not authenticated || List.isEmpty summaries && not listReceived then
            pendingLink <- Some(convId, commitId)
        else
            pendingLink <- None
            if summaries |> List.exists (fun s -> s.id = convId) then
                pendingReveal <- Some(convId, RevealMessage commitId)
                this.OpenConversation convId
                if (activeConversation ()).IsSome then this.Render()
            else
                toast "链接指向的对话不存在或已删除。" ShellToastTone.Warning

    /// 消息链接 → 应用内打开；否则交给系统浏览器；两者都不是就说清楚。
    member this.OpenUrl(url: string) =
        match MessageLink.tryParse url with
        | Some link -> this.OpenMessageLink link
        | None ->
            match Uri.TryCreate(url, UriKind.Absolute) with
            | true, uri when uri.Scheme = Uri.UriSchemeHttp || uri.Scheme = Uri.UriSchemeHttps -> ports.OpenLink url
            | _ -> toast "无法识别这个链接。" ShellToastTone.Warning

    /// 点击一条搜索命中：打开那段对话，并滚动到命中的消息（目标在更早历史里则先加载）。
    member this.OpenSearchHit(hit: SearchHit) =
        pendingReveal <- Some(hit.conversationId, RevealMessage hit.messageId)
        this.OpenConversation hit.conversationId
        // 已经在这段对话里：不会有新快照来触发 Render，主动重试一次。
        if (activeConversation ()).IsSome then this.Render()

    /// 在同一消息位置的另一个版本（原文或某个分叉）之间切换，并滚到该位置。
    member this.SwitchVersion(target: Guid) =
        let reveal =
            match lineage.TryFind target with
            | Some l -> l.forkFirstMessageId |> Option.map RevealMessage
            | None ->
                // 回到原文：定位到当前分叉的分叉点之后那一条。
                activeConvId
                |> Option.bind lineage.TryFind
                |> Option.map (fun l -> RevealAfter l.forkBaseMessageId)
        pendingReveal <- reveal |> Option.map (fun r -> target, r)
        this.OpenConversation target
        if activeConvId = Some target && (activeConversation ()).IsSome then this.Render()

    /// 搜索命中 / 版本切换后把目标消息带进视口：更早就继续加载更早历史；到头仍找不到则放弃。
    member private this.TryReveal(convId: Guid, view: ConversationView, messages: MessageView list) =
        match pendingReveal with
        | Some(target, reveal) when target = convId ->
            let loaded = messages |> List.choose _.commitId
            match RevealTarget.resolve reveal loaded with
            | Some commit ->
                pendingReveal <- None
                match reveal with
                | RevealMessage _ -> v().Chat.RevealAndHighlight commit |> ignore
                | RevealAfter _ -> v().Chat.ScrollToMessage commit |> ignore
            | None ->
                let wanted = match reveal with RevealMessage c -> Some c | RevealAfter _ -> None
                match wanted with
                | Some c when view.pageHasMore && c < view.pageEarliest ->
                    if not pageLoading then this.RequestOlderHistory()
                | _ -> pendingReveal <- None
        | _ -> ()

    /// 决策 440：主机明确声明功能表却不含 history-paging 时早失败早提示。
    member this.RequestOlderHistory() =
        if not (this.RequireFeature HostFeatures.HistoryPaging) then ()
        else
            match activeConvId, activeConversation () with
            | Some convId, Some view when view.pageHasMore && not pageLoading ->
                v().Chat.BeginHistoryPrependAnchor()
                v().Chat.Pane.HideOlderFailure()
                pageLoading <- true
                pageRequestedAt <- Some DateTimeOffset.UtcNow
                // 分页没有骨架屏（会重置滚动）：一条 transient toast 给加载反馈。
                toast "正在加载更早消息…" ShellToastTone.Info
                send (HistoryRequest
                        {| conversationId = convId; beforeCommitId = view.pageEarliest; limit = 100 |})
            | _ -> ()

    member private this.ShowOlderPageRetry(message: string) =
        v().Chat.Pane.ShowOlderFailure(
            message,
            fun () ->
                if authenticated then this.RequestOlderHistory()
                else this.ShowOlderPageRetry OlderPageLoad.offlineText)

    /// 决策 310：更早一页的等待有上限。超时 / 断线都收口到这里。
    member private this.FailOlderPage(message: string) =
        if OlderPageLoad.inFlight pageLoading pageRequestedAt then
            pageLoading <- false
            pageRequestedAt <- None
            v().Chat.CancelHistoryPrependAnchor()
            this.ShowOlderPageRetry message

    /// 超时检查：只在有请求在途时有事做。
    member private this.CheckOlderPageTimeout(now: DateTimeOffset) =
        if OlderPageLoad.timedOut pageLoading pageRequestedAt now (ms Timing.OlderPageTimeout) then
            this.FailOlderPage OlderPageLoad.timeoutText

    member this.JumpExchange(direction: int) = v().Chat.JumpExchange direction
    member this.ScrollToBeginning() = v().Chat.SmoothScrollToBeginning()
    member this.ScrollToEnd() = v().Chat.SmoothScrollToEnd()
    member this.CloseDrawer() = v().Workspace.SetCompactNavigation false |> ignore

    // ---- 附件流程 ----

    /// 入口在异步读取文件前取得归属快照（(instanceId, 连接代次)）。
    member this.UploadOwner() : string * int = instanceId, ports.ConnectionGeneration()

    /// 读好的文件回到组合者：逐个判归属，失败只说一次；成功走 BeginUpload。
    member this.AttachFiles (owner: string * int) (files: (string * byte[]) list) =
        let ownerInstance, ownerConnection = owner
        for (fileName, bytes) in files do
            if UploadFacts.stillOwned authenticated ownerInstance ownerConnection instanceId (ports.ConnectionGeneration()) then
                this.BeginUpload(attachmentDraft (), fileName, bytes)
            else toast UploadFacts.disconnectedText ShellToastTone.Warning

    member this.RemoveAttachment(attachmentId: Guid) =
        v().Composer.SetAttachments((attachmentDraft ()).Remove attachmentId)

    /// 决策 332：超大图片先在后台缩小再走原流程。
    member private this.BeginUpload(owner: AttachmentDraftController, fileName: string, bytes: byte[]) =
        if UploadFacts.shrinkFirst (ImageShrink.needsShrink fileName bytes) (int64 bytes.Length) then
            let ownerInstance = instanceId
            let ownerConnection = ports.ConnectionGeneration()
            toast (UploadFacts.shrinkingText fileName) ShellToastTone.Info
            async {
                let! result = ImageShrink.shrinkAsync fileName bytes
                ports.Post(fun () ->
                    if not (UploadFacts.stillOwned authenticated ownerInstance ownerConnection instanceId (ports.ConnectionGeneration())) then
                        toast UploadFacts.disconnectedText ShellToastTone.Warning
                    else
                        match result with
                        | Error message -> toast message ShellToastTone.Warning
                        | Ok None -> this.BeginUploadCore(owner, fileName, bytes, None)
                        | Ok(Some shrunk) ->
                            toast (UploadFacts.shrunkText fileName (ImageShrink.note shrunk)) ShellToastTone.Info
                            this.BeginUploadCore(owner, shrunk.fileName, shrunk.bytes, Some shrunk))
            }
            |> Async.Start
        else this.BeginUploadCore(owner, fileName, bytes, None)

    member private this.BeginUploadCore
        (owner: AttachmentDraftController, fileName: string, bytes: byte[], shrunk: ShrunkImage option)
        =
        match MediaTypes.validate fileName (int64 bytes.Length) with
        | Error message -> toast message ShellToastTone.Warning
        | Ok() ->
            let sha = UploadFacts.sha256Hex bytes
            let mediaType = MediaTypes.ofFileName fileName
            let attachmentId = Guid.CreateVersion7()
            // 缩略图解码归入口壳（IImage）；失败忽略。
            if MediaTypes.isModelImage mediaType then ports.MakeThumbnail attachmentId bytes
            shrunk |> Option.iter (fun r -> v().Composer.SetShrinkNote(attachmentId, ImageShrink.note r))
            let upload =
                { attachmentId = attachmentId
                  fileName = fileName
                  mediaType = mediaType
                  size = int64 bytes.Length
                  sha256 = sha }
            owner.Begin upload |> ignore
            this.Render()
            send (
                AttachmentBegin
                    {| attachmentId = attachmentId
                       totalBytes = int64 bytes.Length
                       sha256 = sha
                       mediaType = mediaType
                       fileName = fileName |})
            for index, dataBase64 in UploadFacts.chunks bytes do
                send (AttachmentChunk {| attachmentId = attachmentId; index = index; dataBase64 = dataBase64 |})
            send (AttachmentComplete {| attachmentId = attachmentId; sha256 = sha |})

    // ---- 导入 / 导出 ----

    /// 决策 313：导入对话。未连接时不打开（导入要写到主机上）。
    member this.ImportConversation() =
        if not authenticated then toast "连接主机后才能导入对话。" ShellToastTone.Warning
        elif not (this.RequireFeature HostFeatures.ConversationImport) then ()
        elif importDialog |> Option.exists (fun d -> d.IsOpen) then ()
        else
            let ownerInstance = instanceId
            let dialog =
                v().NewImportDesk(fun preview onDone ->
                    if not authenticated || instanceId <> ownerInstance then onDone false
                    else
                        let conversationId = Guid.CreateVersion7()
                        let config =
                            match this.MakeConversationCommand conversationId with
                            | CreateConversation d -> d.config
                            | _ -> SessionConfig.empty
                        let messages = JsonArray()
                        for m in preview.messages do messages.Add(m.DeepClone())
                        let cmd =
                            ImportConversation
                                {| invocationId = newInvocation ()
                                   conversationId = conversationId
                                   title = ConversationTransfer.importedTitle preview.title
                                   config = config
                                   messages = messages |}
                        this.SendCommandWithCompletion
                            cmd
                            (Some(sprintf "已导入「%s」，共 %d 条消息" preview.title preview.messages.Length))
                            (fun () ->
                                this.OpenConversation conversationId
                                send (ObserveConversation {| conversationId = conversationId |}))
                            onDone)
            importDialog <- Some dialog
            dialog.Show()

    member this.ExportConversation(summary: ConversationSummary) =
        if not authenticated then toast "连接主机后才能读取完整历史。" ShellToastTone.Warning
        elif not (this.RequireFeature HostFeatures.ConversationExport) then ()
        elif exportDialog |> Option.exists (fun dialog -> dialog.IsOpen) then ()
        else
            let ownerInstance = instanceId
            let dialog =
                v().NewExportDesk
                    (fun query ->
                        if authenticated && instanceId = ownerInstance then
                            ports.SendAtGeneration (ConversationExportRead query)
                        else Task.FromResult false)
                    (ConversationTransfer.lineageOf summaries lineage summary.id)
                    summary.id
                    summary.title
            exportDialog <- Some dialog
            dialog.Show()

    // ---- 预设 ----

    /// 套用预设：当前对话还是空的就直接改它的配置（不留下一段空对话），否则新建一段。
    member private this.ApplyPresetInfo(preset: PresetInfo) =
        if not authenticated then toast "连接主机后才能使用预设。" ShellToastTone.Warning
        else
            let config, note = Presets.toSessionConfig catalog preset
            let emptyActive =
                match activeConvId, activeConversation () with
                | Some convId, Some view when List.isEmpty (messagesOf view) && not (runs.Get(Some convId)).running ->
                    Some(convId, view)
                | _ -> None
            match emptyActive with
            | Some(convId, view) ->
                let command =
                    UpdateConversationConfig {| invocationId = newInvocation (); conversationId = convId; config = config |}
                this.SendCommandWithFeedback command (Some(sprintf "已套用预设「%s」" preset.name)) (fun () ->
                    view.config <- config
                    this.Render())
                v().Composer.Focus()
            | None ->
                match this.CreateConversationWith(Some config) with
                | Some _ -> toast (sprintf "已用预设「%s」新建对话" preset.name) ShellToastTone.Success
                | None -> ()
            note |> Option.iter (fun n -> toast n ShellToastTone.Warning)

    member this.ApplyPreset(preset: PresetInfo) = this.ApplyPresetInfo preset

    member this.ApplyPreset(id: string) =
        catalog.presets |> List.tryFind (fun p -> p.id = id) |> Option.iter this.ApplyPresetInfo

    /// 「存为预设…」：取当前对话的完整会话配置，名字默认是对话标题。
    member this.SaveConversationAsPreset() =
        if not (this.RequireFeature HostFeatures.Presets) then ()
        else
            match activeConversation () with
            | Some view when authenticated ->
                let title = activeSummary () |> Option.map (fun s -> s.title) |> Option.defaultValue ""
                let config = view.config
                v().Dialogs.Prompt(
                    "存为预设",
                    "预设名称",
                    Presets.suggestName catalog.presets title,
                    "保存",
                    fun name onCompleted ->
                        this.SendConfigWithFeedback
                            (sprintf "已存为预设「%s」，新建对话时可以套用" (name.Trim()))
                            (fun requestId ->
                                ConfigUpsertPreset {| requestId = requestId; preset = Presets.payloadOf None name config |})
                            onCompleted)
                |> ignore
            | _ -> toast "打开一段对话后才能存为预设。" ShellToastTone.Warning

    member this.RenamePreset(preset: PresetInfo) =
        if this.RequireFeature HostFeatures.Presets then
            v().Dialogs.Prompt(
                "重命名预设",
                "预设名称",
                preset.name,
                "保存",
                fun name onCompleted ->
                    this.SendConfigWithFeedback
                        "预设已重命名"
                        (fun requestId ->
                            ConfigUpsertPreset
                                {| requestId = requestId; preset = Presets.payloadOf (Some preset.id) name (Presets.ofPreset preset) |})
                        onCompleted)
            |> ignore

    member this.DeletePreset(preset: PresetInfo) =
        if this.RequireFeature HostFeatures.Presets then
            v().Dialogs.Confirm(
                "删除预设",
                (sprintf "「%s」将从这台主机上移除。已经用它建好的对话不受影响。" preset.name),
                "删除",
                fun () ->
                    this.SendConfigWithFeedback
                        "预设已删除"
                        (fun requestId -> ConfigDeletePreset {| requestId = requestId; presetId = preset.id |})
                        ignore)
            |> ignore

    /// 预设入口随目录同步：侧栏下拉、空态芯片、顶栏菜单。
    member private this.SyncPresetSurfaces() =
        let entries =
            catalog.presets
            |> List.map (fun p -> p.name, Presets.describe catalog p, (fun () -> this.ApplyPresetInfo p))
        v().Sidebar.SetPresets(
            entries,
            fun anchor items ->
                v().Overlay.ShowMenu(anchor, false, [ for (name, _, run) in items -> Wanxiang2.PopupMenu.PopupEntry.create name run ]))
        v().Chat.Pane.SetPresets entries
        v().Chat.SetSavePresetAction(
            if HostFeatures.supports hostFeatures HostFeatures.Presets then
                Some(fun () -> this.SaveConversationAsPreset())
            else None)

    // ---- 设置页 / 首次运行 ----

    member private this.OnboardingBusy() = v().Onboarding.IsBusy

    /// 首次运行卡片连接成功、目录已就绪：选定模型，输入框预填第一句话并聚焦。
    member private this.CompleteOnboarding() =
        match v().Onboarding.TakeConnected catalog with
        | Some(providerId, model) ->
            v().Onboarding.Reset()
            draftModelSelection <- Some(providerId, model)
            this.Render()
            if String.IsNullOrWhiteSpace (v().Composer.Text) then v().Composer.SetText OnboardingRules.FirstPrompt
            v().Composer.Focus()
            toast (sprintf "已连接 %s" (Catalog.describeModel providerId model catalog)) ShellToastTone.Success
        | None -> ()

    member this.ShowAddProvider() =
        enteredSettingsForFirstProvider <- not (Catalog.isReady catalog)
        this.ShowSettings None
        // 刚翻可见性时控件还没过布局，直接开编辑器会落空，所以延一帧。
        ports.Post(fun () -> v().Settings.OpenAddProvider())

    /// 旧 ShowSettings 与 ShellSurface.OpenSettings 的统一入口：带分区时跳到该分区。
    member this.ShowSettings(section: string option) =
        settingsReturnFocus <- Some(ports.CaptureFocus())
        v().SettingsHostVisible true
        v().WorkspaceHostVisible false
        // 打开即有焦点归宿：分区导航按钮。延一帧到布局之后。
        ports.Post(fun () -> v().SettingsFocusInitial())
        send CatalogRequest
        section |> Option.iter (fun key -> v().SettingsShowSection(Some key))

    member this.CloseSettings() =
        enteredSettingsForFirstProvider <- false
        v().SettingsHostVisible false
        v().WorkspaceHostVisible true
        this.RestoreSettingsFocus()

    /// 设置关闭回到设置入口触发点：触发控件仍在树上就还给它，否则落到输入区，绝不掉到页顶。
    member private this.RestoreSettingsFocus() =
        let fallback () =
            if v().Workspace.Host.IsVisible then v().Composer.Focus()
        match settingsReturnFocus with
        | Some restore ->
            settingsReturnFocus <- None
            ports.Post(fun () -> if not (restore ()) then fallback ())
        | None -> fallback ()

    member this.FocusSidebarSearch(query: string option) =
        let state = v().Workspace.EnsureSearchVisible()
        if not state.compactMode && prefs.sidebarCollapsed <> state.sidebarCollapsed then
            prefs <- { prefs with sidebarCollapsed = state.sidebarCollapsed }
            ports.PrefsSave prefs
        query |> Option.iter (fun text -> v().Sidebar.SetSearchText text)
        v().Sidebar.FocusSearch()

    member this.ToggleSidebar() =
        // 焦点归宿在 ShellDesk；这里只把桌面折叠状态落盘。
        let state = v().Workspace.ToggleSidebar()
        if not state.compactMode then
            prefs <- { prefs with sidebarCollapsed = state.sidebarCollapsed }
            ports.PrefsSave prefs

    // ---- 模型切换（决策 240）----

    member this.ShowModelPicker(anchor: Control) =
        match activeConvId, activeConversation () with
        | Some convId, Some view ->
            let options = ModelSwitcher.options catalog (Some(view.config.provider, view.config.model))
            if List.isEmpty options then
                // 空态不是死路：点模型入口就直接进入「添加模型服务」，与空态主按钮同一出口。
                toast "还没有可用的模型，先添加一个模型服务。" ShellToastTone.Warning
                this.ShowAddProvider()
            else
                let generating = (runs.Get(Some convId)).running
                let title =
                    if generating then "正在生成的这条回复不受影响，切换从下一条开始。"
                    else "切换本对话的模型"
                v().ModelSwitch.Show(
                    anchor,
                    false,
                    options,
                    title,
                    fun picked ->
                        let config = { view.config with provider = picked.providerId; model = picked.model }
                        let command =
                            UpdateConversationConfig
                                {| invocationId = newInvocation ()
                                   conversationId = convId
                                   config = config |}
                        this.SendCommandWithFeedback command (Some(ModelSwitcher.switchedToast picked.providerLabel picked.model)) (fun () ->
                            view.config <- config
                            this.Render()))
        | _ ->
            // 没有对话也要能挑模型：选择只落草稿暂存位，建对话时作为 config 的起手。
            let current = draftModelSelection |> Option.orElse (Catalog.defaultSelection catalog)
            let options = ModelSwitcher.options catalog current
            if List.isEmpty options then
                toast "还没有可用的模型，先添加一个模型服务。" ShellToastTone.Warning
                this.ShowAddProvider()
            else
                v().ModelSwitch.Show(
                    anchor,
                    false,
                    options,
                    "新对话使用的模型",
                    fun picked ->
                        draftModelSelection <- Some(picked.providerId, picked.model)
                        v().Composer.SetModelLabel(Catalog.describeModel picked.providerId picked.model catalog)
                        toast (sprintf "已选择 %s · %s" picked.providerLabel picked.model) ShellToastTone.Info)

    // ---- 动作记录工厂（入口壳构造各 Desk 时用）----

    /// 侧栏动作（旧 BuildSidebar 的 SidebarDeskActions）。
    member this.SidebarActions() : Wanxiang2.SidebarDesk.SidebarDeskActions =
        { newConversation = fun () -> this.CreateConversation() |> ignore
          openConversation = fun id -> this.OpenConversation id
          renameConversation = fun summary -> this.RenameConversation summary
          deleteConversation = fun summary -> this.DeleteConversation summary
          setPinned = fun summary pinned -> this.SetPinned(summary, pinned)
          setArchived = fun summary archived -> this.SetArchived(summary, archived)
          selectionAllPinned = fun ids -> this.SelectionAllPinned ids
          setPinnedMany = fun ids pin -> this.SetPinnedMany(ids, pin)
          setArchivedMany = fun ids archived -> this.SetArchivedMany(ids, archived)
          deleteMany = fun ids -> this.DeleteMany ids
          duplicateAsFork = fun summary -> this.DuplicateAsFork summary
          exportConversation = fun summary -> this.ExportConversation summary
          openSettings = fun () -> this.ShowSettings None
          reconnect = fun () -> this.ReconnectFromSidebar()
          toggleArchivedVisibility = fun () -> this.SavePrefs { prefs with showArchived = not prefs.showArchived }
          closeNavigation = fun () -> this.CloseDrawer() }

    /// 侧栏「重新连接」：已连只说一句，有令牌直接重连，否则开连接对话框。
    member this.ReconnectFromSidebar() =
        if authenticated then toast "已连接" ShellToastTone.Info
        elif lastToken.IsSome then this.Connect()
        else this.ShowConnectDialog()

    /// 卡片动作（旧 BuildChat 的 CardActions）。
    member this.CardActions() : Wanxiang2.MessageCardView.CardActions =
        { CopyText = fun text -> ports.CopyToClipboard text
          Regenerate = fun () -> this.Regenerate()
          EditAndFork = fun message -> this.EditAndFork message
          CopyLink = Some(fun commitId -> this.CopyMessageLink commitId)
          Recover = Some(fun recovery -> this.Recover recovery)
          DeleteMessage = fun commitId -> this.DeleteMessage commitId
          DownloadAttachment = fun sha -> this.DownloadAttachment sha
          OpenLink = fun url -> this.OpenUrl url }

    /// 对话屏动作（旧 BuildChat 的 ChatScreenActions）。
    member this.ChatActions() : Wanxiang2.ChatScreen.ChatScreenActions =
        { Card = this.CardActions()
          RenameTitle = fun title -> this.RenameTitle title
          ToggleSidebar = fun () -> this.ToggleSidebar()
          OpenSessionSettings = fun () -> this.ShowSessionContext()
          ForkFromHere = fun () -> this.ForkFromHere()
          ExportConversation = fun () -> activeSummary () |> Option.iter (fun summary -> this.ExportConversation summary)
          StopGeneration = fun () -> this.StopGeneration()
          RequestOlderHistory = fun () -> this.RequestOlderHistory()
          FocusHome = fun () -> v().Composer.Focus()
          OpenParent = fun id -> this.OpenConversation id
          SiblingSelected = fun target -> this.SwitchVersion target
          // 定位高亮停留是「一锤子」延时：入口的 After 是普通计时器，回来时先自停（旧 DispatcherTimer.RunOnce）。
          After = fun delay action -> afterOnce delay action }

    /// 输入区动作（旧 BuildComposer）：三个 IO 动作由入口壳给，其余在这里接。
    member this.ComposerActions
        (pickAttachment: unit -> unit, dropFiles: IStorageItem list -> unit, pasteFromClipboard: unit -> bool)
        : Wanxiang2.ComposerDesk.ComposerDeskActions =
        { Submit = fun text -> this.SendMessage text
          StopGeneration = fun () -> this.StopGeneration()
          PickAttachment = pickAttachment
          RemoveAttachment = fun id -> this.RemoveAttachment id
          OpenModelPicker = fun anchor -> this.ShowModelPicker anchor
          DropFiles = dropFiles
          PasteFromClipboard = pasteFromClipboard
          OpenSessionSettings = fun () -> this.ShowSessionContext() }

    /// 设置页钩子（旧 BuildSettings 的 SettingsHooks）。
    member this.SettingsHooks() : Wanxiang2.SettingsScreen.SettingsHooks =
        { UpsertProvider =
            fun payload onCompleted ->
                this.SendConfigWithFeedback
                    "模型服务设置已保存"
                    (fun requestId -> ConfigUpsertProvider {| requestId = requestId; provider = payload |})
                    onCompleted
          DeleteProvider =
            fun id ->
                v().Dialogs.Confirm(
                    "删除模型服务",
                    (sprintf "「%s」将从配置中移除，使用它的对话需要重新选择模型。" id),
                    "删除",
                    fun () ->
                        this.SendConfigWithFeedback
                            "模型服务已删除"
                            (fun requestId -> ConfigDeleteProvider {| requestId = requestId; providerId = id |})
                            ignore)
                |> ignore
          ProbeProvider = fun id -> this.ProbeProvider id
          UpsertMcp =
            fun payload onCompleted ->
                this.SendConfigWithFeedback
                    "MCP 设置已保存"
                    (fun requestId -> ConfigUpsertMcp {| requestId = requestId; server = payload |})
                    onCompleted
          DeleteMcp =
            fun id ->
                v().Dialogs.Confirm(
                    "删除 MCP 服务",
                    (sprintf "「%s」提供的工具将不再可用。" id),
                    "删除",
                    fun () ->
                        this.SendConfigWithFeedback
                            "MCP 服务已删除"
                            (fun requestId -> ConfigDeleteMcp {| requestId = requestId; serverId = id |})
                            ignore)
                |> ignore
          UpdateGeneration =
            fun payload onCompleted ->
                this.SendConfigWithFeedback
                    "生成设置已保存"
                    (fun requestId -> ConfigUpdateGeneration {| requestId = requestId; generation = payload |})
                    onCompleted
          SavePrefs = fun next -> this.SavePrefs next }

    /// 首次运行卡片的钩子（旧 BuildOnboarding 的 OnboardingHooks）。
    member this.OnboardingHooks() : Wanxiang2.SettingsScreen.OnboardingHooks =
        { UpsertProvider =
            fun payload onCompleted ->
                this.SendConfigWithFeedback
                    ""
                    (fun requestId -> ConfigUpsertProvider {| requestId = requestId; provider = payload |})
                    onCompleted
          DiscardProvider =
            fun id ->
                this.SendConfigWithFeedback
                    ""
                    (fun requestId -> ConfigDeleteProvider {| requestId = requestId; providerId = id |})
                    ignore
          ProbeProvider = fun id -> this.ProbeProvider id
          TakenIds = fun () -> catalog.providers |> List.map (fun p -> p.id)
          DiscoverLocal =
            fun () ->
                if OnboardingRules.canDiscoverLocal lastUrl then
                    OnboardingRules.discoverOllamaAt ProviderPresets.OllamaProbeUrl
                else Task.FromResult None
          OpenAdvanced = fun () -> this.ShowAddProvider() }

    /// 工作区钩子（旧 BuildWorkspace 的 WorkspaceHooks）。
    member this.WorkspaceHooks(prefersTouch: unit -> bool) : Wanxiang2.Workspace.WorkspaceHooks =
        { PrefersTouch = prefersTouch
          SidebarWidth = fun () -> prefs.sidebarWidth
          InitialSidebarCollapsed = prefs.sidebarCollapsed }

    // ---- 快捷键 / 命令面板（转发给 ShellCommands 组合者）----

    member this.Commands: ShellCommands =
        match shellCommands with
        | Some c -> c
        | None ->
            let c =
                ShellCommands(
                    { OverlayEscape = fun () -> v().Overlay.HandleEscape()
                      IsDialogOpen = fun () -> v().Overlay.Layer.IsDialogOpen
                      IsPopupOpen = fun () -> v().Overlay.Layer.IsPopupOpen
                      IsSettingsOpen = fun () -> v().Settings.View.IsVisible
                      IsDrawerOpen = fun () ->
                          let state = v().Workspace.State
                          state.compactMode && state.compactNavigationOpen
                      IsGenerating = fun () -> (runs.Get activeConvId).generationId.IsSome
                      HasConversation = fun () -> (activeSummary ()).IsSome
                      VisibleOrder = fun () -> v().Sidebar.GetVisibleOrder()
                      HasPreset = fun id -> catalog.presets |> List.exists (fun p -> p.id = id)
                      PaletteInput = fun () -> this.PaletteInput()
                      Theme = fun () -> prefs.theme
                      CloseSettings = fun () -> this.CloseSettings()
                      CloseDrawer = fun () -> this.CloseDrawer()
                      StopGeneration = fun () -> this.StopGeneration()
                      ToggleSidebar = fun () -> this.ToggleSidebar()
                      CloseWindow = fun () -> ports.CloseWindow()
                      NewConversation = fun () -> this.CreateConversation() |> ignore
                      FocusSearch = fun query -> this.FocusSidebarSearch query
                      OpenSettings =
                          fun key ->
                              // 找不到分区时只打开设置页。
                              let section =
                                  key
                                  |> Option.bind (fun k ->
                                      Wanxiang2.SettingsScreen.SettingsPart.all
                                      |> List.tryFind (fun s -> (string s).ToLowerInvariant() = k)
                                      |> Option.map string)
                              this.ShowSettings section
                      OpenModelPicker = fun () -> this.ShowModelPicker(v().Composer.ModelAnchor())
                      JumpExchange = fun direction -> this.JumpExchange direction
                      ScrollToBeginning = fun () -> this.ScrollToBeginning()
                      ScrollToEnd = fun () -> this.ScrollToEnd()
                      FocusComposer = fun () -> v().Composer.Focus()
                      CopyLastReply = fun () -> this.CopyLatestReply()
                      RegenerateLast = fun () -> this.RegenerateLatest()
                      TryUndo =
                          // 决策 232：只在「撤销」提示条可见、且焦点不在有字的输入框时接管。
                          fun () ->
                              let inTextBox = try ports.IsEditingText () with _ -> false
                              not inTextBox && v().Overlay.TryInvokeToastAction "撤销"
                      FocusToast = fun () -> v().Overlay.FocusToastAction()
                      ExportConversation = fun () -> activeSummary () |> Option.iter (fun s -> this.ExportConversation s)
                      OpenConversation = fun id -> this.OpenConversation id
                      ShowShortcuts = fun () -> v().Dialogs.Shortcuts() |> ignore
                      ApplyPreset = fun id -> this.ApplyPreset id
                      SavePreset = fun () -> this.SaveConversationAsPreset()
                      Fork = fun () -> this.Fork()
                      Import = fun () -> this.ImportConversation()
                      AddProvider = fun () -> this.ShowAddProvider()
                      ShowPalette = fun items recent -> v().Palette.Show(items, recent)
                      SaveRecent = fun recent -> paletteRecent <- recent
                      SaveTheme = fun theme -> this.SavePrefs { prefs with theme = theme }
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

    /// 命令面板「最近使用」的持久化接线（宿主桥）：入口启动时装入（loadRecent 读文件），
    /// 关窗时读回（saveRecent 写文件）；持久化本身归入口（文件 / localStorage）。
    member this.SetPaletteRecent(recent: string list) = paletteRecent <- recent

    member this.PaletteRecent() : string list = paletteRecent

    member this.RunPaletteItem(item: PaletteItem, query: string) = this.Commands.RunPaletteItem(item, query)

    member this.HandleShortcut(shortcut: ShortcutAction) : bool = this.Commands.HandleShortcut shortcut

    member this.ShowCommandPalette() = this.Commands.ShowPalette()

    /// 快捷键说明对话框（旧 Dialogs.shortcuts）。
    member this.ShowShortcuts() = v().Dialogs.Shortcuts() |> ignore

    // ---- 协议分发（模型部分）----

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
                      Apply = fun ev -> this.ApplyHostFace ev })
            protocolDispatch <- Some d
            d

    member private this.ObserveConversationList() =
        // 请求一经发出就置侧栏加载中；快照到达时由 Sidebar.SetConversations 解除。
        v().Sidebar.SetConversationListLoading true
        send ObserveConversationList

    // ---- 组装 ----

    /// 入口壳在视图树挂载后调用：组装视图、接事件、启动周期计时。
    /// 返回的 IDisposable 在卸载时调用（停计时、落草稿）。
    member this.Build() : IDisposable =
        views <- Some(ports.BuildViews())
        // 启动就把折行与动效默认值交给渲染侧（旧 Build 开头：ApplyTheme）。
        this.ApplyTheme()
        ports.SetReducedMotion prefs.reduceMotion
        ports.SetCodeWrap prefs.codeWrap
        // 删除协作：视图组装后构造（surface 引用视图）。
        deletion <-
            Some(
                DeletionDesk(
                    commandFeedback,
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
                      SendCommand = fun cmd -> this.SendCommand cmd
                      NewInvocation = newInvocation }))
        let subs = ResizeArray<IDisposable>()
        // 浮层：提示条焦点归宿 + 点空白关浮层；对话框焦点恢复由设置页接管。
        v().Overlay.SetToastFocusHome(fun () -> v().Composer.Focus())
        v().Overlay.WireDismiss()
        // 侧栏：导入入口（新侧栏为 no-op）、命令面板入口、搜索与版本、归档可见性。
        v().Sidebar.ConfigureImport(fun () -> this.ImportConversation())
        v().Sidebar.ConfigurePalette(fun () -> this.ShowCommandPalette())
        v().Palette.Run.Add(fun (item, query) -> this.RunPaletteItem(item, query))
        v().Sidebar.SearchHandler <-
            Some(fun query includeArchived offset ->
                match MessageLink.tryParse query with
                | Some link ->
                    v().Sidebar.ResetSearch()
                    this.OpenMessageLink link
                | None ->
                    let queryId = Guid.CreateVersion7()
                    search.Sent(queryId, DateTimeOffset.UtcNow)
                    send (
                        SearchQuery
                            { queryId = queryId
                              query = query
                              includeArchived = includeArchived
                              offset = offset
                              limit = SearchLimits.defaultPageSize }))
        v().Sidebar.OpenSearchHit <- fun hit -> this.OpenSearchHit hit
        v().Sidebar.SetShowArchived prefs.showArchived
        // 设置页：钩子、关闭、检查主机、本机草稿、设备、导入与预设。
        v().Settings.Bind(this.SettingsHooks())
        v().Settings.CloseRequested.Add(fun () -> this.CloseSettings())
        v().Settings.Health.CheckRequested.Add(fun () ->
            if authenticated && ports.IsConnected() then
                send (DoctorRequest {| requestId = newInvocation () |})
                doctorWait.Begin DateTimeOffset.UtcNow
                // 旧主机不认识这个事件会直接忽略，所以要有超时说明。
                afterOnce doctorWait.Limit (fun () ->
                    if doctorWait.TimeOut() then
                        match v().Settings.Health.Doctor with
                        | Running -> v().Settings.Health.SetUnavailable BoundedWaits.doctorTimeoutText
                        | _ -> ())
                |> ignore
            else
                v().Settings.Health.SetUnavailable BoundedWaits.doctorOfflineText)
        v().Settings.LocalDrafts.ClearRequested.Add(fun () ->
            v().Dialogs.Confirm(
                "清除本地草稿",
                "这台设备上保存的未发送文字、附件名称和未确认的消息会被删除，无法恢复。已发送并保存的对话不受影响。",
                "清除",
                fun () -> this.ClearLocalDrafts())
            |> ignore)
        this.UpdateDraftSummary()
        v().Settings.Devices.ListRequested.Add(fun () -> send DevicesRequest)
        v().Settings.Devices.InviteRequested.Add(fun () -> send PairingInvite)
        v().Settings.Devices.RenameRequested.Add(fun (deviceId, name) ->
            this.SendConfigWithFeedback
                "设备已重命名"
                (fun requestId -> DeviceRename {| requestId = requestId; deviceId = deviceId; name = name |})
                ignore)
        v().Settings.Devices.RevokeRequested.Add(fun device ->
            v().Dialogs.Confirm(
                "吊销设备",
                (sprintf "「%s」会立即断开，之后需要重新配对才能再连这台主机。" device.name),
                "吊销",
                fun () ->
                    this.SendConfigWithFeedback
                        "设备已吊销"
                        (fun requestId -> DeviceRevoke {| requestId = requestId; deviceId = device.id |})
                        ignore)
            |> ignore)
        v().Settings.ConfigureImport(fun () ->
            this.CloseSettings()
            this.ImportConversation())
        v().Settings.ConfigurePresets((fun p -> this.RenamePreset p), (fun p -> this.DeletePreset p))
        // 对话屏：同位置版本、空态起手式、存为预设入口。
        v().Chat.SetSiblingProvider(fun commit prev ->
            match activeConvId with
            | Some convId -> ForkLineage.siblings summaries lineage convId commit prev
            | None -> None)
        v().Chat.Pane.SetStarterPick(Some(fun seed -> v().Composer.SetText seed))
        v().Composer.SetEnterSends prefs.enterSends
        // 光标推进：把本地游标回执发回主机。
        state.CursorChanged.Add(fun _ -> send (state.CursorAdvancedEvent()))
        // 协议事件与连接关闭：入口负责代次过滤与 UI 线程 marshal。
        ports.SubscribeHostEvents(fun ev -> this.HandleEvent ev) |> subs.Add
        ports.SubscribeClosed(fun detail -> this.HandleConnectionClosed detail) |> subs.Add
        // 侧栏宽度：拖动立刻落盘（旧 BuildWorkspace 的 SidebarResized）。
        v().Workspace.SidebarResized.Add(fun width ->
            prefs <- { prefs with sidebarWidth = width }
            ports.PrefsSave prefs)
        this.Render()
        v().Workspace.ApplyResponsiveLayout()
        // 周期计时：投递确认 / 请求超时 / 草稿自动保存 / 更早一页看门狗 / 视口轮询。
        let delivery =
            ports.Every (ms Timing.OutboxDeliveryCheckTick) (fun () ->
                let now = DateTimeOffset.UtcNow
                // 决策 443：旧主机静默丢弃它不认识的请求——到点就让界面退出等待并说明原因。
                let expired = this.ExpireHostRequests now
                if outbox.Expire(now, ms Timing.OutboxConfirmTimeout) || expired then this.Render())
        let draftsTimer = ports.Every (TimeSpan.FromMilliseconds 800.0) (fun () -> this.FlushDrafts())
        let viewport = ports.Every (ms Timing.HostViewportPoll) (fun () -> this.TickViewport())
        let watchdog = ports.Every (ms Timing.OlderPageCheckTick) (fun () ->
            this.CheckOlderPageTimeout DateTimeOffset.UtcNow)
        // 决策 451：PWA 页面转入后台时同样立刻落草稿。
        ports.BrowserAddHidden(fun () -> this.FlushDrafts())
        { new IDisposable with
            member _.Dispose() =
                this.FlushDrafts()
                for sub in subs do sub.Dispose()
                delivery.Dispose()
                draftsTimer.Dispose()
                viewport.Dispose()
                watchdog.Dispose() }

    // ---- 自动连接 / 草稿装载 ----

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
                // 凭据 IO（文件 / IndexedDB）在后台完成；落地与视图刷新触碰控件，必须回到 UI 线程。
                ports.Post(fun () ->
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
        this.UpdateDraftSummary()
