module Wanxiang2.ActionStrip.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.ActionStrip
open Wanxiang2.ActionStrip.Tests

let private opacity (c: Control) =
    c.GetBaseValue(Visual.OpacityProperty).Value

[<Fact>]
let ``空行也占一个命中区高`` () =
    Headless.run (fun () ->
        let s = ActionStrip(StripRest.Hidden)
        Assert.Equal(30.0, s.Host.MinHeight))

[<Fact>]
let ``闲置档：隐去档闲置为 0，恒显档全程可见`` () =
    Headless.run (fun () ->
        let hidden = ActionStrip(StripRest.Hidden)
        let subtle = ActionStrip(StripRest.Subtle)
        Assert.Equal(0.0, opacity hidden.Host)
        // react 的 msg-actions 恒显：Subtle 档闲置也是全不透明，不随悬停淡出。
        Assert.Equal(1.0, opacity subtle.Host)
        subtle.ApplyShown true
        Assert.Equal(1.0, opacity subtle.Host)
        subtle.ApplyShown false
        Assert.Equal(1.0, opacity subtle.Host)
        hidden.ApplyShown true
        Assert.Equal(1.0, opacity hidden.Host)
        hidden.ApplyShown false
        Assert.Equal(0.0, opacity hidden.Host))

[<Fact>]
let ``按钮带字、高 30、有读屏名并执行动作`` () =
    Headless.run (fun () ->
        let s = ActionStrip(StripRest.Hidden)
        let mutable hits = 0

        let b =
            s.Add(StripGlyph.Refresh, "重新生成", "重新生成", "丢弃最后一次回复并重新生成", fun () -> hits <- hits + 1)

        Assert.Equal(30.0, b.Height)
        Assert.True(Double.IsNaN b.Width)
        let face = b.Content :?> StackPanel
        Assert.Equal("重新生成", (face.Children[1] :?> TextBlock).Text)
        Assert.Equal(box StripGlyph.Refresh, (face.Children[0]).Tag)
        Assert.Equal("丢弃最后一次回复并重新生成", AutomationProperties.GetHelpText b)
        Assert.Contains("ghost", b.Classes)
        Assert.DoesNotContain("icon-button", b.Classes)

        Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(
            Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement b
        )
        |> ignore

        b.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent, b))
        Assert.Equal(1, hits)
        s.ApplyIcon(b, StripGlyph.Check, Brushes.Green)
        Assert.Equal(box StripGlyph.Check, (face.Children[0]).Tag)
        s.ApplyCaption(b, "已复制")
        Assert.Equal("已复制", (face.Children[1] :?> TextBlock).Text))

[<Fact>]
let ``复制读屏名带角色与序号`` () =
    Assert.Equal("复制用户消息 3", ActionStrip.copyName true (Some 2))
    Assert.Equal("复制助手消息", ActionStrip.copyName false None)

[<Fact>]
let ``按钮高度是 30，宽度跟文案`` () =
    Headless.run (fun () ->
        let s = ActionStrip(StripRest.Hidden)
        let b = s.Add(StripGlyph.Trash, "删", "删", "删", ignore)
        Assert.Equal(30.0, b.Height)
        Assert.True(Double.IsNaN b.Width)
        Assert.Equal("删", ((b.Content :?> StackPanel).Children[1] :?> TextBlock).Text)
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
