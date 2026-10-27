namespace Wanxiang2.AppShell.Tests

// 组合者测试台：真实 Desk 树（按入口壳同序构造）+ 假 ShellPorts。
// 目的：钉住「旧 AppShell 的行为」在组合者这一层的可观察后果——发出去的协议事件/命令载荷、
// 提示条文案、偏好扇出、能力表闸门。计时器与 UI 线程投递都在这里被替换成确定性实现。

open System
open System.Threading
open System.Threading.Tasks
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.WorkspaceState
open Wanxiang2.Theme
open Wanxiang2.OverlayDesk
open Wanxiang2.SidebarDesk
open Wanxiang2.ComposerDesk
open Wanxiang2.ApprovalDesk
open Wanxiang2.ChatScreen
open Wanxiang2.MessageCardView
open Wanxiang2.CodeTint
open Wanxiang2.Workspace
open Wanxiang2.SettingsScreen
open Wanxiang2.PaletteDesk
open Wanxiang2.ModelSwitchDesk
open Wanxiang2.DialogsDesk

/// 假端口：记录组合者发出去的东西，并给出确定的时机（Post 走 Dispatcher，测试自己跑消息循环）。
type FakePorts(prefs0: UiPrefs) =
    let toasts = ResizeArray<string * ShellToastTone>()
    let undos = ResizeArray<string>()
    let events = ResizeArray<WireEvent>()
    let authEvents = ResizeArray<int * WireEvent>()
    let commands = ResizeArray<ClientCommand>()
    let savedPrefs = ResizeArray<UiPrefs>()
    let draftWrites = ResizeArray<DraftWrite>()
    /// 宿主侧存着的本机草稿（`LoadLocalDraft` 端口读它）。
    let localDraft = ref None
    let thumbnails = ResizeArray<Guid * byte[]>()
    let themes = ResizeArray<bool>()
    let motions = ResizeArray<bool>()
    let wraps = ResizeArray<bool>()
    let links = ResizeArray<string>()
    let copies = ResizeArray<string>()
    let downloads = ResizeArray<string * byte[]>()
    let connects = ResizeArray<string>()
    let recordedEffects = ResizeArray<Wanxiang2.WorkspaceState.Effect>()
    let browserSucceeded = ref 0
    let restoreCalls = ref 0
    let posts = ref 0
    let mutable generation = 7
    let mutable connected = true
    let mutable sendsSucceed = true
    /// 浏览器凭据（IndexedDB 里那一条）。测试可设成 `Some` 来验 PWA 的自动连接回落。
    let mutable browserConnection: (string * string * string) option = None
    /// 宿主侧的「打开文件选择器」回调：测试台里没有真正的文件选择器，
    /// 但「添加附件」按键必须能走到宿主的真实上传入口（`ShellComposer.AttachFiles`），
    /// 于是这里给一个可替换的钩子。默认什么都不做（老行为）。
    let mutable pickAttachment: unit -> unit = fun () -> ()
    let mutable editing = false
    let mutable dark = false
    let mutable hostHandler: (WireEvent -> unit) option = None
    let mutable closedHandler: (string option -> unit) option = None
    /// 浏览器 CSS 视口宽。0 表示桌面：组合者继续用窗口 Bounds。
    let mutable cssViewport = 0.0
    let timers = ResizeArray<TimeSpan * (unit -> unit)>()

    /// 假时钟：组合者的时间来源（`ShellPorts.Now`）。
    /// 到期删除这类按时间推进的行为要能被确定性地测，就不能读系统时钟。
    let mutable clockNow = DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero)

    member _.Toasts = toasts |> List.ofSeq
    member _.UndoToasts = undos |> List.ofSeq
    member _.Events = events |> List.ofSeq
    member _.AuthEvents = authEvents |> List.ofSeq
    member _.Commands = commands |> List.ofSeq
    member _.SavedPrefs = savedPrefs |> List.ofSeq
    member _.DraftWrites = draftWrites |> List.ofSeq
    /// 设置宿主侧存着的本机草稿（`LoadLocalDraft` 端口会读到它）。
    member _.SetLocalDraft(text: string option) = localDraft := text
    member _.LocalDraft = !localDraft
    member _.Thumbnails = thumbnails |> List.ofSeq
    member _.AppliedThemes = themes |> List.ofSeq
    member _.ReducedMotion = motions |> List.ofSeq
    member _.CodeWraps = wraps |> List.ofSeq
    member _.OpenLinks = links |> List.ofSeq
    member _.Copied = copies |> List.ofSeq
    member _.Downloads = downloads |> List.ofSeq
    member _.Connects = connects |> List.ofSeq
    member _.RecordedEffects = recordedEffects |> List.ofSeq
    member _.BrowserSucceeded = browserSucceeded.Value
    member _.RestoreFocusCalls = restoreCalls.Value
    /// 已执行的 UI 线程投递次数（等后台续体落地用）。
    member _.Posts = posts.Value

    member _.Generation
        with get () = generation
        and set value = generation <- value

    member _.Connected
        with get () = connected
        and set value = connected <- value

    member _.SendsSucceed
        with get () = sendsSucceed
        and set value = sendsSucceed <- value

    member _.EditingText
        with get () = editing
        and set value = editing <- value

    /// 替换「添加附件」按键的宿主行为（点真按键 → 走真上传路径）。
    member _.OnPickAttachment
        with get () = pickAttachment
        and set value = pickAttachment <- value

    /// 当前假时钟时刻。
    member _.Now
        with get () = clockNow
        and set value = clockNow <- value

    /// 把假时钟往前拨 `seconds` 秒（到期删除这类行为的唯一推进方式）。
    member _.AdvanceSeconds(seconds: float) = clockNow <- clockNow.AddSeconds seconds

    /// 浏览器里的已存连接（`Some (url, token, instanceId)` 时 `AutoConnect` 应当用它连上）。
    member _.BrowserConnection
        with get () = browserConnection
        and set value = browserConnection <- value

    member _.SystemDark
        with get () = dark
        and set value = dark <- value

    /// 浏览器报告的 CSS 像素宽度。PWA 对应 `innerWidth`；测试里由活树审计写入。
    member _.CssViewport
        with get () = cssViewport
        and set value = cssViewport <- value

    /// 跑 Build 登记的那一只周期回调。生产里由 `ports.Every` 每拍触发。
    member _.FireEvery(interval: TimeSpan) =
        for span, tick in timers do
            if span = interval then
                tick ()

    /// 入口壳的协议事件回调（Build 里订阅进去的那个）。
    member _.Deliver(ev: WireEvent) =
        hostHandler |> Option.iter (fun handler -> handler ev)

    /// 入口壳的连接关闭回调。
    member _.Close(detail: string option) =
        closedHandler |> Option.iter (fun handler -> handler detail)

    member this.Sink: Wanxiang.UI.EffectSink =
        {
            Focus = fun f -> recordedEffects.Add(Effect.Focus f)
            ScrollChat = fun s -> recordedEffects.Add(Effect.ScrollChat s)
            SendCommand =
                fun cmd ->
                    recordedEffects.Add(Effect.SendCommand cmd)
                    commands.Add cmd
            SendWire =
                fun w ->
                    recordedEffects.Add(Effect.SendWire w)
                    events.Add w
            After =
                fun (ms, msg) ->
                    match msg with
                    | :? Msg as m -> recordedEffects.Add(Effect.After(ms, m))
                    | _ -> ()
            Every = fun (h, ms) -> recordedEffects.Add(Effect.Every(h, ms))
            StopEvery = fun h -> recordedEffects.Add(Effect.StopEvery h)
            ShowToast =
                fun (t, tone) ->
                    recordedEffects.Add(Effect.ShowToast(t, tone))
                    toasts.Add(t, ShellToastTone.Info)
            Clipboard =
                fun c ->
                    recordedEffects.Add(Effect.Clipboard c)
                    copies.Add c
            OpenLink =
                fun u ->
                    recordedEffects.Add(Effect.OpenLink u)
                    links.Add u
            SaveDownload =
                fun (n, b) ->
                    recordedEffects.Add(Effect.SaveDownload(n, b))
                    downloads.Add(n, b)
            ApplyTheme = fun t -> recordedEffects.Add(Effect.ApplyTheme t)
            SetWindowBusy = fun b -> recordedEffects.Add(Effect.SetWindowBusy b)
            CloseWindow = fun () -> recordedEffects.Add(Effect.CloseWindow)
            PrefsSave =
                fun p ->
                    recordedEffects.Add(Effect.PrefsSave p)
                    savedPrefs.Add p
            PrefsLoad = fun () -> recordedEffects.Add(Effect.PrefsLoad)
            DraftWrite =
                fun dw ->
                    recordedEffects.Add(Effect.DraftWrite dw)
                    draftWrites.Add dw
            BrowserHook = fun h -> recordedEffects.Add(Effect.BrowserHook h)
        }

    member this.Ports(buildViews: unit -> ShellViews) : ShellPorts =
        {
            Connect =
                fun url ->
                    connects.Add url
                    async { return generation }
            SendAuth = fun epoch ev -> authEvents.Add(epoch, ev)
            Send = fun ev -> events.Add ev
            SendCommand =
                fun cmd ->
                    commands.Add cmd
                    Task.FromResult sendsSucceed
            SendAtGeneration =
                fun ev ->
                    events.Add ev
                    Task.FromResult true
            // 假时钟：组合者的时间来源。生产入口给系统时钟，测试给可推进的假时钟。
            Now = fun () -> clockNow
            // 真把延时回调排上：草稿落盘的 800 ms 闸走的就是这个端口
            // （`compose/AppShell` 的 `afterOnce`）。只记录不调度的话，
            // 闸永远不会到期，测不出「打字停下来才落盘」。
            After =
                fun ms action ->
                    let mutable handle: IDisposable = null

                    let timer =
                        DispatcherTimer(
                            ms,
                            DispatcherPriority.Background,
                            EventHandler(fun _ _ ->
                                let h = handle

                                if not (isNull h) then
                                    h.Dispose()

                                action ())
                        )

                    timer.Start()
                    // `DispatcherTimer` 本身不是 IDisposable，用一个壳把 `Stop` 暴露成 `Dispose`。
                    let wrapper =
                        { new IDisposable with
                            member _.Dispose() = timer.Stop()
                        }

                    handle <- wrapper
                    handle
            Every =
                fun interval action ->
                    timers.Add(interval, action)

                    { new IDisposable with
                        member _.Dispose() = ()
                    }
            Post =
                fun action ->
                    Dispatcher.UIThread.Post(fun () ->
                        posts.Value <- posts.Value + 1
                        action ())
            Toast = fun text tone -> toasts.Add(text, tone)
            ToastUndo = fun text _ -> undos.Add text
            CopyToClipboard = fun text -> copies.Add text
            OpenLink = fun url -> links.Add url
            SaveDownload = fun name bytes -> downloads.Add(name, bytes)
            ApplyDraftWrite = fun write -> draftWrites.Add write
            LoadLocalDraft = fun () -> async { return !localDraft }
            PrefsLoad = fun () -> prefs0
            PrefsSave = fun next -> savedPrefs.Add next
            TryLoadClientToml = fun () -> None
            TryLoadBrowserConnection = fun () -> async { return browserConnection }
            SaveBrowserConnection = fun _ _ _ _ -> ()
            BrowserViewportWidth = fun () -> cssViewport
            BrowserResumeEpoch = fun () -> 0
            BrowserPageUrl = fun () -> ""
            BrowserConversationSucceeded = fun () -> browserSucceeded.Value <- browserSucceeded.Value + 1
            BrowserAddHidden = fun _ -> ()
            SystemPrefersDark = fun () -> dark
            DefaultServerUrl = fun () -> "ws://127.0.0.1:1/ws"
            ApplyTheme = fun value -> themes.Add value
            SetReducedMotion = fun value -> motions.Add value
            SetCodeWrap = fun value -> wraps.Add value
            CaptureFocus =
                fun () ->
                    fun () ->
                        restoreCalls.Value <- restoreCalls.Value + 1
                        true
            IsEditingText = fun () -> editing
            ConnectionGeneration = fun () -> generation
            IsConnected = fun () -> connected
            SubscribeHostEvents =
                fun handler ->
                    hostHandler <- Some handler

                    { new IDisposable with
                        member _.Dispose() = hostHandler <- None
                    }
            SubscribeClosed =
                fun handler ->
                    closedHandler <- Some handler

                    { new IDisposable with
                        member _.Dispose() = closedHandler <- None
                    }
            CloseWindow = fun () -> false
            ClientName = fun () -> "测试客户端"
            MakeThumbnail = fun id bytes -> thumbnails.Add(id, bytes)
            EffectSink = this.Sink
            BuildViews = buildViews
        }

