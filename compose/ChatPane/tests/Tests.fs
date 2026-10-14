module Wanxiang2.ChatPane.Tests.Tests

// 真实组合：主题外观 + ChatScroller / ExchangeNav / ChatEmpty / SkeletonList + ScrollFollow / ScrollTween / ExchangeJump / BreathPhase / ChatEmptyText。

open System
open Avalonia.Controls
open Avalonia.Threading
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ChatPane
open Wanxiang2.ChatPaneTests

let private rig () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let styles = ThemeStyles(theme)
    let pane = ChatPane<string>(ChatPaneLooks.ofTheme theme styles, fun s -> Border(Width = s, Height = s) :> Control)
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
        for i in 1 .. 20 do
            pane.Messages.Children.Add(Border(Height = 40.0, Width = 200.0))
        // 宿主渲染完同步报告，布局在其后
        pane.Rendered(false, 20, false)
        for _ in 1 .. 4 do Dispatcher.UIThread.RunJobs()
        Assert.True(pane.Follow.AtBottom, "after render")
        // 用户向上：记未读，露钮
        pane.Scroller.SetOffset 0.0
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
