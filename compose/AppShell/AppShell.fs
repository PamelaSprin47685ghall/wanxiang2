namespace Wanxiang.UI

open System
open System.Threading.Tasks
open Avalonia.Controls
open Avalonia.Platform.Storage
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang2.WorkspaceState
open Wanxiang2.MotionSwitch

[<RequireQualifiedAccess>]
type ShellToastTone =
    | Info
    | Success
    | Warning
    | Failure

type ShellPorts =
    { Connect: string -> Async<int>
      SendAuth: int -> WireEvent -> unit
      Send: WireEvent -> unit
      SendCommand: ClientCommand -> Task<bool>
      SendAtGeneration: WireEvent -> Task<bool>
      After: TimeSpan -> (unit -> unit) -> IDisposable
      Every: TimeSpan -> (unit -> unit) -> IDisposable
      Post: (unit -> unit) -> unit
      Toast: string -> ShellToastTone -> unit
      ToastUndo: string -> (unit -> unit) -> unit
      CopyToClipboard: string -> unit
      OpenLink: string -> unit
      SaveDownload: string -> byte[] -> unit
      ApplyDraftWrite: DraftWrite -> unit
      PrefsLoad: unit -> UiPrefs
      PrefsSave: UiPrefs -> unit
      TryLoadClientToml: unit -> (string * string) option
      TryLoadBrowserConnection: unit -> Async<(string * string * string) option>
      SaveBrowserConnection: string -> string -> string -> string -> unit
      BrowserViewportWidth: unit -> float
      BrowserResumeEpoch: unit -> int
      BrowserPageUrl: unit -> string
      BrowserConversationSucceeded: unit -> unit
      BrowserAddHidden: (unit -> unit) -> unit
      SystemPrefersDark: unit -> bool
      DefaultServerUrl: unit -> string
      ApplyTheme: bool -> unit
      SetReducedMotion: bool -> unit
      SetCodeWrap: bool -> unit
      CaptureFocus: unit -> (unit -> bool)
      IsEditingText: unit -> bool
      ConnectionGeneration: unit -> int
      IsConnected: unit -> bool
      SubscribeHostEvents: (WireEvent -> unit) -> IDisposable
      SubscribeClosed: (string option -> unit) -> IDisposable
      CloseWindow: unit -> bool
      ClientName: unit -> string
      MakeThumbnail: Guid -> byte[] -> unit
      EffectSink: Wanxiang.UI.EffectSink
      BuildViews: unit -> ShellViews }

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
      Overlay: Wanxiang2.OverlayDesk.OverlayDesk
      Dialogs: Wanxiang2.DialogsDesk.DialogsDesk
      NewExportDesk: (ConversationExportQuery -> Task<bool>) -> ExportLineage -> Guid -> string -> Wanxiang2.TransferDesk.ExportDesk
      NewImportDesk: (ImportPreview -> (bool -> unit) -> unit) -> Wanxiang2.TransferDesk.ImportDesk
      SettingsHostVisible: bool -> unit
      WorkspaceHostVisible: bool -> unit
      SettingsFocusInitial: unit -> unit
      SettingsShowSection: string option -> unit }

