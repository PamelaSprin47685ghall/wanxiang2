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
    let mutable paletteRecent: string list = []

    // 账本状态参与者（由 ProtocolDispatch 处理）
    let state = ClientState()
    let outbox = MessageOutbox()
    let downloads = DownloadBuffers()
    let search = SearchInFlight()
    let probes = PendingProbes()
    let runs = ConversationRuns()
    let commandFeedback = CommandFeedbackTracker()
    let configFeedback = ConfigFeedbackTracker()

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
            let activeConvId =
                match model.Conn with
                | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(conv, _))) -> Some conv.ConversationId
                | _ -> None
            let activeConv =
                activeConvId |> Option.bind (fun id -> state.Conversations |> Map.tryFind id)

            match activeConvId, activeConv with
            | None, _ ->
                v.Chat.SetConversationChrome false
                v.Chat.SetBreadcrumb None
                match vs.Chat.Phase with
                | ChatPhase.Empty (IdleEmptyKind.ConnectionFailed err) ->
                    v.Chat.Pane.ShowEmpty(Wanxiang.UI.ChatEmptyState.ConnectionFailed err, None)
                | ChatPhase.Empty IdleEmptyKind.NotConnected ->
                    v.Chat.Pane.ShowEmpty(Wanxiang.UI.ChatEmptyState.NotConnected, None)
                | ChatPhase.Empty IdleEmptyKind.NoConversation ->
                    v.Chat.Pane.ShowEmpty(Wanxiang.UI.ChatEmptyState.NoConversation, None)
                | ChatPhase.Empty _ ->
                    v.Chat.Pane.ShowEmpty(Wanxiang.UI.ChatEmptyState.NoConversation, None)
                | _ ->
                    v.Chat.Pane.ShowEmpty(Wanxiang.UI.ChatEmptyState.NoConversation, None)
                v.Chat.Header.Render vs.Header
            | Some _, None ->
                v.Chat.SetConversationChrome true
                v.Chat.Pane.ShowSkeleton()
                v.Chat.Header.Render vs.Header
            | Some convId, Some view ->
                v.Chat.Pane.HideSkeleton()
                v.Chat.Pane.HideEmpty()
                v.Chat.Header.Render vs.Header
                let msgs = view.messages |> Seq.map MessageView.ofSnapshotItem |> List.ofSeq
                let run = runs.Get (Some convId)
                v.Chat.SetGenerating(run.running, (if run.running then "生成中" else ""))
                v.Chat.SetCanStop(run.running)
                let retry () = this.Dispatch(User(Chat(ChatUserMsg.Regenerate)))
                v.Chat.Render
                    { Messages = msgs
                      Streaming = run.message
                      Error = run.error |> Option.map (fun e -> e, retry)
                      FontSize = model.Prefs.fontScale
                      AutoCollapseReasoning = model.Prefs.autoCollapseReasoning
                      Usage = run.usage
                      MissingAttachments = Set.empty }

            // 3. Composer
            v.Composer.Box.Render vs.Composer

            // 4. Sidebar
            let summaries =
                model.Summaries
                |> List.map (fun s ->
                    {
                        Wanxiang.UI.ConversationSummary.id = s.Id
                        title = s.Title
                        preview = s.Preview
                        running = s.Running
                        pinned = s.Pinned
                        archived = s.Archived
                        createdAt = s.UpdatedAt
                        updatedAt = s.UpdatedAt
                        messageCount = s.MessageCount
                        isFork = false
                        providerId = s.ProviderId
                        model = s.Model
                        lastCommitId = s.LastCommitId
                    })
            v.Sidebar.SetConversations summaries
            v.Sidebar.SetActive activeConvId
            v.Sidebar.SetConnection(vs.Sidebar.Host.Connected, vs.Sidebar.Host.StatusText)
            v.Sidebar.SetCompactMode vs.Sidebar.Compact

            // 5. Settings
            match vs.Settings with
            | Some settingsVS ->
                v.SettingsHostVisible true
                v.WorkspaceHostVisible false
                settingsVS.Catalog |> Option.iter v.Settings.SetCatalog
            | None ->
                v.SettingsHostVisible false
                v.WorkspaceHostVisible true

            // 6. Approval
            v.Approval.Render(vs.Approval, activeConvId)

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
                          match model.Conn with
                          | ConnPhase.Authed(_, CatalogPhase.Ready(_, _)) when (Workspace.project model).Settings.IsSome -> true
                          | _ -> false
                      IsDrawerOpen = fun () -> model.Nav.DrawerOpen
                      IsGenerating = fun () ->
                          match model.Conn with
                          | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active(_, RunState.Generating _))) -> true
                          | _ -> false
                      HasConversation = fun () ->
                          match model.Conn with
                          | ConnPhase.Authed(_, CatalogPhase.Ready(_, WorkspacePhase.Active _)) -> true
                          | _ -> false
                      VisibleOrder = fun () -> model.Summaries |> List.map (fun s -> string s.Id)
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
                      SaveRecent = fun recent -> paletteRecent <- recent
                      SaveTheme = fun theme ->
                          let next = { model.Prefs with theme = theme }
                          ports.EffectSink.PrefsSave next
                      Toast = fun text tone ->
                          let t = match tone with Wanxiang.UI.ShellTone.Warning -> ToastTone.Warning | _ -> ToastTone.Info
                          ports.EffectSink.ShowToast(text, t) },
                    paletteRecent)
            shellCommands <- Some c
            c

    member this.HandleShortcut(shortcut: ShortcutAction) : bool =
        this.Commands.HandleShortcut shortcut

    member this.SetPaletteRecent(recent: string list) = paletteRecent <- recent
    member this.PaletteRecent() : string list = paletteRecent

    member this.Build() : IDisposable =
        let built = ports.BuildViews()
        views <- Some built
        let subs = ResizeArray<IDisposable>()
        ports.SubscribeHostEvents(fun ev -> this.HandleEvent ev) |> subs.Add
        ports.SubscribeClosed(fun detail -> this.HandleConnectionClosed detail) |> subs.Add
        let ticker = ports.Every (TimeSpan.FromMilliseconds 250.0) (fun () ->
            this.Dispatch(MsgTick DateTimeOffset.UtcNow))
        this.Reproject()
        { new IDisposable with
            member _.Dispose() =
                ticker.Dispose()
                for sub in subs do sub.Dispose() }

    member this.AutoConnect() =
        match ports.TryLoadClientToml() with
        | Some(url, token) ->
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
                let summaries =
                    model.Summaries
                    |> List.map (fun s ->
                        { Wanxiang.UI.ConversationSummary.id = s.Id; title = s.Title; preview = s.Preview; running = s.Running
                          pinned = s.Pinned; archived = s.Archived; createdAt = s.UpdatedAt; updatedAt = s.UpdatedAt
                          messageCount = s.MessageCount; isFork = false; providerId = s.ProviderId; model = s.Model; lastCommitId = s.LastCommitId })
                let lineage = ForkLineage.parse state.ConversationList
                let lineageInfo = ConversationTransfer.lineageOf summaries lineage convId
                let title = model.Summaries |> List.tryFind (fun s -> s.Id = convId) |> Option.map (fun s -> s.Title) |> Option.defaultValue "导出对话"
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
              let rows = model.Summaries |> List.filter (fun s -> List.contains s.Id ids)
              not (List.isEmpty rows) && rows |> List.forall (fun s -> s.Pinned)
          setPinnedMany = fun ids pin ->
              for id in ids do
                  match model.Summaries |> List.tryFind (fun s -> s.Id = id) with
                  | Some s ->
                      let cmd = SetConversationFlags {| invocationId = Guid.NewGuid(); conversationId = s.Id; pinned = pin; archived = s.Archived |}
                      this.Dispatch(Msg.WireRecv(WireEvent.Command cmd))
                  | None -> ()
          setArchivedMany = fun ids archived ->
              for id in ids do
                  match model.Summaries |> List.tryFind (fun s -> s.Id = id) with
                  | Some s ->
                      let cmd = SetConversationFlags {| invocationId = Guid.NewGuid(); conversationId = s.Id; pinned = s.Pinned; archived = archived |}
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
                  async {
                      try
                          let! epoch = ports.Connect url
                          ports.SendAuth epoch (AuthPresent {| token = token |})
                      with _ -> ()
                  } |> Async.Start
              | None -> ()
          toggleArchivedVisibility = fun () ->
              let next = { model.Prefs with showArchived = not model.Prefs.showArchived }
              ports.EffectSink.PrefsSave next
          closeNavigation = fun () -> this.Dispatch(User(Overlay(OverlayUserMsg.Escape))) }

    member this.SettingsHooks() : Wanxiang2.SettingsScreen.SettingsHooks =
        { UpsertProvider = fun _ _ -> ()
          DeleteProvider = fun id -> this.Dispatch(User(Settings(SettingsUserMsg.DeleteProvider id)))
          ProbeProvider = fun id -> this.Dispatch(User(Settings(SettingsUserMsg.ProbeProvider id)))
          UpsertMcp = fun _ _ -> ()
          DeleteMcp = fun _ -> ()
          UpdateGeneration = fun _ onDone -> onDone true
          SavePrefs = fun p -> ports.EffectSink.PrefsSave p }

    member this.OnboardingHooks() : Wanxiang2.SettingsScreen.OnboardingHooks =
        { UpsertProvider = fun _ _ -> ()
          DiscardProvider = fun _ -> ()
          ProbeProvider = fun id -> this.Dispatch(User(Settings(SettingsUserMsg.ProbeProvider id)))
          TakenIds = fun () -> []
          DiscoverLocal = fun () -> Task.FromResult None
          OpenAdvanced = fun () -> this.Dispatch(User(Settings(SettingsUserMsg.OpenSection (Some "providers")))) }

    member this.WorkspaceHooks(touch: unit -> bool) : Wanxiang2.Workspace.WorkspaceHooks =
        { PrefersTouch = touch
          SidebarWidth = fun () -> model.Nav.SidebarWidth
          InitialSidebarCollapsed = model.Nav.SidebarCollapsed }
