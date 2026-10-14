module Wanxiang2.CopyButton.Tests.Tests

open System
open Avalonia.Controls
open Xunit
open Wanxiang2.CopyButton

let private setup () =
    let mutable now = DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero)
    let opacity = Cell 0.6
    let look = { FadeOpacity = opacity; Hold = Cell(TimeSpan.FromMilliseconds 1500.0); Fade = Cell(TimeSpan.FromMilliseconds 150.0) }
    let b = Button()
    let c = CopyButton(b, look, (fun () -> now))
    b, c, opacity, (fun ms -> now <- now.AddMilliseconds ms)

[<Fact>]
let ``复制后先淡出再保持勾号，到点复位`` () =
    Headless.run (fun () ->
        let b, c, _, advance = setup ()
        let seen = ResizeArray<CopyState>()
        c.StateChanged.Add seen.Add
        c.Copied false
        Assert.Equal(CopyState.Fading, c.State)
        Assert.Equal(0.6, b.Opacity)
        advance 150.0
        c.Refresh()
        Assert.Equal(CopyState.Confirmed, c.State)
        Assert.Equal(1.0, b.Opacity)
        advance 1350.0
        c.Refresh()
        Assert.Equal(CopyState.Default, c.State)
        Assert.Equal<CopyState list>([ CopyState.Fading; CopyState.Confirmed; CopyState.Default ], List.ofSeq seen))

[<Fact>]
let ``减弱动效不淡出；再次复制重新起算`` () =
    Headless.run (fun () ->
        let b, c, _, advance = setup ()
        c.Copied true
        Assert.Equal(CopyState.Confirmed, c.State)
        Assert.Equal(1.0, b.Opacity)
        advance 1000.0
        c.Copied true
        advance 1000.0
        c.Refresh()
        Assert.Equal(CopyState.Confirmed, c.State))

[<Fact>]
let ``离开可视树立即复位；淡出值随主题实时变`` () =
    Headless.run (fun () ->
        let b, c, opacity, _ = setup ()
        let w = Window(Content = b)
        w.Show()
        c.Copied false
        opacity.Set 0.4
        Assert.Equal(0.4, b.Opacity)
        w.Content <- null
        Assert.Equal(CopyState.Default, c.State)
        Assert.Equal(1.0, b.Opacity)
        w.Close())
