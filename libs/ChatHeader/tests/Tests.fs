module Wanxiang2.ChatHeader.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ChatHeader
open Wanxiang2.ChatHeader.Tests

let private look () =
    { Ink = (fun _ -> SolidColorBrush(Colors.Gray) :> IBrush)
      Gauge = (fun _ -> Cell 8.0 :> _)
      BodyFont = Cell FontFamily.Default
      Glyph = (fun _ _ -> Border(Width = 10.0, Height = 10.0) :> Control)
      DressIcon = ignore
      DressPlain = ignore
      DressField = ignore }

let private invoke (b: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement b
    ((peer :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

let private key (c: Control) k =
    c.RaiseEvent(KeyEventArgs(Key = k, RoutedEvent = InputElement.KeyDownEvent))

[<Fact>]
let ``标题改名：Enter 提交去空白，Esc 取消，未变不报；不可编辑时不进编辑`` () =
    Headless.run (fun () ->
        let h = ChatHeader(look ())
        let log = ResizeArray<string>()
        h.Renamed.Add log.Add
        let w = Window(Content = h.Host)
        w.Show()
        h.SetConversationChrome true
        h.SetTitle("旧标题", false)
        invoke h.TitleAction
        Assert.False h.IsEditing
        h.SetTitle("旧标题", true)
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
        let h = ChatHeader(look ())
        let log = ResizeArray<string>()
        h.Renamed.Add log.Add
        let w = Window(Content = h.Host)
        w.Show()
        h.SetTitle("新对话", true)
        h.BeginRename()
        h.TitleEditor.Text <- "我的草稿"
        h.SetTitle("自动标题", true)
        Assert.True h.IsEditing
        Assert.Equal("我的草稿", h.TitleEditor.Text)
        h.CommitRename false
        Assert.Equal<string list>([ "我的草稿" ], List.ofSeq log)
        w.Close())

[<Fact>]
let ``生成中状态点桌面保留槽位、窄屏收起；更多按钮随对话；面包屑可点才报`` () =
    Headless.run (fun () ->
        let h = ChatHeader(look ())
        let w = Window(Content = h.Host)
        w.Show()
        Assert.True h.GeneratingChip.IsVisible
        Assert.Equal(0.0, h.GeneratingChip.Opacity)
        h.SetGenerating(true, "")
        Assert.Equal(1.0, h.GeneratingChip.Opacity)
        h.SetCompact true
        Assert.False h.GeneratingChip.IsVisible
        Assert.False h.MoreButton.IsVisible
        h.SetConversationChrome true
        Assert.True h.MoreButton.IsVisible
        let mutable more = 0
        let mutable crumb = 0
        h.MorePressed.Add(fun _ -> more <- more + 1)
        h.CrumbPressed.Add(fun () -> crumb <- crumb + 1)
        invoke h.MoreButton
        h.SetCrumb(Some { Text = "分叉自 A"; Description = "父对话已删除"; Clickable = false })
        invoke h.CrumbAction
        Assert.Equal(0, crumb)
        h.SetCrumb(Some { Text = "分叉自 A"; Description = "回到 A"; Clickable = true })
        invoke h.CrumbAction
        Assert.Equal("分叉自 A", h.CrumbText)
        h.SetCrumb None
        Assert.Equal("", h.CrumbText)
        Assert.Equal((1, 1), (more, crumb))
        w.Close())
