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

/// 从主题管理者取外壳与品牌标的外观。
module ShellLook =
    /// 经减弱动效门控的时长：时长改了或开关变了都重推
    let duration (t: ThemeManager) (timing: Timing) : IObservable<TimeSpan> =
        { new IObservable<TimeSpan> with
            member _.Subscribe(o: IObserver<TimeSpan>) =
                let live = t.Timing timing
                let push () = o.OnNext(t.Motion.Duration live.Value)
                let a = live.Subscribe(fun _ -> push ())
                let b = t.Motion.Changed.Subscribe(fun _ -> push ())
                { new IDisposable with member _.Dispose() = a.Dispose(); b.Dispose() } }

    let layoutOf (t: ThemeManager) : ShellLayoutLook =
        { Scrim = t.Brush PaletteKey.Scrim
          ScrimFade = duration t Timing.ScrimFade
          SidebarMinWidth = t.Metric Metric.SidebarMinWidth
          SidebarMaxWidth = t.Metric Metric.SidebarMaxWidth
          SplitterWidth = LiveValue LayoutPolicy.sidebarSplitterWidth
          ChatMinHeight = t.Metric Metric.BarHeight
          StartMinHeight = LiveValue LayoutPolicy.startWorkspaceMinHeight }

    let brandOf (t: ThemeManager) : BrandMarkLook =
        { Surface = t.Brush PaletteKey.Surface
          Accent = t.Brush PaletteKey.Accent
          RadiusRatio = t.Metric Metric.LogoRadiusRatio
          GlyphRatio = LiveValue 0.42 }

/// 品牌标（旧 Brand.logo）：图只找一次；找图顺序同旧代码。
type ShellBrand(look: BrandMarkLook, image: IImage option) =
    /// 旧代码的找图顺序：内嵌资源 → 程序目录 logo.png → 程序目录 pwa/logo.png → WANXIANG_HOME（缺省 ~/.config/wanxiang）/logo.png
    static member LoadImage() : IImage option =
        let home =
            match Environment.GetEnvironmentVariable "WANXIANG_HOME" with
            | s when not (String.IsNullOrWhiteSpace s) -> s
            | _ -> IO.Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".config", "wanxiang")
        BrandMark.tryLoad
            (Some(Uri "avares://Wanxiang.UI/Assets/logo.png"))
            [ IO.Path.Combine(AppContext.BaseDirectory, "logo.png")
              IO.Path.Combine(AppContext.BaseDirectory, "pwa", "logo.png")
              IO.Path.Combine(home, "logo.png") ]
        |> Option.map (fun b -> b :> IImage)

    new(theme: ThemeManager) = ShellBrand(ShellLook.brandOf theme, ShellBrand.LoadImage())

    /// 边长为 size 的品牌标（侧栏、空状态、头像都用它）
    member _.Logo(size: float) : Control = BrandMark.tile look image size

/// 宿主里还没迁出的控件（侧栏、对话视图、输入区）由组合者经这些插槽驱动。
type ShellHooks =
    { /// 紧凑模式同步给输入区、侧栏、对话视图
      SetCompactMode: bool -> unit
      /// 起步居中同步给对话视图
      SetStartMode: bool -> unit
      FocusComposer: unit -> unit
      FocusSidebarSearch: unit -> unit
      /// 触屏优先时不自动聚焦搜索框（软键盘会盖住列表）
      PrefersTouch: unit -> bool }

/// 主工作区的接线：NavigationController 决定状态，ShellColumns 把状态投影到 Grid，WorkspaceStack 让起步与续聊共用输入区。
/// 焦点归宿（D3/D4）在这里：窄屏抽屉打开进搜索框、收起回输入区；桌面折叠时焦点原在侧栏也回输入区；点遮罩与 Escape 同一路径收起抽屉。
type ShellDesk
    (look: ShellLayoutLook,
     shellGrid: Grid,
     sidebar: Control,
     splitter: Control,
     chat: Control,
     composer: Control,
     hooks: ShellHooks,
     initialSidebarCollapsed: bool,
     sidebarWidth: unit -> float) =
    let navigation = NavigationController(initialSidebarCollapsed, LayoutPolicy.compactBreakpoint)
    let workspace = WorkspaceStack(chat, composer, look)
    let columns = ShellColumns(shellGrid, sidebar, splitter, workspace, look, sidebarWidth)

    let project (s: NavigationSnapshot) =
        columns.Apply { Compact = s.compactMode; DrawerOpen = s.compactNavigationOpen; SidebarCollapsed = s.sidebarCollapsed }

    let setCompactNavigation (opened: bool) =
        let before = navigation.State
        let state = navigation.SetCompactNavigation opened
        if state.compactMode then
            project state
            if opened then (if not (hooks.PrefersTouch()) then hooks.FocusSidebarSearch())
            elif before.compactNavigationOpen then hooks.FocusComposer()
        state

    do
        columns.CompactApplied.Add hooks.SetCompactMode
        columns.FocusHomeRequested.Add(fun () -> hooks.FocusComposer())
        columns.ScrimPressed.Add(fun () -> setCompactNavigation false |> ignore)
        workspace.StartModeChanged.Add hooks.SetStartMode
        // 列定义只建空的，初值由第一次投影落定
        if shellGrid.ColumnDefinitions.Count = 0 then
            shellGrid.ColumnDefinitions.Add(ColumnDefinition())
            shellGrid.ColumnDefinitions.Add(ColumnDefinition())
            shellGrid.ColumnDefinitions.Add(ColumnDefinition(Width = GridLength.Star))
        look.SidebarMaxWidth.Subscribe(fun v -> shellGrid.ColumnDefinitions[0].MaxWidth <- v) |> ignore
        for c in [ sidebar; splitter; workspace :> Control ] do
            if not (shellGrid.Children.Contains c) then shellGrid.Children.Add c
        project navigation.State

    new(theme: ThemeManager, shellGrid, sidebar, splitter, chat, composer, hooks, initialSidebarCollapsed, sidebarWidth) =
        ShellDesk(ShellLook.layoutOf theme, shellGrid, sidebar, splitter, chat, composer, hooks, initialSidebarCollapsed, sidebarWidth)

    member _.State = navigation.State
    /// 对话区一列（对话视图 + 输入区）
    member _.Workspace = workspace
    member _.Columns = columns

    /// 起步页（新对话、无消息）与续聊切换
    member _.SetStarting(value: bool) = workspace.SetStarting value

    /// 视口宽度变化；抽屉因此刚打开时把焦点送进搜索框（触屏除外）
    member _.ApplyViewport(width: float) =
        if width > 0.0 then
            let before = navigation.State
            let needsApply, state = navigation.ApplyViewport width
            if needsApply then project state
            if NavigationSnapshot.autofocusDrawer before state (hooks.PrefersTouch()) then hooks.FocusSidebarSearch()
        navigation.State

    /// Ctrl+B：窄屏开合抽屉（打开进搜索框、收起回输入区），桌面折叠侧栏；返回新状态，宿主据此存偏好
    member _.ToggleSidebar() =
        let state = navigation.ToggleSidebar()
        project state
        if state.compactMode then
            if state.compactNavigationOpen then hooks.FocusSidebarSearch() else hooks.FocusComposer()
        state

    /// 窄屏抽屉开合（Escape、侧栏行、遮罩都走这里）
    member _.SetCompactNavigation(opened: bool) = setCompactNavigation opened

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
        else None
