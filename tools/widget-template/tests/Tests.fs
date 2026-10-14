module Wanxiang2.__NAME__.Tests.Tests

open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.__NAME__

let private look () =
    let bg = SolidColorBrush(Colors.White)
    let pad = Cell(Thickness 4.0)
    bg, pad, { Background = bg; Foreground = SolidColorBrush(Colors.Black); Padding = pad }

[<Fact>]
let ``bump raises changed and shows the count in a real window`` () =
    Headless.run (fun () ->
        let _, _, l = look ()
        let w = __NAME__ l
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
let ``look instances stay live after construction`` () =
    Headless.run (fun () ->
        let bg, pad, l = look ()
        let w = __NAME__ l
        bg.Color <- Colors.Red
        pad.Set(Thickness 9.0)
        Assert.Equal(Colors.Red, (w.Background :?> SolidColorBrush).Color)
        Assert.Equal(Thickness 9.0, w.Padding))
