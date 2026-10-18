namespace Wanxiang2.AppShell.Tests

// 组合者测试台：真实 Desk 树（按入口壳同序构造）+ 假 ShellPorts。
// 目的：钉住「旧 AppShell 的行为」在组合者这一层的可观察后果——发出去的协议事件/命令载荷、
// 提示条文案、偏好扇出、能力表闸门。计时器与 UI 线程投递都在这里被替换成确定性实现。

open System
open System.Collections.Generic
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

/// 测试用的活度量：主题管理者改值时通知（与 Workspace 测试同一件小工具）。
type Cell<'T>(initial: 'T) =
    let mutable value = initial
    let subs = List<IObserver<'T>>()

    member _.Set v =
        value <- v
        for s in List.ofSeq subs do s.OnNext v

    interface IObservable<'T> with
        member _.Subscribe o =
            subs.Add o
            o.OnNext value
            { new IDisposable with member _.Dispose() = subs.Remove o |> ignore }

/// 假端口：记录组合者发出去的东西，并给出确定的时机（Post 走 Dispatcher，测试自己跑消息循环）。
type FakePorts(prefs0: UiPrefs) =
    let toasts = ResizeArray<string * ShellToastTone>()
    let undos = ResizeArray<string>()
    let events = ResizeArray<WireEvent>()
    let authEvents = ResizeArray<int * WireEvent>()
    let commands = ResizeArray<ClientCommand>()
    let savedPrefs = ResizeArray<UiPrefs>()
    let draftWrites = ResizeArray<DraftWrite>()
    let thumbnails = ResizeArray<Guid * byte[]>()
    let themes = ResizeArray<bool>()
    let motions = ResizeArray<bool>()
    let wraps = ResizeArray<bool>()
    let links = ResizeArray<string>()
    let copies = ResizeArray<string>()
    let downloads = ResizeArray<string * byte[]>()
    let connects = ResizeArray<string>()
    let browserSucceeded = ref 0
    let restoreCalls = ref 0
    let posts = ref 0
    let mutable generation = 7
    let mutable connected = true
    let mutable sendsSucceed = true
    let mutable editing = false
    let mutable dark = false
    let mutable hostHandler: (WireEvent -> unit) option = None
    let mutable closedHandler: (string option -> unit) option = None

    member _.Toasts = toasts |> List.ofSeq
    member _.UndoToasts = undos |> List.ofSeq
    member _.Events = events |> List.ofSeq
    member _.AuthEvents = authEvents |> List.ofSeq
    member _.Commands = commands |> List.ofSeq
    member _.SavedPrefs = savedPrefs |> List.ofSeq
    member _.DraftWrites = draftWrites |> List.ofSeq
    member _.Thumbnails = thumbnails |> List.ofSeq
    member _.AppliedThemes = themes |> List.ofSeq
    member _.ReducedMotion = motions |> List.ofSeq
    member _.CodeWraps = wraps |> List.ofSeq
    member _.OpenLinks = links |> List.ofSeq
    member _.Copied = copies |> List.ofSeq
    member _.Downloads = downloads |> List.ofSeq
    member _.Connects = connects |> List.ofSeq
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

    member _.SystemDark
        with get () = dark
        and set value = dark <- value

    /// 入口壳的协议事件回调（Build 里订阅进去的那个）。
    member _.Deliver(ev: WireEvent) = hostHandler |> Option.iter (fun handler -> handler ev)

    /// 入口壳的连接关闭回调。
    member _.Close(detail: string option) = closedHandler |> Option.iter (fun handler -> handler detail)

    member _.Ports(buildViews: unit -> ShellViews) : ShellPorts =
        { Connect =
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
          After = fun _ _ -> { new IDisposable with member _.Dispose() = () }
          Every = fun _ _ -> { new IDisposable with member _.Dispose() = () }
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
          PrefsLoad = fun () -> prefs0
          PrefsSave = fun next -> savedPrefs.Add next
          TryLoadClientToml = fun () -> None
          TryLoadBrowserConnection = fun () -> async { return None }
          SaveBrowserConnection = fun _ _ _ _ -> ()
          BrowserViewportWidth = fun () -> 0.0
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
                { new IDisposable with member _.Dispose() = hostHandler <- None }
          SubscribeClosed =
            fun handler ->
                closedHandler <- Some handler
                { new IDisposable with member _.Dispose() = closedHandler <- None }
          CloseWindow = fun () -> false
          ClientName = fun () -> "测试客户端"
          MakeThumbnail = fun id bytes -> thumbnails.Add(id, bytes)
          BuildViews = buildViews }

