module Wanxiang2.StreamCaret.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.StreamCaret
open Wanxiang2.StreamCaret.Tests

let private mk (reduced: Cell<bool>) =
    { Ink = Brushes.Blue
      Gauge = (function CaretGauge.OpacityMax -> Cell 0.95 :> IObservable<float> | CaretGauge.Width -> Cell 2.0 | _ -> Cell 16.0)
      Frame = Cell(TimeSpan.FromMilliseconds 50.0)
      Reduced = reduced }

let private op (c: Control) = c.GetBaseValue(Visual.OpacityProperty).Value

[<Fact>]
let ``挂上才按呼吸函数取不透明度并起表`` () =
    Headless.run (fun () ->
        let mutable v = 0.4
        let c = StreamCaret(mk (Cell false), fun () -> v)
        Assert.Equal(0.95, op c.Host)
        let w = Window(Content = c.Host)
        w.Show()
        Assert.True c.IsBreathing
        Assert.Equal(0.4, op c.Host)
        v <- 0.6
        c.Step()
        Assert.Equal(0.6, op c.Host)
        w.Content <- null
        Assert.False c.IsBreathing
        w.Close())

[<Fact>]
let ``减弱动效钉在上限且不起表`` () =
    Headless.run (fun () ->
        let reduced = Cell true
        let c = StreamCaret(mk reduced, fun () -> 0.4)
        let w = Window(Content = c.Host)
        w.Show()
        Assert.False c.IsBreathing
        Assert.Equal(0.95, op c.Host)
        reduced.Set false
        Assert.True c.IsBreathing
        Assert.Equal(0.4, op c.Host)
        reduced.Set true
        Assert.Equal(0.95, op c.Host)
        Assert.Equal(2.0, c.Host.Width)
        w.Close())
