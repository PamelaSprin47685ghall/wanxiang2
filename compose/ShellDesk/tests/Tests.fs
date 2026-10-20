module Wanxiang2.ShellDesk.Tests.Tests

// 真实组合：主题外观 + NavigationController + ShellColumns / WorkspaceStack + BrandMark；宿主控件用占位控件，经插槽记录调用。

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ThemePalette
open Wanxiang2.Theme
open Wanxiang2.ShellDesk
open Wanxiang2.DeskTests

type private Rig =
    { Desk: ShellDesk
      Grid: Grid
      Sidebar: StackPanel
      Search: TextBox
      Calls: ResizeArray<string>
      Window: Window
      Theme: ThemeManager }

let private rig (collapsed: bool) =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let search = TextBox()
    let sidebar = StackPanel()
    sidebar.Children.Add search
    let splitter = GridSplitter()
    let chat = Border()
    let composer = TextBox()
    let calls = ResizeArray<string>()
    let hooks =
        { ApplyCompactMode = fun c -> calls.Add(sprintf "compact:%b" c)
          ApplyStartMode = fun s -> calls.Add(sprintf "start:%b" s)
          FocusComposer = fun () -> calls.Add "focus:composer"
          FocusSidebarSearch = fun () -> calls.Add "focus:search"
          PrefersTouch = fun () -> false }
    let grid = Grid()
    let desk = ShellDesk(theme, grid, sidebar, splitter, chat, composer, hooks, collapsed, fun () -> 280.0)
    let window = Window(Width = 1000.0, Height = 700.0, Content = grid)
    window.Show()
    Dispatcher.UIThread.RunJobs()
    { Desk = desk; Grid = grid; Sidebar = sidebar; Search = search; Calls = calls; Window = window; Theme = theme }

// 迁自 FocusRingTests：窄屏不自动打开抽屉；显式打开后焦点送进抽屉（这里断言组合者要求聚焦搜索框）。
[<Fact>]
let ``窄屏不自动打开抽屉，显式打开后焦点留在抽屉内`` () =
    Headless.run (fun () ->
        let r = rig false
        r.Calls.Clear()
        let s = r.Desk.ApplyViewport 360.0
        Assert.True s.compactMode
        Assert.False s.compactNavigationOpen
        Assert.False r.Sidebar.IsVisible
        Assert.DoesNotContain("focus:search", r.Calls)
        Assert.Contains("compact:true", r.Calls)
        let opened = r.Desk.ToggleSidebar()
        Assert.True opened.compactNavigationOpen
        Assert.True r.Sidebar.IsVisible
        Assert.Equal(2, r.Sidebar.ZIndex)
        Assert.True r.Desk.Columns.Scrim.IsVisible
        Assert.Equal("focus:search", r.Calls[r.Calls.Count - 1])
        r.Window.Close())

// 迁自 UiStabilityTests：桌面折叠侧栏时焦点原在侧栏，回到输入区。
[<Fact>]
let ``collapsing the desktop sidebar returns focus to the composer`` () =
    Headless.run (fun () ->
        let r = rig false
        r.Desk.ApplyViewport 1000.0 |> ignore
        r.Search.Focus() |> ignore
        Dispatcher.UIThread.RunJobs()
        r.Calls.Clear()
        let s = r.Desk.ToggleSidebar()
        Assert.True s.sidebarCollapsed
        Assert.False r.Sidebar.IsVisible
        Assert.Equal<string list>([ "compact:false"; "focus:composer" ], List.ofSeq r.Calls)
        // 焦点不在侧栏时展开 / 折叠不抢焦点
        r.Calls.Clear()
        r.Desk.ToggleSidebar() |> ignore
        r.Desk.ToggleSidebar() |> ignore
        Assert.DoesNotContain("focus:composer", r.Calls)
        r.Window.Close())

