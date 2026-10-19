module Wanxiang2.BatchBar.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.BatchBar
open Wanxiang2.BatchBar.Tests.Probe
open Wanxiang2.WorkspaceState

let private state n =
    Some {
        SelectedCount = n
        AllSelected = (n = 3)
        Busy = false
        AllPinned = (n = 3)
    }

let private make () =
    let b = BatchBar()
    let panel = StackPanel()
    panel.Children.Add b.Header
    panel.Children.Add b.ActionBar
    let w = host panel
    b, w

let private press (w: Window) k =
    w.KeyPress(k, RawInputModifiers.None, PhysicalKey.None, null)
    w.KeyRelease(k, RawInputModifiers.None, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``selection action buttons disable themselves while nothing is selected`` () =
    Headless.run (fun () ->
        let b, _ = make ()
        b.Render(state 0)
        for c in [ b.PinButton; b.ArchiveButton; b.DeleteButton ] do
            Assert.False c.IsEnabled
            Assert.False c.Focusable
        b.Render(state 2)
        for c in [ b.PinButton; b.ArchiveButton; b.DeleteButton ] do Assert.True c.IsEnabled)

[<Fact>]
let ``selection buttons rename themselves as the selection changes`` () =
    Headless.run (fun () ->
        let b, _ = make ()
        b.Render(state 3)
        Assert.Equal("已选 3 项", b.CountText)
        Assert.Equal("取消全选", string (b.SelectAllButton :?> Button).Content)
        b.Render(Some { (state 1).Value with AllPinned = true })
        Assert.Equal("全选", string (b.SelectAllButton :?> Button).Content)
        Assert.Equal("取消置顶", string (b.PinButton :?> Button).Content))

[<Fact>]
let ``selection action bar arrows walk the batch buttons`` () =
    Headless.run (fun () ->
        let b, w = make ()
        b.Render(state 2)
        b.PinButton.Focus() |> ignore
        press w Key.Right
        Assert.True b.ArchiveButton.IsFocused
        press w Key.Right
        Assert.True b.DeleteButton.IsFocused
        press w Key.Left
        Assert.True b.ArchiveButton.IsFocused)

[<Fact>]
let ``every batch control raises its event through click`` () =
    Headless.run (fun () ->
        let b, _ = make ()
        b.Render(state 2)
        let log = ResizeArray<string>()
        b.ExitClicked.Add(fun () -> log.Add "exit")
        b.SelectAllClicked.Add(fun () -> log.Add "all")
        b.PinClicked.Add(fun () -> log.Add "pin")
        b.ArchiveClicked.Add(fun () -> log.Add "archive")
        b.DeleteClicked.Add(fun () -> log.Add "delete")
        click (named b.Header "退出多选（Esc）").Value
        for c in [ b.SelectAllButton; b.PinButton; b.ArchiveButton; b.DeleteButton ] do click c
        Assert.Equal<string list>([ "exit"; "all"; "pin"; "archive"; "delete" ], List.ofSeq log))

[<Fact>]
let ``render None hides both bars`` () =
    Headless.run (fun () ->
        let b, _ = make ()
        b.Render(state 2)
        Assert.True b.Header.IsVisible
        Assert.True b.ActionBar.IsVisible
        b.Render(None)
        Assert.False b.Header.IsVisible
        Assert.False b.ActionBar.IsVisible)
