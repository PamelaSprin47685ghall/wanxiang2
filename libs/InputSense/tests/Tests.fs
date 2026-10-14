module Wanxiang2.InputSense.Tests.Tests

open Avalonia.Controls
open Avalonia.Input
open Xunit
open Wanxiang2.InputSense

[<Fact>]
let ``没有输入时按平台默认；翻转才通知`` () =
    let s = InputSense(true)
    Assert.True s.PrefersTouch
    let seen = ResizeArray<bool>()
    s.Changed.Add seen.Add
    s.Report false
    s.Report false
    s.Report true
    Assert.Equal<bool list>([ false; true ], List.ofSeq seen)
    Assert.True s.PrefersTouch
    s.Reset()
    Assert.True(InputSense(false).PrefersTouch |> not)

[<Fact>]
let ``根控件上的按键（含已处理）记为非触屏`` () =
    Headless.run (fun () ->
        let s = InputSense(true)
        let tb = TextBox()
        tb.KeyDown.Add(fun e -> e.Handled <- true)
        let w = Window(Content = tb)
        s.Track w
        w.Show()
        tb.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.A, Source = tb))
        Assert.False s.PrefersTouch
        w.Close())