// 迁自 AppShellTests：Ctrl+B 在桌面切换折叠，返回的新状态交宿主存偏好；初值来自偏好。
[<Fact>]
let ``ToggleSidebar toggles sidebarCollapsed and persists into prefs`` () =
    Headless.run (fun () ->
        let r = rig true
        r.Desk.ApplyViewport 1000.0 |> ignore
        Assert.False r.Sidebar.IsVisible
        Assert.Equal(0.0, r.Grid.ColumnDefinitions[0].Width.Value)
        let s = r.Desk.ToggleSidebar()
        Assert.False s.sidebarCollapsed
        Assert.True r.Sidebar.IsVisible
        Assert.Equal(280.0, r.Grid.ColumnDefinitions[0].Width.Value)
        r.Window.Close())

// 旧 AppShell：遮罩按下与 Escape 同一归宿——收起抽屉，焦点回输入区。
[<Fact>]
let ``按下抽屉遮罩收起抽屉并把焦点交回输入区`` () =
    Headless.run (fun () ->
        let r = rig false
        r.Desk.ApplyViewport 360.0 |> ignore
        r.Desk.ApplyCompactNavigation true |> ignore
        Assert.True r.Desk.Columns.Scrim.IsVisible
        r.Calls.Clear()
        let args = PointerPressedEventArgs(r.Desk.Columns.Scrim, new Pointer(1, PointerType.Mouse, true), r.Window, Point(1.0, 1.0), 0UL, PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None)
        r.Desk.Columns.Scrim.RaiseEvent args
        Assert.False r.Desk.State.compactNavigationOpen
        Assert.False r.Sidebar.IsVisible
        Assert.False r.Desk.Columns.Scrim.IsVisible
        Assert.Equal("focus:composer", r.Calls[r.Calls.Count - 1])
        r.Window.Close())

// 起步居中经插槽告知对话视图；分隔条拖完只在桌面未折叠时写回宽度。
[<Fact>]
let ``起步模式经插槽同步，拖分隔条只在桌面写回宽度`` () =
    Headless.run (fun () ->
        let r = rig false
        r.Desk.ApplyViewport 1000.0 |> ignore
        r.Calls.Clear()
        r.Desk.RenderStarting true
        Assert.Contains("start:true", r.Calls)
        Assert.True(r.Desk.NoteSidebarDragged().IsSome)
        r.Desk.ApplyViewport 360.0 |> ignore
        Assert.True(r.Desk.NoteSidebarDragged().IsNone)
        r.Window.Close())

// 迁自旧 Brand：没有图时退成「万」字，底与字色是主题的活画笔，随主题切换。
[<Fact>]
let ``品牌标没有图时退成万字并随主题换色`` () =
    Headless.run (fun () ->
        let brand = ShellBrand(None)
        let tile = brand.Logo 40.0 :?> Border
        let glyph = tile.Child :?> TextBlock
        Assert.Equal("万", glyph.Text)
        Assert.True(tile.Classes.Contains "surface")
        Assert.True(glyph.Classes.Contains "brand-glyph"))

// 布局度量归主题管理者：改分隔条宽、窄屏断点，已显示的外壳跟着重投影。
[<Fact>]
let ``改主题的布局度量后已显示的外壳跟着变`` () =
    Headless.run (fun () ->
        let r = rig false
        r.Desk.ApplyViewport 1000.0 |> ignore
        Assert.Equal(4.0, r.Grid.ColumnDefinitions[1].Width.Value)
        r.Theme.ApplyMetric(Wanxiang2.ThemeMetrics.Metric.SidebarSplitterWidth, 9.0)
        Assert.Equal(9.0, r.Grid.ColumnDefinitions[1].Width.Value)
        Assert.False r.Desk.State.compactMode
        r.Theme.ApplyMetric(Wanxiang2.ThemeMetrics.Metric.CompactBreakpoint, 1200.0)
        Assert.True r.Desk.State.compactMode
        Assert.False r.Sidebar.IsVisible
        r.Window.Close())
