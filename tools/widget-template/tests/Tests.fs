module Wanxiang2.__NAME__.Tests.Tests

open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.__NAME__

[<Fact>]
let ``bump raises changed and shows the count in a real window`` () =
    Headless.run (fun () ->
        let w = __NAME__ ()
        w.Render { Visible = true; Text = "" }
        let win = Window(Content = w)
        win.Show()
        let mutable seen = 0
        w.Changed.Add(fun n -> seen <- n)
        w.Bump()
        w.Bump()
        Assert.Equal(2, seen)
        Assert.Equal("2", (w.Child :?> TextBlock).Text)
        win.Close())

[<Fact>]
let ``render is idempotent and drives visibility from the view state`` () =
    Headless.run (fun () ->
        let w = __NAME__ ()
        let vs = { Visible = true; Text = "hi" }
        w.Render vs
        w.Render vs
        Assert.True w.IsVisible
        Assert.Equal("hi", (w.Child :?> TextBlock).Text)
        w.Render { Visible = false; Text = "hi" }
        Assert.False w.IsVisible)
