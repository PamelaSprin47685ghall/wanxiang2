module Wanxiang2.BusyRing.Tests.Tests

open System
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.BusyRing
open Wanxiang2.BusyRing.Tests

let private mk (reduced: Cell<bool>) =
    { Ink = (function BusyInk.Track -> Brushes.Gray :> IBrush | BusyInk.Arc -> Brushes.Blue)
      StrokeWidth = Cell 1.8
      Frame = Cell(TimeSpan.FromMilliseconds 50.0)
      Reduced = reduced }

[<Fact>]
let ``挂上才转，摘下停`` () =
    Headless.run (fun () ->
        let r = BusyRing(mk (Cell false), 16.0)
        Assert.False r.IsSpinning
        let w = Window(Content = r.Host)
        w.Show()
        Assert.True r.IsSpinning
        r.Step()
        Assert.Equal(18.0, r.Angle)
        w.Content <- null
        Assert.False r.IsSpinning
        w.Close())

[<Fact>]
let ``减弱动效停在零度并露出底轨，活值切换即时生效`` () =
    Headless.run (fun () ->
        let reduced = Cell false
        let r = BusyRing(mk reduced, 12.0)
        let w = Window(Content = r.Host)
        w.Show()
        r.Step()
        reduced.Set true
        Assert.False r.IsSpinning
        Assert.Equal(0.0, r.Angle)
        let canvas = (r.Host :?> Viewbox).Child :?> Canvas
        Assert.True canvas.Children[0].IsVisible
        reduced.Set false
        Assert.True r.IsSpinning
        Assert.False canvas.Children[0].IsVisible
        w.Close())
