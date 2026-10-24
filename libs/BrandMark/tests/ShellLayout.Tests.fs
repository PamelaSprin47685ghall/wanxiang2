module Wanxiang2.ShellLayout.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Avalonia.VisualTree
open Xunit
open Wanxiang2.ShellLayout

let sidebarMin, sidebarMax, sidebarDefault, splitterW = 232.0, 420.0, 280.0, 6.0

let fixture () =
    let sidebar = StackPanel()
    let list = ListBox(Focusable = true)
    sidebar.Children.Add list
    let input = TextBox(AcceptsReturn = true)
    let composer = Border(Child = input, Height = 80.0)
    let chat = Border()
    let chatColumn = DockPanel()
    DockPanel.SetDock(composer, Dock.Bottom)
    chatColumn.Children.Add composer
    chatColumn.Children.Add chat
    let splitter = GridSplitter()
    let shell = Grid()
    shell.ColumnDefinitions.Add(ColumnDefinition(MaxWidth = sidebarMax))
    shell.ColumnDefinitions.Add(ColumnDefinition())
    shell.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
    shell.Children.Add sidebar
    shell.Children.Add splitter
    shell.Children.Add chatColumn
    shell, sidebar, list, splitter, chatColumn, composer, input

let wide =
    {
        Compact = false
        DrawerOpen = false
        SidebarCollapsed = false
    }

let compact =
    {
        Compact = true
        DrawerOpen = true
        SidebarCollapsed = false
    }

// 迁自旧 UiStabilityTests（MainLayoutController 的投影用例）。
[<Fact>]
let ``main layout controller projects navigation state onto the shell grid`` () =
    Headless.run (fun () ->
        let shell, sidebar, _, splitter, chatColumn, _, _ = fixture ()
        let window = Window(Width = 1000.0, Height = 640.0, Content = shell)
        window.Show()
        let savedWidth = sidebarDefault

        let controller =
            ShellColumns(shell, sidebar, splitter, chatColumn, sidebarMax, splitterW, fun () -> savedWidth)

        let compactSeen = ResizeArray<bool>()
        controller.CompactApplied.Add compactSeen.Add
        let collapsed = { wide with SidebarCollapsed = true }
        controller.Apply wide
        Assert.Equal(GridLength sidebarDefault, shell.ColumnDefinitions[0].Width)
        Assert.Equal(GridLength splitterW, shell.ColumnDefinitions[1].Width)
        Assert.Equal(GridLength.Star, shell.ColumnDefinitions[2].Width)
        Assert.True splitter.IsVisible
        Assert.True sidebar.IsVisible
        Assert.Equal(0, Grid.GetColumn sidebar)
        Assert.Equal(1, Grid.GetColumn splitter)
        Assert.Equal(2, Grid.GetColumn chatColumn)
        controller.Apply collapsed
        Assert.Equal(GridLength 0.0, shell.ColumnDefinitions[0].Width)
        Assert.Equal(GridLength 0.0, shell.ColumnDefinitions[1].Width)
        Assert.False splitter.IsVisible
        Assert.False sidebar.IsVisible
        controller.Apply compact
        Assert.Equal(GridLength.Star, shell.ColumnDefinitions[0].Width)
        Assert.Equal(GridLength 0.0, shell.ColumnDefinitions[1].Width)
        Assert.Equal(GridLength 0.0, shell.ColumnDefinitions[2].Width)
        Assert.Equal(0, Grid.GetColumn chatColumn)
        Assert.Equal(2, sidebar.ZIndex)
        Assert.False splitter.IsVisible
        let noted = controller.NoteUserSidebarWidth 9999.0
        Assert.Equal(sidebarMax, noted)
        Assert.Equal(GridLength sidebarMax, shell.ColumnDefinitions[0].Width)
        Assert.Equal(sidebarMin, controller.NoteUserSidebarWidth 1.0)
        controller.Apply wide
        Assert.Equal(GridLength savedWidth, shell.ColumnDefinitions[0].Width)
        Assert.True splitter.IsVisible
        controller.Apply compact
        Assert.False splitter.IsVisible
        controller.Apply wide
        Assert.True splitter.IsVisible
        Assert.Equal<bool list>([ false; false; true; false; true; false ], List.ofSeq compactSeen)
        window.Close())

