module Wanxiang2.ChatScreen.Tests.Tests

// 真实组合：主题外观 + ChatHeader / ChatPane / MessageCards / ComposerDesk / ApprovalDesk / OverlayDesk。

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang.Core
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.CodeTint
open Wanxiang2.MessageCardView
open Wanxiang2.ComposerDesk
open Wanxiang2.ApprovalDesk
open Wanxiang2.OverlayDesk
open Wanxiang2.ChatScreen
open Wanxiang2.ChatScreenTests

type private Rig =
    {
        Screen: ChatScreen
        Log: ResizeArray<string>
        Timers: ResizeArray<unit -> unit>
        Window: Window
    }

let private rig () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UnixEpoch), false)
    let styles = ThemeStyles theme
    styles.Install Application.Current.Styles
    let log = ResizeArray<string>()
    let timers = ResizeArray<unit -> unit>()
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())

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

    let cards = MessageCards(CardLooks.ofTheme theme styles, host)

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

    let approvals = ApprovalDesk(theme, overlay, ignore)

    let cardActions =
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

    let actions =
        {
            Card = cardActions
            RenameTitle = fun t -> log.Add("rename:" + t)
            ToggleSidebar = fun () -> log.Add "sidebar"
            OpenSessionSettings = ignore
            ForkFromHere = ignore
            ExportConversation = ignore
            StopGeneration = ignore
            RequestOlderHistory = fun () -> log.Add "older"
            FocusHome = fun () -> log.Add "home"
            OpenParent = fun id -> log.Add(sprintf "parent:%O" id)
            SiblingSelected = fun id -> log.Add(sprintf "sibling:%O" id)
            After =
                fun _ f ->
                    timers.Add f

                    { new IDisposable with
                        member _.Dispose() = ()
                    }
        }

    let screen =
        ChatScreen(
            ChatScreenMetrics.ofTheme theme styles,
            cards,
            composer,
            approvals,
            overlay,
            (fun s -> Border(Width = s, Height = s) :> Control),
            actions
        )

    let column = DockPanel()
    DockPanel.SetDock(composer.Host, Dock.Bottom)
    column.Children.Add composer.Host
    column.Children.Add screen.Host
    root.Children.Insert(0, column)
    let w = Desk.show root 800.0 600.0

    {
        Screen = screen
        Log = log
        Timers = timers
        Window = w
    }

let private msg role text commit =
    { MessageView.empty with
        role = role
        text = text
        commitId = Some commit
    }

let private frame messages streaming =
    {
        Messages = messages
        Streaming = streaming
        Error = None
        FontSize = 14.5
        AutoCollapseReasoning = false
        Title = None
        CanEditTitle = false
        IsGenerating = false
        GeneratingText = ""
        CanStop = false
        ConversationChrome = false
        CompactMode = false
        ReducedMotion = false
        Usage = None
        MissingAttachments = Set.empty
    }

let private exchange n =
    [
        for i in 0 .. n - 1 do
            yield msg "user" (sprintf "问 %d" i) (uint64 (2 * i + 1))
            yield msg "assistant" (sprintf "答 %d" i) (uint64 (2 * i + 2))
    ]