/// 组合者：MVU 组合核心
type ShellComposer(ports: ShellPorts) =
    let mutable model: WorkspaceModel = Workspace.init ()
    let mutable views: ShellViews option = None
    let mutable protocolDispatch: ProtocolDispatch option = None
    let mutable shellCommands: ShellCommands option = None

    // 账本状态参与者（由 ProtocolDispatch 处理）
    let state = ClientState()
    let outbox = MessageOutbox()
    let downloads = DownloadBuffers()
    let search = SearchInFlight()
    let probes = PendingProbes()
    let runs = ConversationRuns()
    let commandFeedback = CommandFeedbackTracker()
    let configFeedback = ConfigFeedbackTracker()
    /// 「检查主机」的有界等待（30 秒）与在途请求 id：报告只认这次发出的请求。
    let doctorWait = BoundedWait(BoundedWaits.doctorLimit)
    let mutable doctorRequestId: Guid option = None
    /// 上一次看到的主机实例：换主机时上一台主机的检查结果与配置问题都不属于新主机。
    let mutable lastInstanceId = ""

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

    let ms (t: Timing) = TimeSpan.FromMilliseconds (MotionTimings.initialMs t)

    /// 主机能力表（HostCapabilities 到达后才有；未知 = 一律视为支持）
    let mutable hostFeatures: Set<string> option = None

    /// 发给主机的配置写入：request → ConfigApplied 的有界确认（回执与提示由 ProtocolDispatch 结算）。
    let sendConfig (successMessage: string) (mk: Guid -> WireEvent) (onCompleted: bool -> unit) : unit =
        let requestId = Guid.NewGuid()
        configFeedback.Track(requestId, successMessage, onCompleted)
        ports.Send (mk requestId)

    let noResponse () = HostFeatures.noResponseMessage hostFeatures


    /// 已 Build 之后的界面访问：事件回调都发生在 Build 之后（订阅就在 Build 里）。
    let v () : ShellViews =
        match views with
        | Some built -> built
        | None -> failwith "AppShell: 界面尚未构建（事件早于 Build）"

    member _.Model with get () = model

    member this.Dispatch(msg: Msg) : unit =
        let now = DateTimeOffset.UtcNow
        let next, effects = Workspace.update now msg model
        model <- next
        this.ReprojectInternal effects

    member this.Reproject() : unit =
        this.ReprojectInternal []

    member private this.ReprojectInternal(effects: Effect list) : unit =
        match views with
        | None ->
            ports.EffectSink.Run effects
        | Some v ->
            let vs = Workspace.project model
            // 1. Workspace
            v.Workspace.Render vs.Shell
            // Overlay & Toasts
            v.Overlay.Render vs.Overlay
            v.Overlay.RenderToasts vs.Toasts

            // 2. Chat / Header
            let activeConvInfo =
                match model.Conn with
                | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, run))) -> Some (conv, run)
                | _ -> None
            let activeConvId = activeConvInfo |> Option.map (fun (c, _) -> c.ConversationId)

            match activeConvInfo with
            | None ->
                v.Chat.Pane.Render vs.Chat
                v.Chat.Header.Render vs.Header
                v.Chat.Render
                    { Messages = []
                      Streaming = None
                      Error = None
                      FontSize = model.Prefs.fontScale
                      Title = Some "新对话"
                      CanEditTitle = false
                      IsGenerating = false
                      GeneratingText = ""
                      CanStop = false
                      ConversationChrome = false
                      CompactMode = vs.Shell.Compact
                      AutoCollapseReasoning = model.Prefs.autoCollapseReasoning
                      ReducedMotion = Wanxiang.UI.SharedTheme.manager.Motion.IsReduced
                      Usage = None
                      MissingAttachments = Set.empty }
            | Some (conv, run) ->
                v.Chat.Pane.Render vs.Chat
                v.Chat.Header.Render vs.Header
                let isGenerating, streamMsg, runError =
                    match run with
                    | RunState.Generating(_, _, partial) -> true, partial, None
                    | RunState.Idle err -> false, None, err
                let retry () = this.Dispatch(User(Chat(ChatUserMsg.Regenerate)))
                v.Chat.Render
                    { Messages = conv.Messages
                      Streaming = streamMsg
                      Error = runError |> Option.map (fun e -> e, retry)
                      FontSize = model.Prefs.fontScale
                      Title = Some conv.Title
                      CanEditTitle = true
                      IsGenerating = isGenerating
                      GeneratingText = (if isGenerating then "生成中" else "")
                      CanStop = isGenerating
                      ConversationChrome = true
                      CompactMode = vs.Shell.Compact
                      AutoCollapseReasoning = model.Prefs.autoCollapseReasoning
                      ReducedMotion = Wanxiang.UI.SharedTheme.manager.Motion.IsReduced
                      Usage = None
                      MissingAttachments = Set.empty }

            // 3. Composer
            v.Composer.Render vs.Composer

            // 4. Sidebar：SidebarVS 是唯一输入（行、活动对话、归档可见性、主机地址都在投影里）
            v.Sidebar.Render vs.Sidebar

            // 5. Settings
            match vs.Settings with
            | Some settingsVS ->
                v.SettingsHostVisible true
                v.WorkspaceHostVisible false
                v.Settings.Render settingsVS
            | None ->
                v.SettingsHostVisible false
                v.WorkspaceHostVisible true

            // 6. Approval
            v.Approval.Render(vs.Approval, activeConvId)

            // 6.5 引导：目录就绪后有两种情形——卡片刚接上的服务取走（交接），或还没有可用服务（卡片留在聊天区）
            match model.Conn with
            | ConnPhase.Authed(_, CatalogPhase.Ready(cat, _)) ->
                if Catalog.isReady cat then
                    // 交接：卡片刚接上的模型服务已在目录里可用（只取一次；旧 CompleteOnboarding）
                    match v.Onboarding.TakeConnected cat with
                    | Some(providerId, modelId) ->
                        v.Onboarding.Reset()
                        if String.IsNullOrWhiteSpace model.Composer.Text then
                            // 空闲输入框：预填第一句话并聚焦（旧 CompleteOnboarding 的交接）
                            this.Dispatch(User(Composer(ComposerUserMsg.TextChanged OnboardingRules.FirstPrompt)))
                        v.Composer.Focus()
                        ports.EffectSink.ShowToast(
                            sprintf "已连接 %s" (Catalog.describeModel providerId modelId cat), ToastTone.Info)
                    | None -> ()
                else
                    // 引导卡：三项在途事实归模型，这里按单一入口投影回去
                    v.Onboarding.Render
                        { SelectedPresetKey = model.Onboarding.SelectedPresetKey
                          DiscoveredModels = model.Onboarding.DiscoveredModels
                          SwitchOffered = model.Onboarding.SwitchOffered }
                    v.Chat.Pane.ShowEmbedded(v.Onboarding.View)
                    v.Onboarding.StartDiscovery()
            | _ -> ()

            // 7. Run effects
            ports.EffectSink.Run effects

    member private this.DispatchService: ProtocolDispatch =
        match protocolDispatch with
        | Some d -> d
        | None ->
            let d =
                ProtocolDispatch(
                    { State = state; Runs = runs; Outbox = outbox; Commands = commandFeedback
                      Configs = configFeedback; Downloads = downloads; Search = search; Probes = probes },
                    { ActiveConversation = fun () ->
                        match model.Conn with
                        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) -> Some conv.ConversationId
                        | _ -> None
                      InstanceId = fun () ->
                        match model.Conn with
                        | ConnPhase.Authed(inst, _) -> inst
                        | _ -> ""
                      Send = ports.Send
                      Render = fun () -> this.Reproject()
                      Toast = fun text tone ->
                          let t =
                              match tone with
                              | DispatchTone.Info -> ToastTone.Info
                              | DispatchTone.Success -> ToastTone.Success
                              | DispatchTone.Warning -> ToastTone.Warning
                              | DispatchTone.Failure -> ToastTone.Failure
                          ports.EffectSink.ShowToast(text, t)
                      ObserveList = fun () -> ports.Send WireEvent.ObserveConversationList
                      DispatchPending = fun item ->
                          this.Dispatch(Msg.WireRecv(WireEvent.Command item.command))
                      Downloaded = fun name bytes -> ports.SaveDownload name bytes
                      MissingAttachment = fun sha -> ports.EffectSink.ShowToast((sprintf "缺少附件：%s" sha), ToastTone.Warning)
                      Apply = fun ev ->
                          this.Dispatch(Msg.WireRecv ev)
                          match ev with
                          | CatalogSnapshot _ ->
                              // 引导交接在投影里做（目录可用即可取走），这里只留启动打点
                              if StartupTrace.enabled then
                                  ports.Post(fun () ->
                                      if StartupTrace.mark "catalogReady" && StartupTrace.exitAfter then StartupTrace.requestExit ())
                          | ProviderProbeResult d ->
                              // 探活回执先到引导卡；它没在等，再由设置页接走
                              let models = ProbeModels.ofJson d.models
                              if not (v().Onboarding.ApplyProbe(d.providerId, d.ok, models, d.error)) then
                                  v().Settings.ApplyProbe(d.providerId, d.ok, models, d.error)
                          | SearchResults d ->
                              // 迟到的旧结果已由 ProtocolDispatch 丢掉；这里是权威投递
                              v().Sidebar.ApplySearchResults(
                                  { queryId = d.queryId
                                    query = d.query
                                    hits = SearchHit.parse d.items
                                    hasMore = d.hasMore
                                    truncated = d.truncated },
                                  d.offset)
                          | SearchFailed d ->
                              v().Sidebar.ApplySearchFailure(v().Sidebar.SearchQueryText, d.message)
                          | HostCapabilities d ->
                              hostFeatures <- Some(Set.ofList d.features)
                          | HostConfigRejected d ->
                              v().Settings.Health.ApplyConfigIssue(d.summary, d.errors)
                          | ConfigChanged _ ->
                              v().Settings.Health.ClearConfigIssue() |> ignore
                          | DoctorReport d ->
                              // 只认这次发出的请求的报告：迟到的旧报告丢掉。
                              match doctorRequestId with
                              | Some rid when rid = d.requestId ->
                                  doctorRequestId <- None
                                  doctorWait.Settle() |> ignore
                                  v().Settings.Health.ApplyReport(d.ok, DoctorCheck.parseChecks d.checks)
                              | _ -> ()
                          | ConversationListSnapshot _ ->
                              if StartupTrace.mark "listReady" && StartupTrace.exitAfter then
                                  afterOnce (TimeSpan.FromSeconds 3.0) (fun () -> StartupTrace.requestExit ()) |> ignore
                          | ConversationSnapshot _ ->
                              if StartupTrace.enabled then
                                  ports.Post(fun () ->
                                      if StartupTrace.mark "chatReady" && StartupTrace.exitAfter then StartupTrace.requestExit ())
                          | _ -> () })
            protocolDispatch <- Some d
            d

    member this.HandleEvent(ev: WireEvent) : unit =
        this.DispatchService.Handle ev
        this.Dispatch(Msg.WireRecv ev)

    member this.HandleConnectionClosed(reason: string option) : unit =
        let issue = reason |> Option.map (fun r -> ConnectionIssue.Disconnected r)
        // 在途请求随连接作废：编辑器退出 pending，设备分区说明「读取时连接断开」
        commandFeedback.RejectAll()
        configFeedback.RejectAll()
        doctorRequestId <- None
        doctorWait.Settle() |> ignore
        match views with
        | Some vs' ->
            vs'.Settings.Devices.ApplyDisconnected()
            // 断线时进行中的检查转成说明，不留在「检查中」。
            vs'.Settings.Health.Disconnected()
        | None -> ()
        this.Dispatch(Msg.ConnectionDown issue)

    member this.Commands: ShellCommands =
        match shellCommands with
        | Some c -> c
        | None ->
            let c =
                ShellCommands(
                    { OverlayEscape = fun () ->
                          this.Dispatch(User(Overlay(OverlayUserMsg.Escape)))
                          true
                      IsDialogOpen = fun () ->
                          match model.Overlay with
                          | OverlayPhase.DialogOpen _ -> true
                          | _ -> false
                      IsPopupOpen = fun () ->
                          match model.Overlay with
                          | OverlayPhase.PopupOpen _ -> true
                          | _ -> false
                      IsSettingsOpen = fun () ->
                          model.Settings.Open
                      IsDrawerOpen = fun () -> model.Nav.DrawerOpen
                      IsGenerating = fun () ->
                          match model.Conn with
                          | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, RunState.Generating _))) -> true
                          | _ -> false
                      HasConversation = fun () ->
                          match model.Conn with
                          | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active _)) -> true
                          | _ -> false
                      VisibleOrder = fun () -> model.Summaries |> List.map (fun s -> string s.id)
                      HasPreset = fun _ -> false
                      PaletteInput = fun () ->
                          { hasConversation = false
                            presets = []
                            supportsPresets = false
                            supportsImport = false
                            themeLabel = "跟随系统"
                            sections = []
                            conversations = []
                            showArchived = false }
                      Theme = fun () -> ThemePreference.FollowSystem
                      CloseSettings = fun () -> this.Dispatch(User(Settings(SettingsUserMsg.Close)))
                      CloseDrawer = fun () -> this.Dispatch(User(Overlay(OverlayUserMsg.Escape)))
                      StopGeneration = fun () -> this.Dispatch(User(Composer(ComposerUserMsg.StopGeneration)))
                      ToggleSidebar = fun () -> this.Dispatch(User(Header(HeaderUserMsg.ToggleSidebar)))
                      CloseWindow = fun () -> ports.CloseWindow ()
                      NewConversation = fun () -> this.Dispatch(User(Sidebar(SidebarUserMsg.NewConversation)))
                      FocusSearch = fun _ -> this.Dispatch(User(Sidebar(SidebarUserMsg.SearchActivate)))
                      OpenSettings = fun s -> this.Dispatch(User(Settings(SettingsUserMsg.OpenSection s)))
                      OpenModelPicker = fun () -> this.Dispatch(User(ModelSwitch(ModelSwitchUserMsg.Open)))
                      JumpExchange = fun direction ->
                          match views with
                          | Some v -> v.Chat.JumpExchange direction
                          | None -> ()
                      ScrollToBeginning = fun () -> this.Dispatch(User(Chat(ChatUserMsg.JumpBeginning)))
                      ScrollToEnd = fun () -> this.Dispatch(User(Chat(ChatUserMsg.JumpEnd)))
                      FocusComposer = fun () -> ports.EffectSink.Focus FocusTarget.ComposerInput
                      CopyLastReply = fun () -> this.Dispatch(User(Chat(ChatUserMsg.CopyLatestReply)))
                      RegenerateLast = fun () -> this.Dispatch(User(Chat(ChatUserMsg.Regenerate)))
                      TryUndo = fun () ->
                          let inTextBox = try ports.IsEditingText () with _ -> false
                          if not inTextBox then
                              match model.Feedback.PendingDeletions |> Map.toList |> List.tryLast with
                              | Some(token, _) ->
                                  this.Dispatch(User(Overlay(OverlayUserMsg.ToastUndo (string token))))
                                  true
                              | None ->
                                  match views with
                                  | Some v -> v.Overlay.TryInvokeToastAction "撤销"
                                  | None -> false
                          else false
                      FocusToast = fun () ->
                          match views with
                          | Some v -> v.Overlay.FocusToastAction()
                          | None -> false
                      ExportConversation = fun () ->
                          match model.Conn with
                          | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) ->
                              this.ExportConversation(conv.ConversationId)
                          | _ ->
                              ports.EffectSink.ShowToast("连接主机后才能读取完整历史。", ToastTone.Warning)
                      OpenConversation = fun id -> this.Dispatch(User(Sidebar(SidebarUserMsg.SelectConv id)))
                      ShowShortcuts = fun () ->
                          match views with
                          | Some v -> v.Dialogs.Shortcuts() |> ignore
                          | None -> ()
                      ApplyPreset = fun id ->
                          match model.Conn with
                          | ConnPhase.Authed(_, CatalogPhase.Ready(cat, _)) ->
                              cat.presets |> List.tryFind (fun p -> p.id = id)
                              |> Option.iter (fun p ->
                                  let cfg, _ = Presets.toSessionConfig cat p
                                  match model.Conn with
                                  | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(c, _))) ->
                                      let invId = Guid.NewGuid()
                                      let cmd = UpdateConversationConfig {| invocationId = invId; conversationId = c.ConversationId; config = cfg |}
                                      this.Dispatch(Msg.WireRecv(WireEvent.Command cmd))
                                  | _ -> ())
                          | _ -> ports.EffectSink.ShowToast("连接主机后才能使用预设。", ToastTone.Warning)
                      SavePreset = fun () ->
                          match model.Conn with
                          | ConnPhase.Authed(_, CatalogPhase.Ready(cat, WorkspacePhase.Active(conv, _))) ->
                              let title = conv.Title
                              match views with
                              | Some v ->
                                  v.Dialogs.Prompt(
                                      "存为预设",
                                      "预设名称",
                                      Presets.suggestName cat.presets title,
                                      "保存",
                                      fun name onCompleted ->
                                          let reqId = Guid.NewGuid()
                                          let cfgPayload = Presets.payloadOf None name SessionConfig.empty
                                          ports.Send (ConfigUpsertPreset {| requestId = reqId; preset = cfgPayload |})
                                          onCompleted true) |> ignore
                              | None -> ()
                          | _ -> ports.EffectSink.ShowToast("打开一段对话后才能存为预设。", ToastTone.Warning)
                      Fork = fun () ->
                          this.ForkFromHere()
                      Import = fun () ->
                          this.ImportConversation()
                      AddProvider = fun () ->
                          this.Dispatch(User(Settings(SettingsUserMsg.OpenSection (Some "providers"))))
                      ShowPalette = fun items recent ->
                          match views with
                          | Some v -> v.Palette.Show(items, recent)
                          | None -> ()
                      SaveRecent = fun recent -> this.Dispatch(User(Palette(PaletteUserMsg.SetRecent recent)))
                      SaveTheme = fun theme ->
                          let next = { model.Prefs with theme = theme }
                          ports.EffectSink.PrefsSave next
                      Toast = fun text tone ->
                          let t = match tone with Wanxiang.UI.ShellTone.Warning -> ToastTone.Warning | _ -> ToastTone.Info
                          ports.EffectSink.ShowToast(text, t) },
                    model.PaletteRecent)
            shellCommands <- Some c
            c

    member this.HandleShortcut(shortcut: ShortcutAction) : bool =
        this.Commands.HandleShortcut shortcut

    member this.ApplyPaletteRecent(recent: string list) = this.Dispatch(User(Palette(PaletteUserMsg.SetRecent recent)))
    member this.PaletteRecent() : string list = model.PaletteRecent

    member this.Build() : IDisposable =
        let built = ports.BuildViews()
        views <- Some built
        let subs = ResizeArray<IDisposable>()
        ports.SubscribeHostEvents(fun ev -> this.HandleEvent ev) |> subs.Add
        ports.SubscribeClosed(fun detail -> this.HandleConnectionClosed detail) |> subs.Add

        // 侧栏：正文搜索的出口（查询词 → 线协议）与命中打开（打开对话 + 定位消息）。
        built.Sidebar.SearchHandler <-
            Some(fun query includeArchived offset ->
                match MessageLink.tryParse query with
                | Some(convId, commitId) ->
                    built.Sidebar.ResetSearch()
                    this.Dispatch(User(Sidebar(SidebarUserMsg.SearchHitSelect(convId, Some(string commitId)))))
                | None ->
                    let queryId = Guid.CreateVersion7()
                    search.Sent(queryId, DateTimeOffset.UtcNow)
                    ports.Send(
                        WireEvent.SearchQuery
                            { queryId = queryId
                              query = query
                              includeArchived = includeArchived
                              offset = offset
                              limit = SearchLimits.defaultPageSize }))
        built.Sidebar.OpenSearchHit <- fun hit ->
            this.Dispatch(User(Sidebar(SidebarUserMsg.SearchHitSelect(hit.conversationId, Some(string hit.messageId)))))

        // 设置页：关闭与各设备操作都换成模型消息；吊销前须经确认对话框。
        built.Settings.Bind(this.SettingsHooks())
        built.Settings.CloseRequested.Add(fun () -> this.Dispatch(User(Settings(SettingsUserMsg.Close)))) |> ignore
        built.Settings.Devices.ListRequested.Add(fun () -> this.Dispatch(User(Devices(DeviceUserMsg.LoadDevices)))) |> ignore
        built.Settings.Devices.InviteRequested.Add(fun () -> this.Dispatch(User(Devices(DeviceUserMsg.RequestInvite)))) |> ignore
        built.Settings.Devices.RenameRequested.Add(fun (deviceId, name) ->
            this.Dispatch(User(Devices(DeviceUserMsg.RenameDevice(deviceId, name))))) |> ignore
        built.Settings.Devices.RevokeRequested.Add(fun device ->
            built.Dialogs.Confirm(
                "吊销设备",
                (sprintf "「%s」会立即断开，之后需要重新配对才能再连这台主机。" device.name),
                "吊销",
                fun () -> this.Dispatch(User(Devices(DeviceUserMsg.RevokeDevice device.id))))
            |> ignore)
        // 导入与预设入口（决策 363：导入归设置页）
        built.Settings.WireImport(fun () ->
            this.Dispatch(User(Settings(SettingsUserMsg.Close)))
            this.ImportConversation())
        built.Settings.WirePresets(this.RenamePreset, this.DeletePreset)

        let ticker = ports.Every (TimeSpan.FromMilliseconds 250.0) (fun () ->
            this.Dispatch(MsgTick DateTimeOffset.UtcNow))
        // 决策 443/444：石沉大海的配置/命令/探活请求统一在计时器里收尾（界面提示归桌面壳订阅）
        let expiry = ports.Every (ms Timing.OutboxDeliveryCheckTick) (fun () ->
            let now = DateTimeOffset.UtcNow
            let timeout = ms Timing.HostRequestTimeout
            // 换主机：上一台主机的检查结果与配置问题都不属于新主机。
            let instanceId =
                match model.Conn with
                | ConnPhase.Authed(inst, _) -> inst
                | _ -> ""
            if instanceId <> "" && instanceId <> lastInstanceId then
                lastInstanceId <- instanceId
                doctorRequestId <- None
                doctorWait.Settle() |> ignore
                v().Settings.Health.Reset()
            // 检查主机超时：主机没给报告就别一直显示「正在检查」。
            if doctorWait.Tick now then
                doctorRequestId <- None
                v().Settings.Health.ApplyUnavailable(BoundedWaits.doctorTimeoutText)
            let expiredConfig = configFeedback.Expire(now, timeout)
            let expiredCommands = commandFeedback.Expire(now, timeout)
            for f in expiredConfig do f.onCompleted false
            for f in expiredCommands do f.onCompleted false
            if not (List.isEmpty expiredConfig && List.isEmpty expiredCommands) then
                ports.EffectSink.ShowToast(noResponse (), ToastTone.Warning)
            if search.Expire(now, timeout) then
                v().Sidebar.ApplySearchFailure(v().Sidebar.SearchQueryText, noResponse ())
            for (_, pid) in probes.Expire(now, timeout) do
                let err = Some(noResponse ())
                if not (v().Onboarding.ApplyProbe(pid, false, [], err)) then
                    v().Settings.ApplyProbe(pid, false, [], err))
        this.Reproject()
        { new IDisposable with
            member _.Dispose() =
                ticker.Dispose()
                expiry.Dispose()
                for sub in subs do sub.Dispose() }

    member this.AutoConnect() =
        match ports.TryLoadClientToml() with
        | Some(url, token) ->
            this.Dispatch(Msg.ConnectionRequested url)
            async {
                try
                    let! epoch = ports.Connect url
                    ports.SendAuth epoch (AuthPresent {| token = token |})
                with _ -> ()
            } |> Async.Start
        | None -> ()

    member this.ForkFromHere() =
        match model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active _)) ->
            let seed = ""
            this.ForkFrom(None, seed)
        | _ -> ports.EffectSink.ShowToast(ForkAnchor.noConversationText, ToastTone.Warning)

    member this.ForkFrom(commitId: uint64 option, seedText: string) =
        match model.Conn with
        | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) ->
            match views with
            | Some v ->
                v.Dialogs.EditText(
                    "编辑并分叉",
                    seedText,
                    "创建分叉",
                    fun edited ->
                        let newId = Guid.CreateVersion7()
                        let invId = Guid.NewGuid()
                        let editedMsg = ForkAnchor.editedMessage edited
                        let cmd =
                            ForkConversation {|
                                invocationId = invId
                                conversationId = newId
                                parentConversationId = conv.ConversationId
                                forkAfterId = commitId
                                config = SessionConfig.empty
                                editedMessageJson = editedMsg
                            |}
                        this.Dispatch(Msg.WireRecv(WireEvent.Command cmd))
                        this.Dispatch(User(Sidebar(SidebarUserMsg.SelectConv newId)))
                        ports.EffectSink.Focus FocusTarget.ComposerInput) |> ignore
            | None -> ()
        | _ -> ports.EffectSink.ShowToast(ForkAnchor.noConversationText, ToastTone.Warning)

    member this.ImportConversation() =
        match model.Conn with
        | ConnPhase.Authed _ ->
            match views with
            | Some viewsDesk ->
                let desk = viewsDesk.NewImportDesk(fun _ onDone -> onDone true)
                desk.Show()
            | None -> ()
        | _ -> ports.EffectSink.ShowToast("连接主机后才能导入对话。", ToastTone.Warning)

    member this.ExportConversation(convId: Guid) =
        match model.Conn with
        | ConnPhase.Authed _ ->
            match views with
            | Some v ->
                let lineageInfo = ConversationTransfer.lineageOf model.Summaries model.Lineage convId
                let title = model.Summaries |> List.tryFind (fun s -> s.id = convId) |> Option.map (fun s -> s.title) |> Option.defaultValue "导出对话"
                let desk =
                    v.NewExportDesk
                        (fun query -> ports.SendAtGeneration (ConversationExportRead query))
                        lineageInfo convId title
                desk.Show()
            | None -> ()
        | _ ->
            ports.EffectSink.ShowToast("连接主机后才能读取完整历史。", ToastTone.Warning)

    member this.UploadOwner() : string * int =
        let inst =
            match model.Conn with
            | ConnPhase.Authed(i, _) -> i
            | _ -> ""
        inst, ports.ConnectionGeneration()

    member this.AttachFiles (owner: string * int) (files: (string * byte[]) list) : unit =
        let curInst, curGen = this.UploadOwner()
        if owner <> (curInst, curGen) then
            ports.EffectSink.ShowToast("连接已断开，附件未上传", ToastTone.Warning)
        else
            for name, bytes in files do
                let mediaType = Wanxiang.UI.MediaTypes.ofFileName name
                let sha = Wanxiang.UI.UploadFacts.sha256Hex bytes
                this.Dispatch(User(Composer(ComposerUserMsg.Attach(name, mediaType, int64 bytes.Length, sha))))
                if Wanxiang.UI.MediaTypes.isModelImage mediaType then
                    ports.MakeThumbnail (Guid.NewGuid()) bytes

    member this.ComposerActions(pickAttachment: unit -> unit, dropFiles: IStorageItem list -> unit, pasteFromClipboard: unit -> bool) : Wanxiang2.ComposerDesk.ComposerDeskActions =
        { Submit = fun text ->
            this.Dispatch(User(Composer(ComposerUserMsg.TextChanged text)))
            this.Dispatch(User(Composer(ComposerUserMsg.Send)))
            true
          StopGeneration = fun () -> this.Dispatch(User(Composer(ComposerUserMsg.StopGeneration)))
          PickAttachment = pickAttachment
          RemoveAttachment = fun id -> this.Dispatch(User(Composer(ComposerUserMsg.RemoveAttachment id)))
          OpenModelPicker = fun _ -> this.Dispatch(User(ModelSwitch(ModelSwitchUserMsg.Open)))
          DropFiles = dropFiles
          PasteFromClipboard = pasteFromClipboard
          OpenSessionSettings = fun () -> this.Dispatch(User(Settings(SettingsUserMsg.OpenSection None))) }

    member this.ChatActions() : Wanxiang2.ChatScreen.ChatScreenActions =
        { Card =
            { CopyText = fun text -> ports.EffectSink.Clipboard text
              Regenerate = fun () -> this.Dispatch(User(Chat(ChatUserMsg.Regenerate)))
              EditAndFork = fun message -> this.ForkFrom(message.commitId, message.text)
              CopyLink = Some(fun commitId ->
                  match model.Conn with
                  | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) ->
                      let pageUrl = ports.BrowserPageUrl()
                      ports.EffectSink.Clipboard(MessageLink.format (if String.IsNullOrWhiteSpace pageUrl then None else Some pageUrl) conv.ConversationId commitId)
                  | _ -> ())
              Recover = None
              DeleteMessage = fun id -> this.Dispatch(User(Chat(ChatUserMsg.DeleteExchange (string id))))
              DownloadAttachment = fun sha -> ports.Send(WireEvent.AttachmentDownloadRequest {| sha256 = sha |})
              OpenLink = fun url -> ports.EffectSink.OpenLink url }
          RenameTitle = fun title -> this.Dispatch(User(Header(HeaderUserMsg.CommitRename title)))
          ToggleSidebar = fun () -> this.Dispatch(User(Header(HeaderUserMsg.ToggleSidebar)))
          OpenSessionSettings = fun () -> this.Dispatch(User(Settings(SettingsUserMsg.OpenSection None)))
          ForkFromHere = fun () -> this.ForkFromHere()
          ExportConversation = fun () ->
              match model.Conn with
              | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(c, _))) -> this.ExportConversation c.ConversationId
              | _ -> ports.EffectSink.ShowToast("连接主机后才能读取完整历史。", ToastTone.Warning)
          StopGeneration = fun () -> this.Dispatch(User(Composer(ComposerUserMsg.StopGeneration)))
          RequestOlderHistory = fun () -> this.Dispatch(User(Chat(ChatUserMsg.OlderPageRequested)))
          FocusHome = fun () -> ports.EffectSink.Focus FocusTarget.ComposerInput
          OpenParent = fun id -> this.Dispatch(User(Header(HeaderUserMsg.CrumbClick id)))
          SiblingSelected = fun id -> this.Dispatch(User(Chat(ChatUserMsg.SwitchVersion id)))
          After = fun d f -> ports.After d f }

    member this.SidebarActions() : Wanxiang2.SidebarDesk.SidebarDeskActions =
        { newConversation = fun () -> this.Dispatch(User(Sidebar(SidebarUserMsg.NewConversation)))
          openConversation = fun id -> this.Dispatch(User(Sidebar(SidebarUserMsg.SelectConv id)))
          renameConversation = fun s -> this.Dispatch(User(Sidebar(SidebarUserMsg.RenameConversation(s.id, s.title))))
          deleteConversation = fun s -> this.Dispatch(User(Sidebar(SidebarUserMsg.DeleteConversation s.id)))
          setPinned = fun s _ -> this.Dispatch(User(Sidebar(SidebarUserMsg.TogglePin s.id)))
          setArchived = fun s archived ->
              let invId = Guid.NewGuid()
              let cmd = SetConversationFlags {| invocationId = invId; conversationId = s.id; pinned = s.pinned; archived = archived |}
              this.Dispatch(Msg.WireRecv(WireEvent.Command cmd))
          selectionAllPinned = fun ids ->
              let rows = model.Summaries |> List.filter (fun s -> List.contains s.id ids)
              not (List.isEmpty rows) && rows |> List.forall (fun s -> s.pinned)
          setPinnedMany = fun ids pin ->
              for id in ids do
                  match model.Summaries |> List.tryFind (fun s -> s.id = id) with
                  | Some s ->
                      let cmd = SetConversationFlags {| invocationId = Guid.NewGuid(); conversationId = s.id; pinned = pin; archived = s.archived |}
                      this.Dispatch(Msg.WireRecv(WireEvent.Command cmd))
                  | None -> ()
          setArchivedMany = fun ids archived ->
              for id in ids do
                  match model.Summaries |> List.tryFind (fun s -> s.id = id) with
                  | Some s ->
                      let cmd = SetConversationFlags {| invocationId = Guid.NewGuid(); conversationId = s.id; pinned = s.pinned; archived = archived |}
                      this.Dispatch(Msg.WireRecv(WireEvent.Command cmd))
                  | None -> ()
          deleteMany = fun ids -> this.Dispatch(User(Sidebar(SidebarUserMsg.BatchDelete ids)))
          duplicateAsFork = fun s ->
              this.Dispatch(User(Sidebar(SidebarUserMsg.SelectConv s.id)))
              ports.EffectSink.ShowToast("已打开对话，可从任意消息分叉。", ToastTone.Info)
          exportConversation = fun s -> this.ExportConversation s.id
          openSettings = fun () -> this.Dispatch(User(Settings(SettingsUserMsg.OpenSection None)))
          reconnect = fun () ->
              match ports.TryLoadClientToml() with
              | Some(url, token) ->
                  this.Dispatch(Msg.ConnectionRequested url)
                  async {
                      try
                          let! epoch = ports.Connect url
                          ports.SendAuth epoch (AuthPresent {| token = token |})
                      with _ -> ()
                  } |> Async.Start
              | None -> ()
          toggleArchivedVisibility = fun () ->
              this.Dispatch(User(Sidebar(SidebarUserMsg.ToggleShowArchived)))
          closeNavigation = fun () -> this.Dispatch(User(Overlay(OverlayUserMsg.Escape))) }

    member this.SettingsHooks() : Wanxiang2.SettingsScreen.SettingsHooks =
        { UpsertProvider =
            fun payload onCompleted ->
                sendConfig
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
                        sendConfig
                            "模型服务已删除"
                            (fun requestId -> ConfigDeleteProvider {| requestId = requestId; providerId = id |})
                            ignore)
                |> ignore
          ProbeProvider = fun id -> this.ProbeProvider id
          UpsertMcp =
            fun payload onCompleted ->
                sendConfig
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
                        sendConfig
                            "MCP 服务已删除"
                            (fun requestId -> ConfigDeleteMcp {| requestId = requestId; serverId = id |})
                            ignore)
                |> ignore
          UpdateGeneration =
            fun payload onCompleted ->
                sendConfig
                    "生成设置已保存"
                    (fun requestId -> ConfigUpdateGeneration {| requestId = requestId; generation = payload |})
                    onCompleted
          SavePrefs =
            fun p ->
                // 偏好先入模型再落盘：下次投影各分区读到的就是新值（单一归属，不再旁路）。
                this.Dispatch(User(Settings(SettingsUserMsg.PrefsChanged p)))
          RequestDoctorCheck =
            fun () ->
                // 「检查主机」：纯读请求，带 requestId；报告由协议事件分支回填。
                match model.Conn with
                | ConnPhase.Authed(_, _) ->
                    let requestId = Guid.NewGuid()
                    doctorRequestId <- Some requestId
                    doctorWait.Begin(DateTimeOffset.UtcNow)
                    ports.Send(DoctorRequest {| requestId = requestId |})
                | _ -> v().Settings.Health.ApplyUnavailable(BoundedWaits.doctorOfflineText) }

    member this.OnboardingHooks() : Wanxiang2.SettingsScreen.OnboardingHooks =
        { UpsertProvider =
            fun payload onCompleted ->
                // 引导卡的写入由卡片自己说话：成功文案留空，ProtocolDispatch 不叠 toast。
                sendConfig
                    ""
                    (fun requestId -> ConfigUpsertProvider {| requestId = requestId; provider = payload |})
                    (fun ok ->
                        onCompleted ok
                        // 卡片此刻才把「已接上」记在卡片里：重投影一次，交接才接得住
                        this.Reproject())
          DiscardProvider =
            fun id ->
                sendConfig "" (fun requestId -> ConfigDeleteProvider {| requestId = requestId; providerId = id |}) ignore
          ProbeProvider = fun id -> this.ProbeProvider id
          TakenIds =
            fun () ->
                match model.Conn with
                | ConnPhase.Authed(_, CatalogPhase.Ready(cat, _)) -> cat.providers |> List.map (fun p -> p.id)
                | _ -> []
          DiscoverLocal =
            fun () ->
                if OnboardingRules.canDiscoverLocal model.ServerUrl then
                    OnboardingRules.discoverOllamaAt ProviderPresets.OllamaProbeUrl
                else Task.FromResult None
          OpenAdvanced =
            fun () ->
                this.Dispatch(User(Settings(SettingsUserMsg.OpenSection (Some "providers"))))
                match views with
                | Some vs' -> ports.Post(fun () -> vs'.Settings.ApplyOpenAddProvider())
                | None -> ()
          Report = fun msg -> this.Dispatch(User(Onboarding(msg))) }

    /// 探活：登记请求再发出；回执由 ProtocolDispatch 结算（超时走 ExpireHostRequests）。
    member this.ProbeProvider(providerId: string) =
        let requestId = Guid.NewGuid()
        probes.Track(requestId, providerId, DateTimeOffset.UtcNow)
        ports.Send(ProviderProbeRequest {| requestId = requestId; providerId = providerId |})

    /// 预设写入（须主机支持 presets 能力；未知 = 允许）
    member this.RenamePreset(preset: Wanxiang.UI.PresetInfo) =
        if HostFeatures.supports hostFeatures HostFeatures.Presets then
            v().Dialogs.Prompt(
                "重命名预设",
                "预设名称",
                preset.name,
                "保存",
                fun name onCompleted ->
                    sendConfig
                        "预设已重命名"
                        (fun requestId ->
                            ConfigUpsertPreset
                                {| requestId = requestId; preset = Presets.payloadOf (Some preset.id) name (Presets.ofPreset preset) |})
                        onCompleted)
            |> ignore
        else ports.EffectSink.ShowToast(HostFeatures.unsupportedMessage HostFeatures.Presets, ToastTone.Warning)

    member this.DeletePreset(preset: Wanxiang.UI.PresetInfo) =
        if HostFeatures.supports hostFeatures HostFeatures.Presets then
            v().Dialogs.Confirm(
                "删除预设",
                (sprintf "「%s」将从这台主机上移除。已经用它建好的对话不受影响。" preset.name),
                "删除",
                fun () ->
                    sendConfig
                        "预设已删除"
                        (fun requestId -> ConfigDeletePreset {| requestId = requestId; presetId = preset.id |})
                        ignore)
            |> ignore
        else ports.EffectSink.ShowToast(HostFeatures.unsupportedMessage HostFeatures.Presets, ToastTone.Warning)

    member this.WorkspaceHooks(touch: unit -> bool) : Wanxiang2.Workspace.WorkspaceHooks =
        { PrefersTouch = touch
          SidebarWidth = fun () -> model.Nav.SidebarWidth
          InitialSidebarCollapsed = model.Nav.SidebarCollapsed }
