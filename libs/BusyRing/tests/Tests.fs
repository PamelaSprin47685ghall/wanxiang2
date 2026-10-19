module Wanxiang2.BusyRing.Tests.Tests

open Avalonia.Controls
open Xunit
open Wanxiang2.BusyRing
open Wanxiang2.BusyRing.Tests

[<Fact>]
let ``BusyRing 具有 anim-spin 声明式动效类并随 Render(busy) 启停`` () =
    Headless.run (fun () ->
        let r = BusyRing(16.0)
        Assert.True r.IsBusy
        Assert.True (r.Host.Classes.Contains "anim-spin")
        let w = Window(Content = r.Host)
        w.Show()
        Assert.True r.IsSpinning
        r.Render false
        Assert.False r.IsBusy
        Assert.False r.IsSpinning
        Assert.False r.Host.IsVisible
        r.Render true
        Assert.True r.IsBusy
        Assert.True r.IsSpinning
        Assert.True r.Host.IsVisible
        w.Close())

[<Fact>]
let ``BusyRing 尺寸通过构造指定，结构包含 Viewbox 与 Canvas`` () =
    Headless.run (fun () ->
        let r = BusyRing(24.0)
        let vb = r.Host :?> Viewbox
        Assert.Equal(24.0, vb.Width)
        Assert.Equal(24.0, vb.Height)
        let canvas = vb.Child :?> Canvas
        Assert.Equal(16.0, canvas.Width)
        Assert.Equal(16.0, canvas.Height)
        Assert.Equal(2, canvas.Children.Count))
