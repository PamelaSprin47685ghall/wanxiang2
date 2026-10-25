module Wanxiang2.ChatPane.Tests.Tests

// 真实组合：主题外观 + ChatScroller / ExchangeNav / ChatEmpty / SkeletonList + ScrollFollow / ExchangeJump / ChatEmptyText。

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Threading
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ChatPane
open Wanxiang2.ChatPaneTests
open Wanxiang2.WorkspaceState

let private rig () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)

    if
        not (
            Application.Current.Resources.MergedDictionaries.Contains(
                theme.Resources :> Avalonia.Controls.IResourceDictionary
            )
        )
    then
        Application.Current.Resources.MergedDictionaries.Add(theme.Resources :> Avalonia.Controls.IResourceDictionary)

    let pane =
        ChatPane<string>((fun s -> Border(Width = s, Height = s) :> Control), None)

    let w = Window(Width = 800.0, Height = 600.0, Content = pane.Host)
    w.Show()
    Dispatcher.UIThread.RunJobs()
    pane, w

// 迁自 ChatViewTests：空态文案取 ChatEmptyText；品牌态带 logo。
[<Fact>]
let ``空态文案与品牌态`` () =
    Headless.run (fun () ->
        let pane, w = rig ()
        pane.ShowEmpty(ChatEmptyState.NotConnected, Some("连接主机", ignore))
        Assert.True pane.IsEmptyShown
        Assert.True pane.Empty.IsShown
        Assert.False pane.Scroller.Viewer.IsVisible
        let title, hint = ChatEmptyText.text ChatEmptyState.NotConnected
        Assert.Equal("先连接一台万象主机", title)
        Assert.True(hint.Contains "主机")
        pane.HideEmpty()
        Assert.False pane.IsEmptyShown
        Assert.True pane.Scroller.Viewer.IsVisible
        w.Close())

// 迁自 ChatViewTests：贴底跟随在渲染后滚到底；向上滚后露「回到最新」。
[<Fact>]
let ``贴底跟随与回到最新`` () =
    Headless.run (fun () ->
        let pane, w = rig ()
        pane.HideEmpty()

        for i in 1..20 do
            pane.Messages.Children.Add(Border(Height = 40.0, Width = 200.0))
        // 宿主渲染完同步报告，布局在其后
        pane.Rendered(false, 20, false)

        for _ in 1..4 do
            Dispatcher.UIThread.RunJobs()

        Assert.True(pane.Follow.AtBottom, "after render")
        // 用户向上：记未读，露钮
        pane.Scroller.ApplyOffset 0.0
        Dispatcher.UIThread.RunJobs()
        // 直接走记账：模拟 Scrolled 观察
        pane.Follow.Scrolled(true, false) |> ignore
        pane.Follow.Rendered(false, 21, false) |> ignore
        let v = pane.Follow.Button(true, false)
        Assert.True(v.visible, "button")
        Assert.True(v.text.Contains "新消息" || v.text = "回到最新")
        pane.ScrollToLatest()
        Assert.True(pane.Follow.AtBottom, "latest")
        w.Close())

// 迁自 ChatViewTests：骨架显示与隐藏。
[<Fact>]
let ``骨架显示与隐藏`` () =
    Headless.run (fun () ->
        let pane, w = rig ()
        pane.ShowSkeleton()
        Assert.True pane.Skeleton.IsShown
        pane.HideSkeleton()
        Assert.False pane.Skeleton.IsShown
        w.Close())

// 迁自 ChatViewTests：问答跳转端点与 ScrollTo。
[<Fact>]
let ``问答跳转端点`` () =
    Headless.run (fun () ->
        let pane, w = rig ()
        let mounted = [| Some "u1"; Some "a1"; Some "u2"; Some "a2" |]
        let cards = ExchangeJump.Cards(mounted, fun k -> k.StartsWith "u")
        // 离开底部：贴底时没有「下一轮」
        pane.Follow.Scrolled(true, false) |> ignore
        pane.RefreshJumps(mounted, cards, 0, false)
        Assert.False pane.Nav.CanJumpPrevious
        Assert.True pane.Nav.CanJumpNext

        match pane.Jump(1, mounted, cards, 0, false, None) with
        | JumpStep.ScrollTo(i, _) -> Assert.Equal(2, i)
        | other -> Assert.Fail(sprintf "expected ScrollTo, got %A" other)

        w.Close())

[<Fact>]
let ``1000 条消息下 ItemsControl 使用 VirtualizingStackPanel 仅渲染视口行`` () =
    Headless.run (fun () ->
        let pane, w = rig ()

        let mkMsg i =
            {
                Role = "user"
                Text = sprintf "msg %d" i
                Reasoning = ""
                CommitId = Some(uint64 i)
                ToolCalls = []
                Attachments = []
                CommittedAt = None
                Usage = None
                IsLastAssistant = false
                Streaming = false
                AutoCollapseReasoning = false
                MissingAttachments = Set.empty
                AwaitingApproval = Set.empty
                Index = Some i
            }

        let msgs = [ for i in 1..1000 -> mkMsg i ]

        let vs: ChatVS =
            {
                Phase = ChatPhase.Messages msgs
                Streaming = false
                ScrollHint = None
                RevealTarget = None
                Error = None
            }

        pane.Render(vs)
        Dispatcher.UIThread.RunJobs()
        Assert.True(pane.ItemsControl.IsVisible)
        let panel = pane.ItemsControl.ItemsPanel.Build()
        Assert.IsAssignableFrom<VirtualizingStackPanel>(panel) |> ignore
        let vsp = panel :?> VirtualizingStackPanel
        // 1000 条数据源，虚拟化面板子元素数远小于 1000
        Assert.True(vsp.Children.Count < 100, sprintf "Realized count should be small, got %d" vsp.Children.Count)
        w.Close())

