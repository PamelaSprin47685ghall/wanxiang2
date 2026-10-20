module Wanxiang2.SidebarSearch.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.SidebarSearch
open Wanxiang2.WorkspaceState

open Wanxiang2.SidebarSearch.Tests.Probe

let private make () =
    let s = Wanxiang2.SidebarSearch.SidebarSearch()
    let panel = StackPanel()
    panel.Children.Add s.View
    panel.Children.Add s.EmptyHint
    let w = host panel
    s, w

let private press (w: Window) k =
    w.KeyPress(k, RawInputModifiers.None, PhysicalKey.None, null)
    w.KeyRelease(k, RawInputModifiers.None, PhysicalKey.None, null)
    Dispatcher.UIThread.RunJobs()

[<Fact>]
let ``Sidebar search clear button has accessible tooltip and automation properties`` () =
    Headless.run (fun () ->
        let s, _ = make ()
        let b = (named s.View "清空搜索").Value
        Assert.Equal("清空搜索", ToolTip.GetTip(b) :?> string)
        Assert.Equal(0.0, b.Opacity)
        Assert.False b.Focusable
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView b)
        s.Text <- "abc"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(1.0, b.Opacity)
        Assert.True b.Focusable
        let cleared = ref 0
        s.ClearRequested.Add(fun () -> cleared.Value <- cleared.Value + 1)
        click b
        Assert.Equal(1, cleared.Value))

[<Fact>]
let ``Sidebar search navigation moves focus and handles escape`` () =
    Headless.run (fun () ->
        let s, w = make ()
        let seen = ResizeArray<Key>()
        s.Keys.Add(fun a ->
            seen.Add a.Key
            if a.Key = Key.Down then a.Handled <- true)
        s.Focus true
        press w Key.Down
        press w Key.Enter
        press w Key.Escape
        Assert.Equal<Key list>([ Key.Down; Key.Enter; Key.Escape ], List.ofSeq seen))

[<Fact>]
let ``search input reports changes and the empty hint clears on click, enter and escape`` () =
    Headless.run (fun () ->
        let s, w = make ()
        let fired = ResizeArray<string>()
        s.Changed.Add fired.Add
        s.Text <- "a"
        Dispatcher.UIThread.RunJobs()
        s.Text <- "ab"
        Dispatcher.UIThread.RunJobs()
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<string list>([ "a"; "ab" ], List.ofSeq fired)
        let cleared = ref 0
        s.ClearRequested.Add(fun () -> cleared.Value <- cleared.Value + 1)
        s.ApplyEmptyHint true
        w.UpdateLayout()
        click s.EmptyHint
        s.FocusEmptyHint()
        press w Key.Enter
        press w Key.Escape
        Assert.Equal(3, cleared.Value)
        Assert.Equal("未找到匹配对话，点击清空搜索", AutomationProperties.GetName s.EmptyHint))

[<Fact>]
let ``render SearchBoxVS updates text and empty hint`` () =
    Headless.run (fun () ->
        let s, _ = make ()
        s.Render {
            Text = "query"
            EmptyHintVisible = true
            Active = true
            Debouncing = false
        }
        Assert.Equal("query", s.Text)
        Assert.True s.EmptyHint.IsVisible
        Assert.Equal(1.0, s.ClearButton.Opacity))