/// 浮层外观（与 OverlayDesk 自己的测试同值；这里只求能建树）。
module Look =
    let overlay () : OverlayDeskLook =
        { Layer =
            { Scrim = SolidColorBrush(Color.FromArgb(80uy, 0uy, 0uy, 0uy))
              Surface = Brushes.White
              Edge = Brushes.Gray
              EdgeWidth = Cell(Thickness 1.0)
              DialogRadius = Cell(CornerRadius 8.0)
              PopupRadius = Cell(CornerRadius 6.0)
              DialogPadding = Cell(Thickness 16.0)
              PopupPadding = Cell(Thickness 2.0)
              DialogShadow = Cell(BoxShadows())
              PopupShadow = Cell(BoxShadows())
              ViewportInset = Cell 12.0
              PopupGap = Cell 4.0
              PopupMinWidth = Cell 180.0
              ScrimFade = Cell TimeSpan.Zero
              ScrimEasing = Avalonia.Animation.Easings.CubicEaseOut() }
          Toasts =
            { Surface = Brushes.White
              Edge = Brushes.Gray
              Text = Brushes.Black
              Accent = (fun _ -> Brushes.Gray)
              AccentWidth = Cell 2.0
              AccentRadius = Cell(CornerRadius 999.0)
              AccentOpacity = Cell 0.6
              EdgeWidth = Cell(Thickness 1.0)
              Radius = Cell(CornerRadius 8.0)
              Padding = Cell(Thickness(16.0, 12.0))
              Shadow = Cell(BoxShadows())
              FontSize = Cell 12.0
              LineHeight = Cell 18.0
              ColumnSpacing = Cell 12.0
              Spacing = Cell 8.0
              Margin = Cell(Thickness(24.0, 0.0, 24.0, 32.0))
              MaxWidth = Cell 480.0
              BodyMaxHeight = Cell 160.0
              Fade = Cell TimeSpan.Zero
              FadeEasing = Avalonia.Animation.Easings.CubicEaseOut() }
          Menu =
            { Text = Brushes.Black
              Muted = Brushes.Gray
              Faint = Brushes.LightGray
              Danger = Brushes.Red
              Accent = Brushes.Blue
              Check = (fun b -> Border(Width = 12.0, Height = 12.0, Background = b))
              Chevron = (fun b -> Border(Width = 10.0, Height = 10.0, Background = b))
              FontSize = Cell 12.0
              HintFontSize = Cell 10.0
              IconSlotWidth = Cell 16.0
              IconHeight = Cell 16.0
              RightSlotMinWidth = Cell 16.0
              HintMargin = Cell(Thickness(12.0, 0.0, 0.0, 0.0))
              ColumnSpacing = Cell 6.0
              ItemPadding = Cell(Thickness(8.0, 4.0))
              ItemRadius = Cell(CornerRadius 4.0)
              ItemMinHeight = Cell 32.0
              ItemSpacing = Cell 2.0
              MaxHeight = Cell 200.0
              DividerMargin = Cell(Thickness(6.0, 2.0))
              HeaderMargin = Cell(Thickness(8.0, 6.0, 8.0, 2.0))
              SelectPadding = Cell(Thickness(8.0, 4.0))
              SelectRadius = Cell(CornerRadius 8.0)
              SelectEdgeWidth = Cell(Thickness 1.0)
              SelectCaptionMaxWidth = Cell 260.0 }
          MenuMinWidth = Cell 200.0
          GroupedMenuMinWidth = Cell 240.0
          DwellFailureMs = Cell 9000.0
          DwellWarningMs = Cell 6500.0
          DwellDefaultMs = Cell 4000.0
          DismissTick = TimeSpan.FromMilliseconds 100.0 }