let private invoke (c: Control) =
    ((ControlAutomationPeer.CreatePeerForElement c :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``渲染次序：已提交卡按身份复用，流式卡外形不变时原位更新、换形重建，错误卡追加在尾`` () =
    Headless.run (fun () ->
        let r = rig ()
        let s = r.Screen
        s.Render(frame (exchange 2) None)
        Assert.Equal(4, s.Pane.Messages.Children.Count)
        let first = s.Pane.Messages.Children.[0]

        let stream =
            { MessageView.empty with
                role = "assistant"
                text = "正在"
            }

        s.Render(frame (exchange 2) (Some stream))
        Assert.Equal(5, s.Pane.Messages.Children.Count)
        Assert.Same(first, s.Pane.Messages.Children.[0])
        let streamCard = s.Pane.Messages.Children.[4]
        s.Render(frame (exchange 2) (Some { stream with text = "正在写第二句" }))
        Assert.Same(streamCard, s.Pane.Messages.Children.[4])

        s.Render(
            frame
                (exchange 2)
                (Some
                    { stream with
                        reasoning = "想"
                        text = "正在写第二句"
                    })
        )

        Assert.NotSame(streamCard, s.Pane.Messages.Children.[4])

        let err: GenerationError =
            {
                kind = ProviderTimeout
                message = "超时"
                retryable = true
                detail = None
                retryAfterSeconds = None
            }

        s.Render(
            { frame (exchange 2) None with
                Error = Some(err, ignore)
            }
        )

        Assert.Equal(5, s.Pane.Messages.Children.Count)
        Assert.Equal<ScreenKey option>(None, s.Mounted.[4])
        r.Window.Close())

[<Fact>]
let ``长对话直接呈现完整卡片列表；定位消息并高亮到宿主计时结束`` () =
    Headless.run (fun () ->
        let r = rig ()
        let s = r.Screen
        s.Render(frame (exchange 50) None)
        Assert.Equal(100, s.Pane.Messages.Children.Count)
        Assert.Equal(0, s.HiddenMessageCount)
        Assert.True(s.RevealAndHighlight 3UL)

        Assert.True(
            s.Mounted
            |> Array.exists (function
                | Some k -> k.commit = 3UL
                | None -> false)
        )

        Assert.True s.Pane.Follow.RevealPinned
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(Some 3UL, s.HighlightedCommit)
        Assert.Equal(1, r.Timers.Count)
        r.Timers.[0]()
        Assert.Equal(None, s.HighlightedCommit)
        Assert.Equal(Some 3UL, s.LastFlashedCommit)
        Assert.False(s.ScrollToMessage 9999UL)
        r.Window.Close())

[<Fact>]
let ``顶栏：改名、侧栏开关交宿主；面包屑可点时回到父对话，父对话已删时只是文字`` () =
    Headless.run (fun () ->
        let r = rig ()
        let s = r.Screen

        s.Render
            { frame (exchange 1) None with
                ConversationChrome = true
                Title = Some "旧"
                CanEditTitle = true
            }

        s.Header.BeginRename()
        s.Header.TitleEditor.Text <- "新"
        s.Header.CommitRename false
        invoke s.Header.SidebarButton
        let parent = Guid(1, 0s, 0s, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy)

        s.ApplyBreadcrumb(
            Some
                {
                    parentId = parent
                    parentAvailable = false
                    text = "分叉自 A"
                    description = "父对话已删除"
                }
        )

        Assert.Equal("分叉自 A", s.BreadcrumbText)
        invoke s.Header.CrumbAction

        s.ApplyBreadcrumb(
            Some
                {
                    parentId = parent
                    parentAvailable = true
                    text = "分叉自 A"
                    description = "回到 A"
                }
        )

        invoke s.Header.CrumbAction
        Assert.Equal<string list>([ "rename:新"; "sidebar"; sprintf "parent:%O" parent ], List.ofSeq r.Log)
        r.Window.Close())

[<Fact>]
let ``用户消息有多个版本时卡下出版本条，按「下一个版本」交宿主`` () =
    Headless.run (fun () ->
        let r = rig ()
        let s = r.Screen
        let other = Guid(2, 0s, 0s, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy)

        s.SlotSiblingProvider(fun commit _ ->
            if commit = 1UL then
                Some
                    {
                        index = 0
                        count = 2
                        targets = [ Guid.Empty; other ]
                    }
            else
                None)

        s.Render(frame (exchange 1) None)
        let next = Desk.named s.Host "下一个版本"
        Assert.True next.IsSome
        invoke next.Value
        Assert.Equal<string list>([ sprintf "sibling:%O" other ], List.ofSeq r.Log)
        r.Window.Close())

[<Fact>]
let ``整屏各状态渲染与可见性断言：空态、对话、生成中、流式、面包屑与紧凑模式`` () =
    Headless.run (fun () ->
        let r = rig ()
        let s = r.Screen

        // 1. 空态
        s.Render
            { frame [] None with
                Title = Some "新对话"
                CanEditTitle = false
            }

        s.Pane.ShowEmpty(ChatEmptyState.EmptyConversation, None)
        Assert.True s.Pane.IsEmptyShown
        Assert.Equal("新对话", s.Header.Title)
        Assert.False s.Header.IsEditing

        // 2. 对话态
        s.Pane.HideEmpty()

        s.Render
            { frame (exchange 2) None with
                ConversationChrome = true
                Title = Some "京都行程"
                CanEditTitle = true
            }

        Assert.False s.Pane.IsEmptyShown
        Assert.Equal(4, s.Pane.Messages.Children.Count)
        Assert.Equal("京都行程", s.Header.Title)

        // 3. 生成中状态
        s.Render
            { frame (exchange 2) None with
                ConversationChrome = true
                Title = Some "京都行程"
                CanEditTitle = true
                IsGenerating = true
                GeneratingText = "正在思考"
            }

        Assert.Equal(1.0, s.Header.GeneratingChip.Opacity)

        // 4. 流式消息追加
        let stream =
            { MessageView.empty with
                role = "assistant"
                text = "流式输出中"
            }

        s.Render
            { frame (exchange 2) (Some stream) with
                ConversationChrome = true
                Title = Some "京都行程"
                CanEditTitle = true
            }

        Assert.Equal(5, s.Pane.Messages.Children.Count)

        // 5. 面包屑有父对话与已删除父对话
        let parentId = Guid.NewGuid()

        s.ApplyBreadcrumb(
            Some
                {
                    parentId = parentId
                    parentAvailable = true
                    text = "分叉自「旅行」"
                    description = "回到父对话「旅行」"
                }
        )

        Assert.Equal("分叉自「旅行」", s.BreadcrumbText)
        Assert.True s.Header.CrumbAction.IsVisible

        s.ApplyBreadcrumb(
            Some
                {
                    parentId = parentId
                    parentAvailable = false
                    text = "分叉自「旅行」"
                    description = "父对话已删除"
                }
        )

        Assert.Equal("分叉自「旅行」", s.BreadcrumbText)

        // 6. 紧凑模式
        s.Render
            { frame (exchange 2) None with
                ConversationChrome = true
                Title = Some "京都行程"
                CanEditTitle = true
                CompactMode = true
                IsGenerating = true
            }

        Assert.False s.Header.GeneratingChip.IsVisible

        s.Render
            { frame (exchange 2) None with
                ConversationChrome = true
                Title = Some "京都行程"
                CanEditTitle = true
                CompactMode = false
            }

        // 7. 只读标题模式
        s.Render
            { frame (exchange 2) None with
                ConversationChrome = true
                Title = Some "加载中…"
                CanEditTitle = false
            }

        Assert.Equal("加载中…", s.Header.Title)
        r.Window.Close())
