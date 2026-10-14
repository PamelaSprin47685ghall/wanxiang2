module Wanxiang2.RevealPane.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Xunit
open Wanxiang2.RevealPane
open Wanxiang2.RevealPane.Tests

let private gap = Cell 4.0
let private gutter = Cell 8.0
let private look =
    { Gauge = (function RevealGauge.Gap -> gap :> IObservable<float> | RevealGauge.Gutter -> gutter)
      Dress = fun b -> b.Classes.Add "ghost" }

let private show (c: Control) =
    let w = Window(Width = 400.0, Height = 800.0, Content = c)
    w.Show()
    w.UpdateLayout()
    Avalonia.Threading.Dispatcher.UIThread.RunJobs()
    w.UpdateLayout()
    w

let private tall () = Border(Height = 900.0, Width = 100.0)

[<Fact>]
let ``短内容不出展开按钮`` () =
    Headless.run (fun () ->
        let p = RevealPane(look, Border(Height = 50.0), 360.0, true)
        let w = show p.Host
        Assert.False p.Toggle.IsVisible
        w.Close())

[<Fact>]
let ``超高内容出按钮，展开后不限高，再按收起`` () =
    Headless.run (fun () ->
        let p = RevealPane(look, tall (), 360.0, true)
        let w = show p.Host
        Assert.True p.Toggle.IsVisible
        Assert.Equal(360.0, p.Scroller.MaxHeight)
        p.ToggleFull()
        Assert.True p.IsFull
        Assert.True(Double.IsPositiveInfinity p.Scroller.MaxHeight)
        Assert.Equal("收起", Automation.AutomationProperties.GetName p.Toggle)
        p.ToggleFull()
        Assert.Equal("展开全部", Automation.AutomationProperties.GetName p.Toggle)
        w.Close())

[<Fact>]
let ``隐藏时退出全文态`` () =
    Headless.run (fun () ->
        let p = RevealPane(look, tall (), 360.0, true)
        let w = show p.Host
        p.ToggleFull()
        p.SetVisible false
        Assert.False p.IsFull
        Assert.False p.Host.IsVisible
        Assert.Equal(360.0, p.Scroller.MaxHeight)
        w.Close())

[<Fact>]
let ``起始隐藏时首次布局也不亮按钮`` () =
    Headless.run (fun () ->
        let p = RevealPane(look, tall (), 360.0, false)
        let w = show p.Host
        Assert.False p.Toggle.IsVisible
        p.SetVisible true
        w.UpdateLayout()
        Avalonia.Threading.Dispatcher.UIThread.RunJobs()
        Assert.True p.Toggle.IsVisible
        w.Close())

[<Fact>]
let ``度量是活的且摘下后退订`` () =
    Headless.run (fun () ->
        let p = RevealPane(look, tall (), 360.0, true)
        let w = show p.Host
        gutter.Set 12.0
        Assert.Equal(12.0, p.Scroller.Margin.Right)
        Assert.Contains("ghost", p.Toggle.Classes)
        w.Content <- null
        Assert.Equal(0, gutter.Count)
        gutter.Set 8.0
        w.Close())
