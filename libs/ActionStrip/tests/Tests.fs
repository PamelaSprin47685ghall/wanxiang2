module Wanxiang2.ActionStrip.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.ActionStrip
open Wanxiang2.ActionStrip.Tests

let private target = Cell 30.0
let private look =
    { Gauge = (function StripGauge.Target -> target :> IObservable<float> | StripGauge.Subtle -> Cell 0.55 | StripGauge.FadeMs -> Cell 120.0 | _ -> Cell 2.0)
      Glyph = fun g b -> Border(Tag = g, Background = b)
      Muted = Brushes.Gray
      Dress = fun b -> b.Classes.Add "icon-button"
      Reduced = Cell true }

let private opacity (c: Control) = c.GetBaseValue(Visual.OpacityProperty).Value

[<Fact>]
let ``空行也占一个命中区高`` () =
    Headless.run (fun () ->
        let s = ActionStrip(look, StripRest.Hidden)
        Assert.Equal(30.0, s.Host.MinHeight))

[<Fact>]
let ``闲置档：隐去或淡显，显示时全不透明`` () =
    Headless.run (fun () ->
        let hidden = ActionStrip(look, StripRest.Hidden)
        let subtle = ActionStrip(look, StripRest.Subtle)
        Assert.Equal(0.0, opacity hidden.Host)
        Assert.Equal(0.55, opacity subtle.Host)
        hidden.SetShown true
        Assert.Equal(1.0, opacity hidden.Host)
        hidden.SetShown false
        Assert.Equal(0.0, opacity hidden.Host))

[<Fact>]
let ``按钮是方形命中区、带读屏名并执行动作`` () =
    Headless.run (fun () ->
        let s = ActionStrip(look, StripRest.Hidden)
        let mutable hits = 0
        let b = s.Add(StripGlyph.Refresh, "重新生成", "重新生成", "丢弃最后一次回复并重新生成", fun () -> hits <- hits + 1)
        Assert.Equal(30.0, b.Width)
        Assert.Equal("丢弃最后一次回复并重新生成", AutomationProperties.GetHelpText b)
        Assert.Contains("icon-button", b.Classes)
        Assert.IsAssignableFrom<Avalonia.Automation.Provider.IInvokeProvider>(Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement b) |> ignore
        b.RaiseEvent(Avalonia.Input.KeyEventArgs(RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter))
        Assert.Equal(1, hits)
        s.SetIcon(b, StripGlyph.Check, Brushes.Green)
        Assert.Equal(box StripGlyph.Check, b.Child.Tag))

[<Fact>]
let ``复制读屏名带角色与序号`` () =
    Assert.Equal("复制用户消息 3", ActionStrip.copyName true (Some 2))
    Assert.Equal("复制助手消息", ActionStrip.copyName false None)

[<Fact>]
let ``命中区边长是活的`` () =
    Headless.run (fun () ->
        let s = ActionStrip(look, StripRest.Hidden)
        let b = s.Add(StripGlyph.Trash, "删", "删", "删", ignore)
        let w = Window(Content = s.Host)
        w.Show()
        target.Set 44.0
        Assert.Equal(44.0, b.Height)
        Assert.Equal(44.0, s.Host.MinHeight)
        w.Content <- null
        Assert.Equal(0, target.Count)
        target.Set 30.0
        w.Close())
