module Wanxiang2.ChatEmpty.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Automation.Provider
open Avalonia.Controls
open Avalonia.LogicalTree
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ChatEmpty
open Wanxiang2.ChatEmpty.Tests
open Wanxiang2.WorkspaceState

let rec private all (c: Control) : Control list =
    c :: [ for x in c.GetLogicalChildren() do
             match x with
             | :? Control as cc -> yield! all cc
             | _ -> () ]

let private named (root: Control) name = all root |> List.find (fun c -> AutomationProperties.GetName c = name)

let private invoke (c: Control) =
    let peer = ControlAutomationPeer.CreatePeerForElement c
    Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType())
    ((peer :> obj) :?> IInvokeProvider).Invoke()
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``对话起点：主按钮与起手式都可经读屏 Invoke 触发`` () =
    Headless.run (fun () ->
        let log = ResizeArray<string>()
        let e = ChatEmpty([ "解释概念", "用通俗的话解释：" ], Some(fun seed -> log.Add("seed:" + seed)))
        e.ShowPrompt("今天想讨论什么？", "发出第一条消息后，会自动建立对话。", Some("新对话", fun () -> log.Add "primary"))
        let w = Window(Width = 600.0, Height = 600.0, Content = e.Host)
        w.Show()
        Assert.True e.IsShown
        invoke (named e.Host "新对话")
        let starter = named e.Host "解释概念"
        Assert.Equal(box "填入「用通俗的话解释：」", ToolTip.GetTip starter)
        Assert.True(String.IsNullOrEmpty(AutomationProperties.GetHelpText starter))
        invoke starter
        Assert.Equal<string list>([ "primary"; "seed:用通俗的话解释：" ], List.ofSeq log)
        w.Close())

[<Fact>]
let ``同一空态重复显示不重建；换动作仍生效`` () =
    Headless.run (fun () ->
        let e = ChatEmpty([], None)
        let mutable hit = ""
        e.ShowPrompt("t", "h", Some("开始", fun () -> hit <- "a"))
        let first = (e.Host :?> StackPanel).Children[0]
        e.ShowPrompt("t", "h", Some("开始", fun () -> hit <- "b"))
        Assert.Same(first, (e.Host :?> StackPanel).Children[0])
        let w = Window(Content = e.Host)
        w.Show()
        invoke (named e.Host "开始")
        Assert.Equal("b", hit)
        w.Close())

[<Fact>]
let ``Render 单入口：根据 ChatPhase 渲染对应空态`` () =
    Headless.run (fun () ->
        let e = ChatEmpty([], None)
        e.Render(ChatPhase.Empty IdleEmptyKind.NotConnected, ("连接", ignore))
        Assert.True e.IsShown
        e.Render(ChatPhase.Empty IdleEmptyKind.NoConversation)
        Assert.True e.IsShown
        e.Render(ChatPhase.Skeleton)
        Assert.False e.IsShown)
[<Fact>]
let ``没有起手式回调时不画起手式行；品牌空态包在分组卡里`` () =
    Headless.run (fun () ->
        let e = ChatEmpty([ "写代码", "写一段代码，实现：" ], None)
        e.ShowPrompt("t", "h", None)
        Assert.Equal(1, (e.Host :?> StackPanel).Children.Count)
        e.ShowBrand(Border(Width = 68.0, Height = 68.0), "先连接一台万象主机", "提示", Some("连接主机", ignore))
        let card = (e.Host :?> StackPanel).Children[0] :?> Border
        Assert.Contains("grouping-card", card.Classes)
        Assert.NotNull(named e.Host "连接主机")
        e.Hide()
        Assert.False e.IsShown)

[<Fact>]
let ``内嵌内容原样挂上，再挂同一块不重建；贴底改对齐`` () =
    Headless.run (fun () ->
        let e = ChatEmpty([], None)
        let content = Border()
        e.ShowEmbedded content
        e.ShowEmbedded content
        Assert.Same(content, (e.Host :?> StackPanel).Children[0])
        Assert.Equal(Layout.HorizontalAlignment.Stretch, e.Host.HorizontalAlignment)
        e.ApplyBottomAligned true
        Assert.Equal(Layout.VerticalAlignment.Bottom, e.Host.VerticalAlignment))