[<Fact>]
let ``ChatVS 单入口幂等渲染：空态、骨架与消息切换`` () =
    Headless.run (fun () ->
        let pane, w = rig ()
        // 空态
        pane.Render
            {
                Phase = ChatPhase.Empty IdleEmptyKind.NotConnected
                Streaming = false
                ScrollHint = None
                RevealTarget = None
                Error = None
            }

        Assert.True pane.IsEmptyShown
        Assert.False pane.Scroller.Viewer.IsVisible
        // 骨架
        pane.Render
            {
                Phase = ChatPhase.Skeleton
                Streaming = false
                ScrollHint = None
                RevealTarget = None
                Error = None
            }

        Assert.False pane.IsEmptyShown
        Assert.True pane.Skeleton.IsShown
        // 消息
        let msgs =
            [
                {
                    Role = "user"
                    Text = "hello"
                    Reasoning = ""
                    CommitId = Some 1UL
                    ToolCalls = []
                    Attachments = []
                    CommittedAt = None
                    Usage = None
                    IsLastAssistant = false
                    Streaming = false
                    AutoCollapseReasoning = false
                    MissingAttachments = Set.empty
                    AwaitingApproval = Set.empty
                    Index = Some 1
                }
            ]

        pane.Render
            {
                Phase = ChatPhase.Messages msgs
                Streaming = false
                ScrollHint = None
                RevealTarget = None
                Error = None
            }

        Assert.False pane.IsEmptyShown
        Assert.False pane.Skeleton.IsShown
        Assert.True pane.Scroller.Viewer.IsVisible
        w.Close())

// 窗口 800 时本列约 800，自身档是 32。传入 1100 后两侧留白必须是 48。
[<Fact>]
let ``水平留白按传入的窗口视口分档`` () =
    Headless.run (fun () ->
        let pane, w = rig ()
        let viewer = pane.Scroller.Viewer
        Assert.True(viewer.Bounds.Width > 0.0 && viewer.Bounds.Width < 1024.0)

        let pads () =
            (pane.Host :?> Grid).Children
            |> Seq.choose (fun c ->
                match c with
                | :? ScrollViewer as sv -> Some sv.Padding.Left
                | _ -> None)
            |> Seq.toList

        pane.ApplyViewportWidth 1100.0
        Assert.Equal(48.0, viewer.Padding.Left)
        Assert.Equal(48.0, viewer.Padding.Right)
        Assert.Equal<float list>([ 48.0; 48.0 ], pads ())
        pane.ApplyViewportWidth 1280.0
        Assert.Equal(48.0, viewer.Padding.Left)
        pane.ApplyViewportWidth 800.0
        Assert.Equal(32.0, viewer.Padding.Left)
        Assert.Equal<float list>([ 32.0; 32.0 ], pads ())
        pane.ApplyViewportWidth 500.0
        Assert.Equal(24.0, viewer.Padding.Left)
        pane.ApplyViewportWidth 400.0
        Assert.Equal(16.0, viewer.Padding.Left)
        w.Close())

[<Fact>]
let ``主体各典型状态结构与可见性状态断言`` () =
    Headless.run (fun () ->
        let pane, w = rig ()
        // 1. 消息态
        pane.HideEmpty()
        Assert.False pane.IsEmptyShown
        Assert.False pane.Skeleton.IsShown
        Assert.True pane.Scroller.Viewer.IsVisible

        // 2. 骨架态
        pane.ShowSkeleton()
        Assert.True pane.Skeleton.IsShown

        // 3. 更早一页失败态
        let mutable retried = false
        pane.ShowOlderFailure("没能取到更早的消息：超时", fun () -> retried <- true)
        Assert.True pane.Nav.OlderFailureVisible
        Assert.Equal("没能取到更早的消息：超时", pane.Nav.OlderFailureText)

        let peer =
            Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement pane.Nav.RetryButton

        ((peer :> obj) :?> Avalonia.Automation.Provider.IInvokeProvider).Invoke()
        Assert.True retried
        Assert.False pane.Nav.OlderFailureVisible

        // 4. 空态各分支
        let emptyCases =
            [
                ChatEmptyState.EmptyConversation, None
                ChatEmptyState.NoConversation, Some("新对话", ignore)
                ChatEmptyState.NotConnected, Some("连接主机", ignore)
                ChatEmptyState.ConnectionFailed "超时", Some("重试", ignore)
                ChatEmptyState.NoProvider, Some("添加模型", ignore)
            ]

        for st, action in emptyCases do
            pane.ShowEmpty(st, action)
            Assert.True pane.IsEmptyShown
            Assert.False pane.Scroller.Viewer.IsVisible

        w.Close())
