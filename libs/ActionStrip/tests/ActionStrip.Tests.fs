module Wanxiang2.ActionStrip.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.ActionStrip
open Wanxiang2.ActionStrip.Tests

let private opacity (c: Control) = c.GetBaseValue(Visual.OpacityProperty).Value

[<Fact>]
let ``空行也占一个命中区高`` () =
    Headless.run (fun () ->
        let s = ActionStrip(StripRest.Hidden)
        Assert.Equal(30.0, s.Host.MinHeight))

[<Fact>]
let ``闲置档：隐去或淡显，显示时全不透明`` () =
    Headless.run (fun () ->
        let hidden = ActionStrip(StripRest.Hidden)
        let subtle = ActionStrip(StripRest.Subtle)
        Assert.Equal(0.0, opacity hidden.Host)
        Assert.Equal(0.55, opacity subtle.Host)
        hidden.SetShown true
        Assert.Equal(1.0, opacity hidden.Host)
        hidden.SetShown false
        Assert.Equal(0.0, opacity hidden.Host))

[<Fact>]
let ``按钮是方形命中区、带读屏名并执行动作`` () =
    Headless.run (fun () ->
        let s = ActionStrip(StripRest.Hidden)
        let mutable hits = 0
        let b = s.Add(StripGlyph.Refresh, "重新生成", "重新生成", "丢弃最后一次回复并重新生成", fun () -> hits <- hits + 1)
        Assert.Equal(30.0, b.Width)
        Assert.Equal("丢弃最后一次回复并重新生成", AutomationProperties.GetHelpText b)
        Assert.Contains("icon-button", b.Classes)
        Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement b) |> ignore
        b.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, b))
        Assert.Equal(1, hits)
        s.SetIcon(b, StripGlyph.Check, Brushes.Green)
        Assert.Equal(box StripGlyph.Check, (b.Content :?> Control).Tag))

[<Fact>]
let ``复制读屏名带角色与序号`` () =
    Assert.Equal("复制用户消息 3", ActionStrip.copyName true (Some 2))
    Assert.Equal("复制助手消息", ActionStrip.copyName false None)

[<Fact>]
let ``按钮尺寸恒定为 30x30`` () =
    Headless.run (fun () ->
        let s = ActionStrip(StripRest.Hidden)
        let b = s.Add(StripGlyph.Trash, "删", "删", "删", ignore)
        Assert.Equal(30.0, b.Height)
        Assert.Equal(30.0, s.Host.MinHeight))

let private invokeVia (c: Avalonia.Controls.Control) =
    let peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement c
    Assert.Equal(Avalonia.Automation.Peers.AutomationControlType.Button, peer.GetAutomationControlType())
    let p = Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(peer)
    p.Invoke()
    Avalonia.Threading.Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``操作按钮有读屏 Invoke 且执行动作`` () =
    Headless.run (fun () ->
        let s = ActionStrip(StripRest.Hidden)
        let mutable hits = 0
        let b = s.Add(StripGlyph.Copy, "复制", "复制", "复制", fun () -> hits <- hits + 1)
        invokeVia b
        Assert.Equal(1, hits))
