namespace Wanxiang2.ShellDesk

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch
open Wanxiang2.Theme
open Wanxiang2.ShellLayout
open Wanxiang2.BrandMark

/// 品牌标（旧 Brand.logo）：图只找一次；找图顺序同旧代码。
type ShellBrand(image: IImage option) =
    /// 旧代码的找图顺序：内嵌资源 → 程序目录 logo.png → 程序目录 pwa/logo.png → WANXIANG_HOME（缺省 ~/.config/wanxiang）/logo.png
    static member LoadImage() : IImage option =
        let home =
            match Environment.GetEnvironmentVariable "WANXIANG_HOME" with
            | s when not (String.IsNullOrWhiteSpace s) -> s
            | _ ->
                IO.Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".config", "wanxiang")

        BrandMark.tryLoad
            (Some(Uri "avares://Wanxiang.UI/Assets/logo.png"))
            [
                IO.Path.Combine(AppContext.BaseDirectory, "logo.png")
                IO.Path.Combine(AppContext.BaseDirectory, "pwa", "logo.png")
                IO.Path.Combine(home, "logo.png")
            ]
        |> Option.map (fun b -> b :> IImage)

    new(_theme: ThemeManager) = ShellBrand(ShellBrand.LoadImage())
    new() = ShellBrand(ShellBrand.LoadImage())

    /// 边长为 size 的品牌标（侧栏、空状态、头像都用它）
    member _.Logo(size: float) : Control = BrandMark.tile image size

/// 侧栏、对话视图、输入区与本组合者保持解耦，由外层宿主经这些插槽同步状态与焦点。
type ShellHooks =
    {
        /// 紧凑模式同步给输入区、侧栏、对话视图
        ApplyCompactMode: bool -> unit
        /// 起步居中同步给对话视图
        ApplyStartMode: bool -> unit
        FocusComposer: unit -> unit
        FocusSidebarSearch: unit -> unit
        /// 触屏优先时不自动聚焦搜索框（软键盘会盖住列表）
        PrefersTouch: unit -> bool
    }

