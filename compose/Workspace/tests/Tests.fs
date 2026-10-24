module Wanxiang2.Workspace.Tests.Tests

// 真实组合：ShellDesk + SidebarDesk + ChatScreen + ComposerDesk + OverlayDesk。

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Threading
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.CodeTint
open Wanxiang2.MessageCardView
open Wanxiang2.ComposerDesk
open Wanxiang2.ApprovalDesk
open Wanxiang2.OverlayDesk
open Wanxiang2.SidebarDesk
open Wanxiang2.ChatScreen
open Wanxiang2.Workspace
open Wanxiang2.WorkspaceTests

let private rig (collapsed: bool) =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
    let styles = ThemeStyles theme
    styles.Install Application.Current.Styles
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())

    let logo (s: float) =
        Border(Width = s, Height = s) :> Control

    let host =
        {
            Tint =
                CodeTint
                    {
                        HighlightHtml = (fun _ -> None)
                        Background = (fun f -> f ())
                        After = (fun _ f -> f ())
                        Synchronous = true
                    }
            MathHtml = (fun _ -> None)
            Wrap = Wanxiang2.MarkdownView.CodeWrapMemory false
            Clock = CaretClock()
            Touch = CardTouch()
            Now = fun () -> DateTimeOffset.UnixEpoch
        }

    let composer =
        ComposerDesk(
            {
                Submit = (fun _ -> true)
                TextChanged = (fun _ -> ())
                CaretChanged = (fun _ -> ())
                StopGeneration = ignore
                PickAttachment = ignore
                RemoveAttachment = ignore
                OpenModelPicker = ignore
                DropFiles = ignore
                PasteFromClipboard = (fun () -> false)
                OpenSessionSettings = ignore
            }
        )

    let ws: Workspace option ref = ref None

    let chat =
        ChatScreen(
            ChatScreenMetrics.ofTheme theme styles,
            MessageCards(CardLooks.ofTheme theme styles, host),
            composer,
            ApprovalDesk(theme, overlay, ignore),
            overlay,
            logo,
            {
                Card =
                    {
                        CopyText = ignore
                        Regenerate = ignore
                        EditAndFork = ignore
                        DeleteMessage = ignore
                        DownloadAttachment = ignore
                        OpenLink = ignore
                        CopyLink = None
                        Recover = None
                    }
                RenameTitle = ignore
                ToggleSidebar = (fun () -> ws.Value |> Option.iter (fun w -> w.ToggleSidebar() |> ignore))
                OpenSessionSettings = ignore
                ForkFromHere = ignore
                ExportConversation = ignore
                StopGeneration = ignore
                RequestOlderHistory = ignore
                FocusHome = ignore
                OpenParent = ignore
                SiblingSelected = ignore
                After =
                    fun _ _ ->
                        { new IDisposable with
                            member _.Dispose() = ()
                        }
            }
        )

    let sidebar =
        SidebarDesk(
            theme,
            overlay,
            {
                newConversation = ignore
                openConversation = ignore
                renameConversation = ignore
                deleteConversation = ignore
                setPinned = (fun _ _ -> ())
                setArchived = (fun _ _ -> ())
                selectionAllPinned = (fun _ -> false)
                setPinnedMany = (fun _ _ -> ())
                setArchivedMany = (fun _ _ -> ())
                deleteMany = ignore
                duplicateAsFork = ignore
                exportConversation = ignore
                openSettings = ignore
                reconnect = ignore
                toggleArchivedVisibility = ignore
                closeNavigation = ignore
                enterSelection = ignore
                toggleSelected = ignore
                exitSelection = ignore
                toggleSelectAllVisible = ignore
            },
            logo
        )

    let workspace =
        Workspace(
            theme,
            root,
            sidebar,
            chat,
            composer,
            {
                PrefersTouch = (fun () -> true)
                SidebarWidth = (fun () -> 280.0)
                InitialSidebarCollapsed = collapsed
            }
        )

    ws.Value <- Some workspace
    let w = Desk.show root 1200.0 800.0
    workspace, chat, sidebar, w

[<Fact>]
let ``宽窗是桌面布局；浏览器喂窄视口后进紧凑模式，顶栏按钮放大到触控目标`` () =
    Headless.run (fun () ->
        let ws, chat, _, w = rig false
        Assert.False ws.State.compactMode
        Assert.True ws.Splitter.IsVisible
        let desktop = chat.Header.SidebarButton.Width
        ws.ObserveViewport 480.0
        Dispatcher.UIThread.RunJobs()
        Assert.True ws.State.compactMode
        Assert.False ws.State.compactNavigationOpen
        Assert.Equal(LayoutPolicy.compactActionTarget, chat.Header.SidebarButton.Width)
        Assert.True(chat.Header.SidebarButton.Width > desktop)
        // 窗口变宽也不再按自身宽度回桌面：CSS 视口为准
        w.Width <- 1400.0
        Dispatcher.UIThread.RunJobs()
        Assert.True ws.State.compactMode
        ws.ObserveViewport 1400.0
        Assert.False ws.State.compactMode
        w.Close())

[<Fact>]
let ``顶栏侧栏按钮经宿主切换：桌面折叠侧栏，紧凑模式开合抽屉`` () =
    Headless.run (fun () ->
        let ws, chat, _, w = rig false

        let press () =
            ((Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement chat.Header.SidebarButton :> obj)
            :?> Avalonia.Automation.Provider.IInvokeProvider)
                .Invoke()

            Dispatcher.UIThread.RunJobs()

        press ()
        Assert.True ws.State.sidebarCollapsed
        Assert.False ws.Splitter.IsVisible
        ws.ObserveViewport 480.0
        press ()
        Assert.True ws.State.compactNavigationOpen
        press ()
        Assert.False ws.State.compactNavigationOpen
        w.Close())

[<Fact>]
let ``Workspace.Render(ShellVS) 单入口幂等应用布局与抽屉状态`` () =
    Headless.run (fun () ->
        let ws, _, _, w = rig false

        let shellVS: Wanxiang2.WorkspaceState.ShellVS =
            {
                Compact = false
                SidebarOpen = true
                SidebarWidth = 280.0
                StartingPageVisible = true
            }

        ws.Render shellVS
        Dispatcher.UIThread.RunJobs()
        Assert.False ws.State.sidebarCollapsed

        let compactVS: Wanxiang2.WorkspaceState.ShellVS =
            {
                Compact = true
                SidebarOpen = true
                SidebarWidth = 280.0
                StartingPageVisible = false
            }

        ws.ObserveViewport 480.0
        ws.Render compactVS
        Dispatcher.UIThread.RunJobs()
        // 紧凑抽屉只由快捷键 / 遮罩 / Escape 开合，重投影不翻面
        Assert.False ws.State.compactNavigationOpen

        ws.ToggleSidebar() |> ignore
        Assert.True ws.State.compactNavigationOpen
        ws.Render compactVS
        Dispatcher.UIThread.RunJobs()
        Assert.True ws.State.compactNavigationOpen
        w.Close())
