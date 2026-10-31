module Wanxiang2.ChatHeader.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Xunit
open Wanxiang2.WorkspaceState
open Wanxiang2.ChatHeader
open Wanxiang2.ChatHeader.Tests

let private invoke (b: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement b
    ((peer :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

let private key (c: Control) k =
    c.RaiseEvent(KeyEventArgs(Key = k, RoutedEvent = InputElement.KeyDownEvent))

[<Fact>]
let ``标题改名：Enter 提交去空白，Esc 取消，未变不报；不可编辑时不进编辑`` () =
    Headless.run (fun () ->
        let h = ChatHeader()
        let log = ResizeArray<string>()
        h.Renamed.Add log.Add
        let w = Window(Content = h.Host)
        w.Show()

        let baseVS: ChatHeaderVS =
            {
                Visible = true
                Compact = false
                InsetLeft = 0.0
                Title = TitleMode.Viewing("旧标题", false)
                GeneratingStatus = None
                Crumb = None
                ShowBack = true
                ShowMore = true
            }

        h.Render baseVS
        invoke h.TitleAction
        Assert.False h.IsEditing

        h.Render
            { baseVS with
                Title = TitleMode.Viewing("旧标题", true)
            }

        Assert.Equal("重命名对话：旧标题", AutomationProperties.GetName h.TitleAction)
        invoke h.TitleAction
        Assert.True h.IsEditing
        h.TitleEditor.Text <- "  新标题 "
        key h.TitleEditor Key.Enter
        Assert.False h.IsEditing
        Assert.Equal("新标题", h.Title)
        key h.TitleAction Key.F2
        h.TitleEditor.Text <- "不要"
        key h.TitleEditor Key.Escape
        Assert.Equal("新标题", h.Title)
        key h.TitleAction Key.F2
        key h.TitleEditor Key.Enter
        Assert.Equal<string list>([ "新标题" ], List.ofSeq log)
        w.Close())

[<Fact>]
let ``改名途中主机标题到达：草稿不丢，提交以草稿为准`` () =
    Headless.run (fun () ->
        let h = ChatHeader()
        let log = ResizeArray<string>()
        h.Renamed.Add log.Add
        let w = Window(Content = h.Host)
        w.Show()

        let vs: ChatHeaderVS =
            {
                Visible = true
                Compact = false
                InsetLeft = 0.0
                Title = TitleMode.Viewing("新对话", true)
                GeneratingStatus = None
                Crumb = None
                ShowBack = true
                ShowMore = true
            }

        h.Render vs
        h.BeginRename()
        h.TitleEditor.Text <- "我的草稿"

        h.Render
            { vs with
                Title = TitleMode.Viewing("自动标题", true)
            }

        Assert.True h.IsEditing
        Assert.Equal("我的草稿", h.TitleEditor.Text)
        h.CommitRename false
        Assert.Equal<string list>([ "我的草稿" ], List.ofSeq log)
        w.Close())

[<Fact>]
let ``生成中状态点桌面保留槽位、窄屏收起；更多按钮随对话；面包屑可点才报`` () =
    Headless.run (fun () ->
        let h = ChatHeader()
        let w = Window(Content = h.Host)
        w.Show()
        Assert.True h.GeneratingChip.IsVisible

        let baseVS: ChatHeaderVS =
            {
                Visible = true
                Compact = false
                InsetLeft = 0.0
                Title = TitleMode.Viewing("会话", true)
                GeneratingStatus = None
                Crumb = None
                ShowBack = false
                ShowMore = false
            }

        h.Render baseVS
        Assert.Equal(0.0, h.GeneratingChip.Opacity)

        h.Render
            { baseVS with
                GeneratingStatus = Some ""
            }

        Assert.Equal(1.0, h.GeneratingChip.Opacity)

        h.Render
            { baseVS with
                Compact = true
                GeneratingStatus = Some ""
            }

        Assert.False h.GeneratingChip.IsVisible
        Assert.False h.MoreButton.IsVisible
        h.Render { baseVS with ShowMore = true }
        Assert.True h.MoreButton.IsVisible
        let mutable more = 0
        let mutable crumb = 0
        h.MorePressed.Add(fun _ -> more <- more + 1)
        h.CrumbPressed.Add(fun () -> crumb <- crumb + 1)
        invoke h.MoreButton

        h.Render
            { baseVS with
                ShowMore = true
                Crumb =
                    Some
                        {
                            ParentId = Guid.NewGuid()
                            ParentTitle = "分叉自 A"
                            Clickable = false
                        }
            }

        invoke h.CrumbAction
        Assert.Equal(0, crumb)

        h.Render
            { baseVS with
                ShowMore = true
                Crumb =
                    Some
                        {
                            ParentId = Guid.NewGuid()
                            ParentTitle = "分叉自 A"
                            Clickable = true
                        }
            }

        invoke h.CrumbAction
        Assert.Equal("分叉自 A", h.CrumbText)

        h.Render
            { baseVS with
                ShowMore = true
                Crumb = None
            }

        Assert.Equal("", h.CrumbText)
        Assert.Equal((1, 1), (more, crumb))

        // 生成中且无面包屑：行只为生成指示而显，空面包屑键不许可见可命中
        // （曾以零尺寸空键盖到首条消息的操作条上，布局审计 C 报交叠）
        h.Render
            { baseVS with
                GeneratingStatus = Some ""
                Crumb = None
            }

        Assert.False h.CrumbAction.IsVisible
        Assert.False h.CrumbAction.Focusable
        Assert.Equal(1.0, h.GeneratingChip.Opacity)

        h.Render
            { baseVS with
                GeneratingStatus = Some ""
                Crumb =
                    Some
                        {
                            ParentId = Guid.NewGuid()
                            ParentTitle = "分叉自 A"
                            Clickable = true
                        }
            }

        Assert.True h.CrumbAction.IsVisible
        Assert.True h.CrumbAction.Focusable
        w.Close())

[<Fact>]
let ``单入口 Render(ChatHeaderVS) 幂等投影全部状态`` () =
    Headless.run (fun () ->
        let h = ChatHeader()
        let w = Window(Content = h.Host)
        w.Show()

        let vs: ChatHeaderVS =
            {
                Visible = true
                Compact = false
                InsetLeft = 16.0
                Title = TitleMode.Viewing("测试会话", true)
                GeneratingStatus = Some "生成中..."
                Crumb =
                    Some
                        {
                            ParentId = Guid.NewGuid()
                            ParentTitle = "根会话"
                            Clickable = true
                        }
                ShowBack = true
                ShowMore = true
            }

        h.Render vs
        Assert.True h.Host.IsVisible
        Assert.Equal("测试会话", h.Title)
        Assert.True h.MoreButton.IsVisible
        Assert.Equal(1.0, h.GeneratingChip.Opacity)
        Assert.Equal("根会话", h.CrumbText)
        w.Close())

[<Fact>]
let ``紧凑态命中区宽按 target 放大，非紧凑态为默认尺寸`` () =
    Headless.run (fun () ->
        let h = ChatHeader()
        let w = Window(Content = h.Host)
        w.Show()
        let desktopWidth = h.SidebarButton.Width

        let vsDesktop =
            {
                Visible = true
                Compact = false
                InsetLeft = 0.0
                Title = TitleMode.Viewing("测试", false)
                GeneratingStatus = None
                Crumb = None
                ShowBack = true
                ShowMore = true
            }

        h.Render vsDesktop
        Assert.Equal(30.0, h.SidebarButton.Width)

        let vsCompact =
            {
                Visible = true
                Compact = true
                InsetLeft = 0.0
                Title = TitleMode.Viewing("测试", false)
                GeneratingStatus = None
                Crumb = None
                ShowBack = true
                ShowMore = true
            }

        h.Render vsCompact
        Assert.Equal(44.0, h.SidebarButton.Width)
        Assert.Equal(44.0, h.MoreButton.Width)
        Assert.True(h.SidebarButton.Width > desktopWidth)
        Assert.Equal(44.0, h.SidebarButton.Width)
        Assert.Equal(44.0, h.MoreButton.Width)
        w.Close())