/// 浮层外观（与 OverlayDesk 自己的测试同值；这里只求能建树）。
module Look =
    let overlay () : OverlayDeskMetrics =
        {
            Layer =
                {
                    Scrim = SolidColorBrush(Color.FromArgb(80uy, 0uy, 0uy, 0uy))
                    Surface = Brushes.White
                    Edge = Brushes.Gray
                    DialogShadow = BoxShadows()
                    PopupShadow = BoxShadows()
                    PopupGap = 4.0
                    PopupMinWidth = 180.0
                    ScrimFade = TimeSpan.Zero
                    ScrimEasing = Avalonia.Animation.Easings.CubicEaseOut()
                }
            Toasts =
                {
                    Surface = Brushes.White
                    Edge = Brushes.Gray
                    Text = Brushes.Black
                    Accent = (fun _ -> Brushes.Gray)
                    AccentOpacity = 0.6
                    Padding = Thickness(16.0, 12.0)
                    Shadow = BoxShadows()
                    LineHeight = 18.0
                    ColumnSpacing = 12.0
                    Spacing = 8.0
                    Margin = Thickness(24.0, 0.0, 24.0, 32.0)
                    MaxWidth = 480.0
                    BodyMaxHeight = 160.0
                    // 减弱动效：主题给 0
                    Fade = TimeSpan.Zero
                    FadeEasing = Avalonia.Animation.Easings.CubicEaseOut()
                }
            Menu =
                {
                    Text = Brushes.Black
                    Muted = Brushes.Gray
                    Faint = Brushes.LightGray
                    Danger = Brushes.Red
                    Accent = Brushes.Blue
                    Check = (fun b -> Border(Width = 12.0, Height = 12.0, Background = b))
                    Chevron = (fun b -> Border(Width = 10.0, Height = 10.0, Background = b))
                    IconSlotWidth = 16.0
                    ItemMinHeight = 32.0
                    MaxHeight = 200.0
                }
            MenuMinWidth = 200.0
            GroupedMenuMinWidth = 240.0
            DwellFailureMs = 9000.0
            DwellWarningMs = 6500.0
            DwellDefaultMs = 4000.0
            DismissTick = TimeSpan.FromMilliseconds 100.0
        }

