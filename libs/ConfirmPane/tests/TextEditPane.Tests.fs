module Wanxiang2.TextEditPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.TextEditPane
open Wanxiang2.TextEditPane.Tests.Probe
open Wanxiang2.TextEditPane.Tests.Fixture

let private press (w: Window) k mods =
    w.KeyPress(k, mods, PhysicalKey.None, null)
    w.KeyRelease(k, mods, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``editText dialog loads text and submits on confirm`` () =
    Headless.run (fun () ->
        let look, _ = make ()
        let p = TextEditPane(look, "编辑消息", "原文\n第二行", "分叉")
        let w = host p.View
        let sent = ResizeArray<string>()
        p.Submitted.Add sent.Add
        p.FocusEnd()
        Assert.Equal(p.Box.Text.Length, p.Box.CaretIndex)
        Assert.Contains("Ctrl / ⌘ + Enter", ToolTip.GetTip p.ConfirmButton :?> string)
        press w Key.Enter RawInputModifiers.Control
        click p.ConfirmButton
        Assert.Equal<string list>([ "原文\n第二行"; "原文\n第二行" ], List.ofSeq sent))