/// 测试台：一个组合者 + 真实视图树 + 假端口。
type Rig =
    { Shell: ShellComposer
      Views: ShellViews
      Fake: FakePorts
      Theme: ThemeManager
      Summary: ConversationSummary
      ImportDesks: ResizeArray<Wanxiang2.TransferDesk.ImportDesk>
      ExportDesks: ResizeArray<Wanxiang2.TransferDesk.ExportDesk> }

module Rig =
    let private summaryOf (id: Guid) : ConversationSummary =
        { id = id
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
          lastCommitId = 0UL }

    /// 建一套完整测试台：BuildViews 交给组合者，Build() 由测试自己调（要断言的时点不同）。
    let createWith (prefs0: UiPrefs) : Rig =
        let theme = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
        let styles = new ThemeStyles(theme)
        styles.Install Application.Current.Styles
        let fake = FakePorts(prefs0)
        let importDesks = ResizeArray<Wanxiang2.TransferDesk.ImportDesk>()
        let exportDesks = ResizeArray<Wanxiang2.TransferDesk.ExportDesk>()
        let mutable shell: ShellComposer = Unchecked.defaultof<_>
        let mutable built: ShellViews option = None

        let buildViews () : ShellViews =
            let root = Grid()
            let overlay = OverlayDesk(root, Look.overlay ())
            let brandLogo (size: float) = Border(Width = size, Height = size) :> Control
            let composer =
                ComposerDesk(
                    ComposerLooks.ofTheme theme styles,
                    shell.ComposerActions((fun () -> ()), (fun _ -> ()), (fun () -> false)))
            let approval = ApprovalDesk(theme, overlay, ignore)
            let cards =
                MessageCards(
                    CardLooks.ofTheme theme styles,
                    { Tint =
                        CodeTint
                            { HighlightHtml = (fun _ -> None)
                              Background = (fun work -> work ())
                              After = (fun _ work -> work ())
                              Synchronous = true }
                      MathHtml = (fun _ -> None)
                      Wrap = Wanxiang2.MarkdownView.CodeWrapMemory false
                      Clock = CaretClock()
                      Touch = CardTouch()
                      Now = fun () -> DateTimeOffset.UnixEpoch })
            let chat =
                ChatScreen(
                    ChatScreenLooks.ofTheme theme styles,
                    cards,
                    composer,
                    approval,
                    overlay,
                    brandLogo,
                    shell.ChatActions())
            let sidebar = SidebarDesk(theme, overlay, shell.SidebarActions(), brandLogo)
            let settings = SettingsScreen(theme, overlay, shell.SettingsHooks(), prefs0)
            let onboarding = OnboardingDesk(theme, overlay, shell.OnboardingHooks())
            let workspace =
                Workspace(
                    theme,
                    root,
                    sidebar,
                    chat,
                    composer,
                    shell.WorkspaceHooks(fun () -> false))
            let palette = PaletteDesk(theme, overlay, (fun () -> false))
            let modelSwitch = ModelSwitchDesk(theme, overlay)
            let dialogs = DialogsDesk(theme, overlay)
            { Sidebar = sidebar
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
                            lineage)
                    exportDesks.Add desk
                    desk
              NewImportDesk =
                fun submit ->
                    let desk = new Wanxiang2.TransferDesk.ImportDesk(theme, overlay, (fun () -> null :> TopLevel), submit)
                    importDesks.Add desk
                    desk
              SettingsHostVisible = fun visible -> settings.View.IsVisible <- visible
              WorkspaceHostVisible = fun visible -> workspace.Host.IsVisible <- visible
              SettingsFocusInitial = fun () -> settings.FocusInitial()
              SettingsShowSection =
                fun section ->
                    section
                    |> Option.iter (fun key ->
                        Wanxiang2.SettingsScreen.SettingsPart.ofKey key
                        |> Option.iter settings.ShowSection) }

        let ports =
            fake.Ports(fun () ->
                let views = buildViews ()
                built <- Some views
                views)
        shell <- ShellComposer(ports)
        shell.Build() |> ignore
        { Shell = shell
          Views = built.Value
          Fake = fake
          Theme = theme
          Summary = summaryOf (Guid "11111111-1111-1111-1111-111111111111")
          ImportDesks = importDesks
          ExportDesks = exportDesks }

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