/// 测试台：一个组合者 + 真实视图树 + 假端口。
type Rig =
    {
        Shell: ShellComposer
        Views: ShellViews
        Fake: FakePorts
        Theme: ThemeManager
        Summary: ConversationSummary
        ImportDesks: ResizeArray<Wanxiang2.TransferDesk.ImportDesk>
        ExportDesks: ResizeArray<Wanxiang2.TransferDesk.ExportDesk>
        Root: Grid
    }

module Rig =
    let private summaryOf (id: Guid) : ConversationSummary =
        {
            id = id
            title = "一段对话"
            preview = ""
            running = false
            pinned = false
            archived = false
            createdAt = DateTimeOffset.UnixEpoch
            updatedAt = DateTimeOffset.UnixEpoch
            messageCount = 0
            isFork = false
            providerId = "p1"
            model = "m1"
            lastCommitId = 0UL
        }

    /// 建一套完整测试台：BuildViews 交给组合者，Build() 由测试自己调（要断言的时点不同）。
    let createWith (prefs0: UiPrefs) : Rig =
        let theme = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
        let styles = new ThemeStyles(theme)
        // `PerAssembly` 下整个程序集共享一个 Application：每次先清掉上一个用例装进去的
        // ThemeStyles 与主题资源字典，只保留 `TestApp.Initialize` 装的第 0 项 `FluentTheme`，
        // 防止 50 多个用例把 Styles 叠到 50 份拖慢样式匹配。
        while Application.Current.Styles.Count > 1 do
            Application.Current.Styles.RemoveAt(Application.Current.Styles.Count - 1)

        Application.Current.Resources.MergedDictionaries.Clear()
        styles.Install Application.Current.Styles
        // 入口壳把主题资源字典并进 Application（EntryShells.App.Initialize）。
        // 对话框留白 Margin.OverlayInset 只在这条路上查得到；只装样式时窄窗确认框不会收进视口。
        let themeDict = theme.Resources :> IResourceDictionary
        Application.Current.Resources.MergedDictionaries.Add themeDict

        let fake = FakePorts(prefs0)
        let importDesks = ResizeArray<Wanxiang2.TransferDesk.ImportDesk>()
        let exportDesks = ResizeArray<Wanxiang2.TransferDesk.ExportDesk>()
        let mutable shell: ShellComposer = Unchecked.defaultof<_>
        let mutable built: ShellViews option = None
        let mutable rootGrid: Grid option = None

        let buildViews () : ShellViews =
            let root = Grid()

            root.SetValue(Avalonia.Controls.Documents.TextElement.FontFamilyProperty, theme.FontFamily.Value)
            |> ignore

            rootGrid <- Some root
            let overlay = OverlayDesk(root, Look.overlay ())

            let brandLogo (size: float) =
                Border(Width = size, Height = size) :> Control

            let composer =
                ComposerDesk(
                    shell.ComposerActions((fun () -> fake.OnPickAttachment()), (fun _ -> ()), (fun () -> false))
                )

            let approval = ApprovalDesk(theme, overlay, ignore)

            let cards =
                MessageCards(
                    CardLooks.ofTheme theme styles,
                    {
                        Tint =
                            CodeTint
                                {
                                    HighlightHtml = (fun _ -> None)
                                    Background = (fun work -> work ())
                                    After = (fun _ work -> work ())
                                    Synchronous = true
                                }
                        MathHtml = (fun _ -> None)
                        Wrap = Wanxiang2.MarkdownView.CodeWrapMemory false
                        Clock = CaretClock()
                        Touch = CardTouch()
                        Now = fun () -> DateTimeOffset.UnixEpoch
                    }
                )

            let chat =
                ChatScreen(
                    ChatScreenMetrics.ofTheme theme styles,
                    cards,
                    composer,
                    approval,
                    overlay,
                    brandLogo,
                    shell.ChatActions()
                )

            let sidebar = SidebarDesk(theme, overlay, shell.SidebarActions(), brandLogo)
            let settings = SettingsScreen(theme, overlay, shell.SettingsHooks(), prefs0)
            let onboarding = OnboardingDesk(theme, overlay, shell.OnboardingHooks())

            let workspace =
                Workspace(theme, root, sidebar, chat, composer, shell.WorkspaceHooks(fun () -> false))

            let palette = PaletteDesk(theme, overlay, (fun () -> false))
            let modelSwitch = ModelSwitchDesk(theme, overlay)
            let dialogs = DialogsDesk(theme, overlay)

            {
                Sidebar = sidebar
                Chat = chat
                Composer = composer
                Settings = settings
                Onboarding = onboarding
                Workspace = workspace
                Approval = approval
                Palette = palette
                ModelSwitch = modelSwitch
                Overlay = overlay
                Dialogs = dialogs
                NewExportDesk =
                    fun sendQuery lineage conversationId title ->
                        let desk =
                            new Wanxiang2.TransferDesk.ExportDesk(
                                theme,
                                overlay,
                                conversationId,
                                title,
                                sendQuery,
                                (fun () -> null :> TopLevel),
                                lineage
                            )

                        exportDesks.Add desk
                        desk
                NewImportDesk =
                    fun submit ->
                        let desk =
                            new Wanxiang2.TransferDesk.ImportDesk(theme, overlay, (fun () -> null :> TopLevel), submit)

                        importDesks.Add desk
                        desk
                SettingsHostVisible =
                    fun visible ->
                        if visible then
                            // 插到索引 0：设置页是整页宿主，必须排在覆盖层（遮罩 / 对话框卡 / 浮层卡 / 提示条）
                            // 之下，否则设置页自己打开的下拉菜单与确认框会被整页盖住。
                            if not (root.Children.Contains settings.View) then
                                root.Children.Insert(0, settings.View)
                        else
                            root.Children.Remove settings.View |> ignore

                        settings.View.IsVisible <- visible
                WorkspaceHostVisible = fun visible -> workspace.Host.IsVisible <- visible
                SettingsFocusInitial = fun () -> settings.FocusInitial()
                SettingsShowSection =
                    fun section ->
                        section
                        |> Option.iter (fun key ->
                            Wanxiang2.SettingsScreen.SettingsPart.ofKey key
                            |> Option.iter settings.ShowSection)
            }

        let ports =
            fake.Ports(fun () ->
                let views = buildViews ()
                built <- Some views
                views)

        shell <- ShellComposer(ports)
        shell.Build() |> ignore

        {
            Shell = shell
            Views = built.Value
            Fake = fake
            Theme = theme
            Summary = summaryOf (Guid "11111111-1111-1111-1111-111111111111")
            ImportDesks = importDesks
            ExportDesks = exportDesks
            Root = rootGrid.Value
        }

    let create () : Rig = createWith UiPrefsFile.defaults

/// 跑消息循环直到条件成立（Post 走 Dispatcher；测试体在 UI 线程上，所以可以自己驱动）。
module Pump =
    let until (predicate: unit -> bool) =
        let deadline = DateTime.UtcNow + TimeSpan.FromSeconds 5.0

        while not (predicate ()) && DateTime.UtcNow < deadline do
            Dispatcher.UIThread.RunJobs()
            Thread.Sleep 5

        Dispatcher.UIThread.RunJobs()

    /// 事件循环跑几拍（只等时机，不等结果）。
    let settle () =
        for _ in 1..3 do
            Dispatcher.UIThread.RunJobs()
            Thread.Sleep 5

        Dispatcher.UIThread.RunJobs()
