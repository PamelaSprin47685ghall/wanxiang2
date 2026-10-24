namespace Wanxiang2.Workspace

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Layout
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ShellDesk
open Wanxiang2.SidebarDesk
open Wanxiang2.ChatScreen
open Wanxiang2.ComposerDesk
open Wanxiang2.OverlayDesk
open Wanxiang2.WorkspaceState

/// 宿主给工作区的几样
type WorkspaceHooks =
    {
        /// 触屏优先：抽屉打开时不自动聚焦搜索框
        PrefersTouch: unit -> bool
        /// 偏好里的侧栏宽度
        SidebarWidth: unit -> float
        InitialSidebarCollapsed: bool
    }

/// 组合者：主工作区（旧 AppShell.Build 的布局段 + ApplyResponsiveLayout）。
/// ShellDesk 管列与导航状态，SidebarDesk 是左列，ChatScreen + ComposerDesk 是对话列，OverlayDesk 的根是本工作区的外层。
/// 视口宽度默认取自身宽度；浏览器宿主用 ObserveViewport 喂 CSS 视口宽（轮询计时器归宿主），之后以它为准。
type Workspace
    (
        theme: ThemeManager,
        overlayRoot: Grid,
        sidebar: SidebarDesk,
        chat: ChatScreen,
        composer: ComposerDesk,
        hooks: WorkspaceHooks
    ) =
    let shellGrid = Grid()

    let splitter =
        GridSplitter(
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            KeyboardIncrement = 8.0,
            DragIncrement = 1.0,
            Focusable = true,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            IsVisible = not hooks.InitialSidebarCollapsed
        )

    let host = Grid()
    let sidebarResized = Event<float>()
    let mutable cssWidth: float option = None

    let shellHooks: ShellHooks =
        {
            // 紧凑与否由视口投影到列。这里若用 init() 重画，会把已连接的空态刷回「未连接」。
            ApplyCompactMode = chat.ApplyCompactMode
            ApplyStartMode = chat.WireStartMode
            FocusComposer = composer.Focus
            FocusSidebarSearch = fun () -> sidebar.FocusSearch()
            PrefersTouch = hooks.PrefersTouch
        }

    let shell =
        ShellDesk(
            theme,
            shellGrid,
            sidebar.View,
            splitter,
            chat.Host,
            composer.Host,
            shellHooks,
            hooks.InitialSidebarCollapsed,
            hooks.SidebarWidth
        )

    /// 宽度以 CSS 视口为准（有的话），否则取自身宽度
    let apply () =
        let width = cssWidth |> Option.defaultValue host.Bounds.Width

        if width > 0.0 then
            shell.ApplyViewport width |> ignore
            // 水平留白跟窗口视口，不跟侧栏打开后的舞台宽度。
            chat.ApplyViewportWidth width
            composer.ApplyViewportWidth width

    do
        AutomationProperties.SetName(splitter, "调整侧边栏宽度")
        splitter.Classes.Add StyleClass.surface
        splitter.Classes.Add StyleClass.hoverPlain
        splitter.Transitions <- theme.StateTransitions false
        splitter.DragCompleted.Add(fun _ -> shell.NoteSidebarDragged() |> Option.iter sidebarResized.Trigger)
        host.Children.Add shellGrid
        host.Background <- theme.Brush Wanxiang2.ThemePalette.PaletteKey.Canvas

        if not (overlayRoot.Children.Contains host) then
            overlayRoot.Children.Insert(0, host)

        host.PropertyChanged.Add(fun e ->
            if e.Property = Visual.BoundsProperty then
                apply ())

        shell.DockChrome(chat.DetachHeader())

    /// 工作区根（已插进浮层根的最底层）
    member _.Host: Control = host
    member _.Shell = shell
    member _.Splitter = splitter
    member _.State = shell.State

    /// 用户拖完分隔条，带要写进偏好的宽度（只在桌面未折叠时）
    member _.SidebarResized = sidebarResized.Publish

    /// 浏览器 CSS 视口宽（zoom、DPR、换显示器都不可靠触发 Bounds）；有值后以它为准
    member _.ObserveViewport(width: float) =
        if width > 0.0 && cssWidth <> Some width then
            cssWidth <- Some width
            apply ()

    /// 立即按当前宽度重投影
    member _.ApplyResponsiveLayout() = apply ()

    /// Ctrl+B；返回新状态，宿主据此存偏好
    member _.ToggleSidebar() = shell.ToggleSidebar()
    member _.EnsureSearchVisible() = shell.EnsureSearchVisible()

    /// 单入口幂等渲染：以单一状态树的 ShellVS 为准（紧凑/抽屉/起步态/侧栏开合）
    member this.Render(vs: ShellVS) =
        shell.RenderStarting vs.StartingPageVisible
        // 窄屏抽屉只由快捷键 / 遮罩 / Escape 开合。模型上的 CompactMode 不会跟着视口走，
        // 若在这里按模型再 Toggle 一次，每次重投影都会把抽屉翻面。
        if shell.State.compactMode then
            ()
        elif vs.SidebarOpen = shell.State.sidebarCollapsed then
            shell.ToggleSidebar() |> ignore
