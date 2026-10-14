module Program

// 用旧 AppShell.Build 的布局段（旧 Sidebar / ChatView / Composer + 审批附属位 + ShellDesk）生成 Workspace 的整树金样。
// 侧栏与对话屏只出根节点一行（各有自己的金样）；输入区连同审批位整棵展开。

open System
open System.IO
open System.Text
open System.Threading
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Themes.Fluent
open Wanxiang.UI

type GenApp() =
    inherit Application()
    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        // 与旧 App.fs 一致：装共享类选择器样式
        Wanxiang2.Theme.ThemeStyles(SharedTheme.manager).Install this.Styles

type Builder =
    static member BuildAvaloniaApp() =
        AppBuilder.Configure<GenApp>().UseSkia().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

let private font = FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body
let logo (size: float) = Border(Width = size, Height = size, Background = Brushes.Transparent) :> Control

[<EntryPoint>]
let main argv =
    let out = argv[0]
    let session = HeadlessUnitTestSession.StartNew(typeof<Builder>, AvaloniaTestIsolationLevel.PerTest)
    let sb = StringBuilder()
    let run (f: unit -> unit) = session.Dispatch(Action f, CancellationToken.None).GetAwaiter().GetResult()
    let card: Wanxiang2.MessageCardView.CardActions =
        { CopyText = ignore; Regenerate = ignore; EditAndFork = ignore; DeleteMessage = ignore; DownloadAttachment = ignore; OpenLink = ignore; CopyLink = None; Recover = None }
    for name, width in Cases.names do
        run (fun () ->
            SharedTheme.manager.Motion.SetReduced true
            let root = Grid()
            let overlay = OverlayHost(root)
            let sidebar =
                Sidebar(overlay,
                        { newConversation = ignore; openConversation = ignore; renameConversation = ignore; deleteConversation = ignore
                          setPinned = (fun _ _ -> ()); setArchived = (fun _ _ -> ()); selectionAllPinned = (fun _ -> false)
                          setPinnedMany = (fun _ _ -> ()); setArchivedMany = (fun _ _ -> ()); deleteMany = ignore
                          duplicateAsFork = ignore; exportConversation = ignore; openSettings = ignore; reconnect = ignore
                          toggleArchivedVisibility = ignore; closeNavigation = ignore },
                        logo)
            sidebar.Build()
            let chat =
                ChatView({ renameTitle = ignore; openSessionSettings = ignore; forkFromHere = ignore; stopGeneration = ignore; requestOlderHistory = ignore
                           retryLast = ignore; toggleSidebar = ignore; focusHome = ignore; message = card }, logo)
            chat.Build()
            let composer =
                Composer({ submit = (fun _ -> true); stopGeneration = ignore; pickAttachment = ignore; removeAttachment = ignore
                           openModelPicker = ignore; dropFiles = ignore; pasteFromClipboard = (fun () -> false) })
            composer.Build()
            // AppShell 空闲面：输入区可用（IdleSurface.composerEnabled）
            composer.SetEnabled(true, "")
            let approvals = Wanxiang2.ApprovalDesk.ApprovalDesk(SharedTheme.manager, overlay.Desk, ignore)
            approvals.SetConversation(Some Cases.conversation)
            composer.SetAccessory approvals.View
            // 以下逐行同 AppShell.Build（initialNavigation.sidebarCollapsed = false）
            let workspace = Grid()
            let shellGrid = Grid()
            let splitter =
                GridSplitter(
                    Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ResizeDirection = GridResizeDirection.Columns,
                    ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                    KeyboardIncrement = 8.0,
                    DragIncrement = 1.0,
                    Focusable = true,
                    Cursor = new Cursor(StandardCursorType.SizeWestEast),
                    IsVisible = true)
            AutomationProperties.SetName(splitter, "调整侧边栏宽度")
            Interaction.surface splitter Interaction.HoverVariant.plain |> ignore
            Interaction.reportFocusOrigin splitter
            let hooks: Wanxiang2.ShellDesk.ShellHooks =
                { SetCompactMode = fun compact -> composer.SetCompactMode compact; sidebar.SetCompactMode compact; chat.SetCompactMode compact
                  SetStartMode = fun centered -> chat.SetStartMode centered
                  FocusComposer = fun () -> composer.Focus()
                  FocusSidebarSearch = fun () -> sidebar.FocusSearch()
                  PrefersTouch = fun () -> true }
            let shell = Wanxiang2.ShellDesk.ShellDesk(SharedTheme.manager, shellGrid, sidebar, splitter, chat, composer, hooks, false, (fun () -> 280.0))
            workspace.Children.Add shellGrid
            shell.ApplyViewport width |> ignore
            match name with
            | "wide" | "compact" -> ()
            | "collapsed" -> shell.ToggleSidebar() |> ignore
            | "starting" -> shell.SetStarting true |> ignore
            | "approval" | "compact-approval" -> approvals.Handle Cases.approval |> ignore
            | "drawer" -> shell.SetCompactNavigation true |> ignore
            | _ -> failwith name
            Dump.cut <- fun c -> obj.ReferenceEquals(c, sidebar) || obj.ReferenceEquals(c, chat)
            sb.Append("== ").Append(name).Append('\n').Append((Dump.treeAt width 900.0 font workspace).TrimEnd('\n')).Append('\n') |> ignore)
    Directory.CreateDirectory(Path.GetDirectoryName out) |> ignore
    File.WriteAllText(out, sb.ToString())
    printfn "wrote %s" out
    0
