module Wanxiang2.StreamCaret.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.StreamCaret
open Wanxiang2.StreamCaret.Tests

let private op (c: Control) =
    c.GetBaseValue(Visual.OpacityProperty).Value

[<Fact>]
let ``挂上才按呼吸函数取不透明度并起表`` () =
    Headless.run (fun () ->
        let mutable v = 0.4
        let c = StreamCaret(fun () -> v)
        Assert.True(c.Host.Classes.Contains "anim-breathe")
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
let ``隐藏时不呼吸`` () =
    Headless.run (fun () ->
        let c = StreamCaret(fun () -> 0.4)
        let w = Window(Content = c.Host)
        w.Show()
        Assert.True c.IsBreathing
        c.Render false
        Assert.False c.IsBreathing
        Assert.False c.Host.IsVisible
        w.Close())
