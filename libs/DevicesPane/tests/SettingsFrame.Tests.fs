module Wanxiang2.SettingsFrame.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.SettingsFrame
open Wanxiang2.SettingsFrame.Tests.Probe

let private item key label = { Key = key; Label = label; Glyph = key }
let private groups =
    [ "主机", [ item "providers" "模型服务"; item "tools" "工具"; item "generation" "对话默认值"; item "devices" "设备" ]
      "这台客户端", [ item "appearance" "外观与操作"; item "about" "关于" ] ]

/// 组合者替身：请求即选中
let private frame () =
    let f = SettingsFrame(groups)
    f.SelectRequested.Add f.Select
    let w = Window(Content = f, Width = 1000.0, Height = 700.0)
    w.Show()
    Dispatcher.UIThread.RunJobs()
    f, w

let private key (c: Control) k =
    c.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = k))

[<Fact>]
let ``SettingsView navigates sections, remembers scroll positions and invalidates cache`` () =
    Headless.run (fun () ->
        let f, _ = frame ()
        let tools = (named f "工具").Value
        click tools
        Assert.Equal("tools", f.Current)
        Assert.Equal("当前分区", AutomationProperties.GetItemStatus tools)
        Assert.Equal("工具，当前分区", AutomationProperties.GetHelpText tools)
        let providers = (named f "模型服务").Value
        Assert.Equal("", AutomationProperties.GetItemStatus providers)
        let content = StackPanel(Height = 3000.0)
        f.ApplyContent(content, Some 200.0)
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(200.0, f.ContentOffset))

[<Fact>]
let ``settings nav arrows wrap and home end jump while keeping focus on the nav`` () =
    Headless.run (fun () ->
        let f, _ = frame ()
        f.Select "providers"
        f.FocusNav "providers"
        let first = (named f "模型服务").Value
        Assert.True first.IsFocused
        key first Key.Up
        Assert.Equal("about", f.Current)
        Assert.True (named f "关于").Value.IsFocused
        key (named f "关于").Value Key.Right
        Assert.Equal("providers", f.Current)
        key first Key.End
        Assert.Equal("about", f.Current)
        key (named f "关于").Value Key.Home
        Assert.Equal("providers", f.Current)
        key first Key.Down
        Assert.Equal("tools", f.Current)
        Assert.Equal(AutomationControlType.TabItem, (ControlAutomationPeer.CreatePeerForElement first).GetAutomationControlType()))

[<Fact>]
let ``settings window title dominates section headings`` () =
    Headless.run (fun () ->
        let f, _ = frame ()
        let title = descendants f |> Seq.pick (fun c -> match c with :? TextBlock as t when t.Text = "设置" -> Some t | _ -> None)
        Assert.Equal(28.0, title.FontSize)
        Assert.True(title.FontSize > 17.0)
        let closes = ResizeArray<unit>()
        f.CloseClicked.Add closes.Add
        click (named f "返回对话").Value
        Assert.Single closes |> ignore)

[<Fact>]
let ``narrow window turns the nav into a top strip and hides group labels`` () =
    Headless.run (fun () ->
        let f, w = frame ()
        Assert.False f.IsCompact
        let label = descendants f |> Seq.pick (fun c -> match c with :? TextBlock as t when t.Text = "这台客户端" -> Some t | _ -> None)
        Assert.True label.IsVisible
        w.Width <- 600.0
        Dispatcher.UIThread.RunJobs()
        Assert.True f.IsCompact
        Assert.False label.IsVisible
        let nav = (named f "设置分区导航").Value
        Assert.Equal(Dock.Top, DockPanel.GetDock nav)
        w.Width <- 1000.0
        Dispatcher.UIThread.RunJobs()
        Assert.False f.IsCompact)

[<Fact>]
let ``scope captions follow the composer`` () =
    Headless.run (fun () ->
        let f, _ = frame ()
        f.ApplyCaptions("这台客户端", "仅在这台客户端即时生效，不修改主机配置。")
        Assert.Contains("仅在这台客户端即时生效", textOf f))