/// 主工作区的接线：NavigationController 决定状态，ShellColumns 把状态投影到 Grid，WorkspaceStack 让起步与续聊共用输入区。
/// 焦点归宿（D3/D4）在这里：窄屏抽屉打开进搜索框、收起回输入区；桌面折叠时焦点原在侧栏也回输入区；点遮罩与 Escape 同一路径收起抽屉。
type ShellDesk
    (
        shellGrid: Grid,
        sidebar: Control,
        splitter: Control,
        chat: Control,
        composer: Control,
        hooks: ShellHooks,
        initialSidebarCollapsed: bool,
        sidebarWidth: unit -> float,
        compactBreakpoint: IObservable<float>,
        splitterWidthOpt: IObservable<float> option
    ) as this =
    let mutable breakpoint = 0.0
    do compactBreakpoint.Subscribe(fun v -> breakpoint <- v).Dispose()
    let mutable navigation = NavigationController(initialSidebarCollapsed, breakpoint)
    let mutable lastWidth = 0.0
    // 600：空态与输入区只在舞台不低于这个高度时成组居中。默认构造的 560 留给不经过本桌的直接测试。
    let workspace = WorkspaceStack(chat, composer, 600.0)
    let stage = DockPanel()

    let initSplitterW =
        match splitterWidthOpt with
        | Some opt ->
            (let mutable w = 6.0 in
             opt.Subscribe(fun v -> w <- v).Dispose()
             w)
        | None -> 6.0

    let columns =
        ShellColumns(shellGrid, sidebar, splitter, stage, 420.0, initSplitterW, sidebarWidth)

    let project (s: NavigationSnapshot) =
        columns.Apply
            {
                Compact = s.compactMode
                DrawerOpen = s.compactNavigationOpen
                SidebarCollapsed = s.sidebarCollapsed
            }

    let applyCompactNavigation (opened: bool) =
        let before = navigation.State
        let state = navigation.ApplyCompactNavigation opened

        if state.compactMode then
            project state

            if opened then
                (if not (hooks.PrefersTouch()) then
                     hooks.FocusSidebarSearch())
            elif before.compactNavigationOpen then
                hooks.FocusComposer()

        state

    do
        columns.CompactApplied.Add hooks.ApplyCompactMode
        columns.FocusHomeRequested.Add(fun () -> hooks.FocusComposer())
        columns.ScrimPressed.Add(fun () -> applyCompactNavigation false |> ignore)
        workspace.StartModeChanged.Add hooks.ApplyStartMode
        // 列定义只建空的，初值由第一次投影落定
        if shellGrid.ColumnDefinitions.Count = 0 then
            shellGrid.ColumnDefinitions.Add(ColumnDefinition())
            shellGrid.ColumnDefinitions.Add(ColumnDefinition())
            shellGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))

        stage.Children.Add workspace

        for c in [ sidebar; splitter; stage :> Control ] do
            if not (shellGrid.Children.Contains c) then
                shellGrid.Children.Add c

        project navigation.State

        splitterWidthOpt
        |> Option.iter (fun opt ->
            opt.Subscribe(fun w ->
                if not this.State.compactMode && not this.State.sidebarCollapsed then
                    shellGrid.ColumnDefinitions[1].Width <- GridLength w)
            |> ignore)
        // 断点改了：按当前折叠偏好换一个导航状态机，用上次视口宽度重投影（模式变了抽屉本就收起）
        compactBreakpoint.Subscribe(fun v ->
            if v <> breakpoint then
                breakpoint <- v
                navigation <- NavigationController(navigation.State.sidebarCollapsed, v)

                if lastWidth > 0.0 then
                    let _, state = navigation.ApplyViewport lastWidth
                    project state)
        |> ignore

    new(theme: ThemeManager, shellGrid, sidebar, splitter, chat, composer, hooks, initialSidebarCollapsed, sidebarWidth) =
        ShellDesk(
            shellGrid,
            sidebar,
            splitter,
            chat,
            composer,
            hooks,
            initialSidebarCollapsed,
            sidebarWidth,
            theme.Metric Metric.CompactBreakpoint,
            Some(theme.Metric Metric.SidebarSplitterWidth)
        )

    new(shellGrid, sidebar, splitter, chat, composer, hooks, initialSidebarCollapsed, sidebarWidth, compactBreakpoint) =
        ShellDesk(
            shellGrid,
            sidebar,
            splitter,
            chat,
            composer,
            hooks,
            initialSidebarCollapsed,
            sidebarWidth,
            compactBreakpoint,
            None
        )

    member _.State = navigation.State
    /// 对话区一列（对话视图 + 输入区）。顶栏在它外面。
    member _.Workspace = workspace
    member _.Columns = columns

    /// 把顶栏钉在舞台顶部，使起步组的垂直居中发生在顶栏之下。
    member _.DockChrome(header: Control) =
        if not (stage.Children.Contains header) then
            DockPanel.SetDock(header, Dock.Top)
            stage.Children.Insert(0, header)

    /// 起步页（新对话、无消息）与续聊切换（投影）
    member _.RenderStarting(value: bool) = workspace.RenderStarting value

    /// 视口宽度变化；抽屉因此刚打开时把焦点送进搜索框（触屏除外）
    member _.ApplyViewport(width: float) =
        if width > 0.0 then
            lastWidth <- width
            let before = navigation.State
            let needsApply, state = navigation.ApplyViewport width

            if needsApply then
                project state

            if NavigationSnapshot.autofocusDrawer before state (hooks.PrefersTouch()) then
                hooks.FocusSidebarSearch()

        navigation.State

    /// Ctrl+B：窄屏开合抽屉（打开进搜索框、收起回输入区），桌面折叠侧栏；返回新状态，宿主据此存偏好
    member _.ToggleSidebar() =
        let state = navigation.ToggleSidebar()
        project state

        if state.compactMode then
            if state.compactNavigationOpen then
                hooks.FocusSidebarSearch()
            else
                hooks.FocusComposer()

        state

    /// 窄屏抽屉开合（Escape、侧栏行、遮罩都走这里）
    member _.ApplyCompactNavigation(opened: bool) = applyCompactNavigation opened

    /// 聚焦侧栏搜索前确保侧栏可见
    member _.EnsureSearchVisible() =
        let state = navigation.EnsureSearchVisible()
        project state
        state

    /// 用户拖完分隔条：桌面且未折叠时钳制并写回，返回要存进偏好的宽度
    member _.NoteSidebarDragged() : float option =
        let nav = navigation.State

        if not nav.compactMode && not nav.sidebarCollapsed then
            Some(columns.NoteUserSidebarWidth shellGrid.ColumnDefinitions[0].ActualWidth)
        else
            None