// 迁自旧 UiStabilityTests（侧栏折叠时焦点迁到输入区）。
[<Fact>]
let ``collapsing the sidebar moves focus from the sidebar to the composer`` () =
    Headless.run (fun () ->
        let shell, sidebar, list, splitter, chatColumn, composer, input = fixture ()
        let window = Window(Width = 1000.0, Height = 640.0, Content = shell)
        window.Show()

        let controller =
            ShellColumns(shell, sidebar, splitter, chatColumn, sidebarMax, splitterW, fun () -> sidebarDefault)

        controller.FocusHomeRequested.Add(fun () -> input.Focus() |> ignore)
        controller.Apply wide
        list.Focus() |> ignore
        Dispatcher.UIThread.RunJobs()
        Assert.True(list.IsFocused, "precondition: focus in sidebar")
        controller.Apply wide
        Dispatcher.UIThread.RunJobs()
        Assert.True(list.IsFocused, "对照组：不折叠时焦点必须留在侧栏")
        controller.Apply { wide with SidebarCollapsed = true }
        Dispatcher.UIThread.RunJobs()

        match window.FocusManager.GetFocusedElement() with
        | :? Control as focused ->
            Assert.True(
                (composer :> Visual).IsVisualAncestorOf focused
                || obj.ReferenceEquals(focused, input),
                "折叠后焦点必须落在输入区内"
            )
        | _ -> Assert.Fail "折叠后焦点不得为 null"

        window.Close())

[<Fact>]
let ``compact drawer scrim follows drawer state and dismisses on press`` () =
    Headless.run (fun () ->
        let shell, sidebar, _, splitter, chatColumn, _, _ = fixture ()
        let window = Window(Width = 600.0, Height = 640.0, Content = shell)
        window.Show()

        let controller =
            ShellColumns(shell, sidebar, splitter, chatColumn, sidebarMax, splitterW, fun () -> sidebarDefault)

        let mutable dismissed = 0
        controller.ScrimPressed.Add(fun () -> dismissed <- dismissed + 1)
        controller.Apply compact
        Assert.True controller.Scrim.IsVisible
        Assert.Equal(1, controller.Scrim.ZIndex)
        Assert.True(controller.Scrim.Classes.Contains "scrim")
        let pointer = new Pointer(1, PointerType.Mouse, true)

        let args =
            PointerPressedEventArgs(
                controller.Scrim,
                pointer,
                shell,
                Point(10.0, 10.0),
                0UL,
                PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None
            )

        controller.Scrim.RaiseEvent args
        Assert.Equal(1, dismissed)
        Assert.True args.Handled
        controller.Apply { compact with DrawerOpen = false }
        Assert.False controller.Scrim.IsVisible
        Assert.False sidebar.IsVisible
        controller.Apply wide
        Assert.False controller.Scrim.IsVisible
        window.Close())

// 迁自旧 ConversationWorkspaceTests `starting and continuing share the same focused draft and attachment controls`（布局部分）。
[<Fact>]
let ``starting and continuing share the same focused draft and attachment controls`` () =
    Headless.run (fun () ->
        let chat = Border()
        let input = TextBox(AcceptsReturn = true, Text = "保留这段尚未发送的想法")
        let composer = Border(Child = input, Height = 120.0)
        let surface = WorkspaceStack(chat, composer, 560.0)
        let modes = ResizeArray<bool>()
        surface.StartModeChanged.Add modes.Add
        let window = Window(Width = 1000.0, Height = 820.0, Content = surface)
        window.Show()
        surface.RenderStarting true
        input.Focus() |> ignore
        Dispatcher.UIThread.RunJobs()
        let parent = composer.Parent
        Assert.True input.IsFocused
        Assert.True(modes[modes.Count - 1])
        let before = composer.TranslatePoint(Point(), surface).Value.Y
        Assert.True(before < surface.Bounds.Height * 0.75, "起步输入应在画面中部，而非底边")
        surface.RenderStarting false
        Dispatcher.UIThread.RunJobs()
        Assert.False(modes[modes.Count - 1])
        Assert.Same(parent, composer.Parent)
        Assert.True input.IsFocused
        Assert.Equal("保留这段尚未发送的想法", input.Text)
        let after = composer.TranslatePoint(Point(), surface).Value.Y
        Assert.True(after > before)
        window.Height <- 460.0
        surface.RenderStarting true
        Dispatcher.UIThread.RunJobs()
        // 不够高：不居中，输入区仍完整可见
        Assert.False(modes[modes.Count - 1])
        let at = composer.TranslatePoint(Point(), surface).Value
        Assert.True(at.Y + composer.Bounds.Height <= surface.Bounds.Height + 1.0)
        Assert.True input.IsEffectivelyVisible
        window.Close())
