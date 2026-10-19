module Wanxiang2.ConfirmPane.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ConfirmPane
open Wanxiang2.ConfirmPane.Tests.Probe

let private press (w: Window) k mods =
    w.KeyPress(k, mods, PhysicalKey.None, null)
    w.KeyRelease(k, mods, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``Dialogs confirm focuses the safe cancel action by default`` () =
    Headless.run (fun () ->
        let p = ConfirmPane("删除对话", "删除后无法恢复。", "删除", false)
        let _ = host p.View
        Assert.Same(p.CancelButton, p.InitialFocus)
        let q = ConfirmPane("删除对话", "删除后无法恢复。", "删除", true)
        Assert.Same(q.ConfirmButton, q.InitialFocus))

[<Fact>]
let ``confirm and confirmWithFocus dialog trigger callbacks correctly`` () =
    Headless.run (fun () ->
        let p = ConfirmPane("删除对话", "删除后无法恢复。", "删除", false)
        let w = host p.View
        let log = ResizeArray<string>()
        p.Confirmed.Add(fun () -> log.Add "ok")
        p.Cancelled.Add(fun () -> log.Add "cancel")
        Assert.Contains("删除后无法恢复。", AutomationProperties.GetHelpText p.ConfirmButton)
        Assert.Contains("此操作可能无法撤销", ToolTip.GetTip p.ConfirmButton :?> string)
        // Danger 没有行级 Enter：焦点在取消上按 Enter 只取消
        p.CancelButton.Focus() |> ignore
        press w Key.Enter RawInputModifiers.None
        press w Key.Right RawInputModifiers.None
        Assert.True p.ConfirmButton.IsFocused
        press w Key.Enter RawInputModifiers.None
        click p.ConfirmButton
        Assert.Equal<string list>([ "cancel"; "ok"; "ok" ], List.ofSeq log))
