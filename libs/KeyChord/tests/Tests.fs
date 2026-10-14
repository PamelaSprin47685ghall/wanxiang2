module Wanxiang2.KeyChord.Tests.Tests

open Avalonia.Input
open Xunit
open Wanxiang2.KeyChord

[<Fact>]
let ``⌘ 与 Ctrl 同义，Shift 单独记，无修饰键单独标出`` () =
    Assert.Equal({ key = "K"; ctrl = true; shift = false; noModifiers = false }, KeyChord.ofKey Key.K KeyModifiers.Meta)
    Assert.Equal({ key = "K"; ctrl = true; shift = true; noModifiers = false }, KeyChord.ofKey Key.K (KeyModifiers.Control ||| KeyModifiers.Shift))
    Assert.Equal({ key = "Escape"; ctrl = false; shift = false; noModifiers = true }, KeyChord.ofKey Key.Escape KeyModifiers.None)
    Assert.Equal({ key = "F2"; ctrl = false; shift = false; noModifiers = false }, KeyChord.ofKey Key.F2 KeyModifiers.Alt)

[<Fact>]
let ``真实按键事件换算相同`` () =
    Headless.run (fun () ->
        let e = KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Z, KeyModifiers = KeyModifiers.Control)
        Assert.Equal(KeyChord.ofKey Key.Z KeyModifiers.Control, KeyChord.ofEvent e))
