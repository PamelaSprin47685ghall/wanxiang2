namespace Wanxiang2.ShellLayout

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.VisualTree

/// 导航状态里布局要的三项（组合者从 NavigationState 的快照取）。
type ShellState =
    {
        Compact: bool
        DrawerOpen: bool
        SidebarCollapsed: bool
    }

/// 同一个输入控件贯穿起步和对话（迁自旧 Views/MainLayout.fs 的 ConversationWorkspace）。
/// 顶栏由舞台钉在本栈之外。起步且舞台够高时，空态与输入区作为一组在顶栏之下垂直居中；
/// 有消息或舞台不够高时输入区贴底、对话列滚动。门槛看父级高度：顶栏占掉的高度不能把 600 的窗口判成贴底。
type WorkspaceStack(chat: Control, composer: Control, startMinHeight: float) as this =
    inherit DockPanel()

    let top = RowDefinition(Height = GridLength 0.0)
    let thread = RowDefinition(Height = GridLength.Star, MinHeight = 48.0)
    let bottom = RowDefinition(Height = GridLength 0.0)
    let grid = Grid()
    let mutable starting = false
    let mutable minHeight = startMinHeight
    let startMode = Event<bool>()
    let mutable parentHook: IDisposable option = None

    let gateHeight () =
        match this.GetVisualParent() with
        | null -> this.Bounds.Height
        | parent ->
            let parentHeight = parent.Bounds.Height
            let ownHeight = this.Bounds.Height
            // 父级比本栈更高，说明顶栏（或其它钉在父级上的条）在本栈之外。门槛用父级。
            if parentHeight > ownHeight + 1.0 then parentHeight
            elif ownHeight > 0.0 then ownHeight
            else parentHeight

    let setLength (row: RowDefinition) (height: GridLength) (min: float) =
        if row.Height <> height then
            row.Height <- height

        if row.MinHeight <> min then
            row.MinHeight <- min

    let apply () =
        let centered = starting && gateHeight () >= minHeight

        if centered then
            setLength top (GridLength.Star) 0.0
            setLength thread (GridLength.Auto) 0.0
            setLength bottom (GridLength.Star) 0.0
        else
            setLength top (GridLength 0.0) 0.0
            setLength thread (GridLength.Star) 48.0
            setLength bottom (GridLength 0.0) 0.0

        startMode.Trigger centered

    let watchParent () =
        parentHook |> Option.iter (fun d -> d.Dispose())

        match this.GetVisualParent() with
        | null -> parentHook <- None
        | parent ->
            parentHook <- Some(parent.GetPropertyChangedObservable(Visual.BoundsProperty).Subscribe(fun _ -> apply ()))

    do
        grid.RowDefinitions.Add top
        grid.RowDefinitions.Add thread
        grid.RowDefinitions.Add(RowDefinition(Height = GridLength.Auto))
        grid.RowDefinitions.Add bottom
        Grid.SetRow(chat, 1)
        Grid.SetRow(composer, 2)
        grid.Children.Add chat
        grid.Children.Add composer
        this.Children.Add grid

        this.AttachedToVisualTree.Add(fun _ ->
            watchParent ()
            apply ())

        this.DetachedFromVisualTree.Add(fun _ ->
            parentHook |> Option.iter (fun d -> d.Dispose())
            parentHook <- None)

        this.PropertyChanged.Add(fun e ->
            if e.Property = Visual.BoundsProperty then
                apply ())

    new(chat: Control, composer: Control) = WorkspaceStack(chat, composer, 560.0)

    /// 起步态投影（幂等，每帧可调）：置位并立即重算居中。
    member _.RenderStarting(value: bool) =
        starting <- value
        apply ()

    member _.IsStarting = starting

    /// 是否居中（每次重算都发）
    [<CLIEvent>]
    member _.StartModeChanged = startMode.Publish

/// 主工作区的空间投影（迁自旧 MainLayoutController）：导航状态 → 三列宽度、各块所在列、抽屉 ZIndex 与遮罩。
/// 列宽写操作只出自这里。紧凑模式每次 Apply 都发 `CompactApplied`，组合者转给输入区、侧栏、对话视图。
type ShellColumns
    (
        shellGrid: Grid,
        sidebar: Control,
        splitter: Control,
        chatColumn: Control,
        sidebarMaxWidth: float,
        splitterWidth: float,
        sidebarWidth: unit -> float
    ) =
    let compactApplied = Event<bool>()
    let scrimPressed = Event<unit>()
    let focusHome = Event<unit>()
    // 侧栏最小宽与偏好下限同一数：列宽只从这里投影，不再有单独的写接口。
    let minWidth = 232.0
    let maxWidth = sidebarMaxWidth
    let splitterW = splitterWidth

    /// compact 抽屉遮罩：内容（ZIndex 0）与侧栏（2）之间一层，只做不透明度淡入，按下即收起抽屉。
    let scrim = Border(IsVisible = false, Opacity = 0.0, ZIndex = 1)

    let apply (state: ShellState) =
        compactApplied.Trigger state.Compact
        let cols = shellGrid.ColumnDefinitions

        if state.Compact then
            // 窄屏第 0 列就是舞台。宽屏留给侧栏的最大宽不能把舞台截在 420。
            cols[0].MaxWidth <- Double.PositiveInfinity
            cols[0].MinWidth <- 0.0
            cols[0].Width <- GridLength.Star
            cols[1].Width <- GridLength 0.0
            cols[2].Width <- GridLength 0.0
            Grid.SetColumn(sidebar, 0)
            Grid.SetColumn(chatColumn, 0)
            splitter.IsVisible <- false
            sidebar.IsVisible <- state.DrawerOpen
            sidebar.ZIndex <- 2
            // 打开淡入；关闭立即隐藏并复位（收起是状态切换，不淡出）。
            scrim.IsVisible <- state.DrawerOpen
            scrim.Opacity <- if state.DrawerOpen then 1.0 else 0.0
        else
            cols[0].MaxWidth <- maxWidth
            cols[0].MinWidth <- if state.SidebarCollapsed then 0.0 else minWidth

            cols[0].Width <-
                if state.SidebarCollapsed then
                    GridLength 0.0
                else
                    GridLength(sidebarWidth ())

            cols[1].Width <-
                if state.SidebarCollapsed then
                    GridLength 0.0
                else
                    GridLength splitterW

            cols[2].Width <- GridLength.Star
            Grid.SetColumn(sidebar, 0)
            Grid.SetColumn(splitter, 1)
            Grid.SetColumn(chatColumn, 2)
            // 折叠前读出焦点：隐藏后 Avalonia 会把焦点清成 null，键盘用户按完 Ctrl+B 就失焦。
            let focusWasInSidebar =
                match TopLevel.GetTopLevel sidebar with
                | null -> false
                | top ->
                    match top.FocusManager with
                    | null -> false
                    | manager ->
                        match manager.GetFocusedElement() with
                        | :? Control as focused -> (sidebar :> Visual).IsVisualAncestorOf focused
                        | _ -> false

            sidebar.IsVisible <- not state.SidebarCollapsed
            splitter.IsVisible <- not state.SidebarCollapsed
            sidebar.ZIndex <- 0

            if state.SidebarCollapsed && focusWasInSidebar then
                focusHome.Trigger()

            scrim.IsVisible <- false
            scrim.Opacity <- 0.0

    do
        scrim.Classes.Add "scrim"

        let fade =
            Avalonia.Animation.DoubleTransition(
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds 150.0
            )

        let ts = Avalonia.Animation.Transitions()
        ts.Add fade
        scrim.Transitions <- ts
        Grid.SetColumn(scrim, 0)
        shellGrid.Children.Add scrim

        scrim.PointerPressed.Add(fun e ->
            e.Handled <- true
            scrimPressed.Trigger())

    new(shellGrid: Grid, sidebar: Control, splitter: Control, chatColumn: Control, sidebarWidth: unit -> float) =
        ShellColumns(shellGrid, sidebar, splitter, chatColumn, 420.0, 6.0, sidebarWidth)

    member val Scrim: Border = scrim

    member _.Apply(state: ShellState) = apply state

    /// 用户拖拽后的宽度落定：钳进 [最小, 最大] 并写回第 0 列；返回值由组合者存进偏好。
    member _.NoteUserSidebarWidth(actual: float) : float =
        let width = Math.Clamp(actual, minWidth, max minWidth maxWidth)
        shellGrid.ColumnDefinitions[0].Width <- GridLength width
        width

    /// 每次 Apply 都发，参数为是否紧凑
    [<CLIEvent>]
    member _.CompactApplied = compactApplied.Publish

    /// 点了抽屉遮罩（组合者收起 compact 抽屉）
    [<CLIEvent>]
    member _.ScrimPressed = scrimPressed.Publish

    /// 折叠时焦点原在侧栏：组合者把焦点交给输入区
    [<CLIEvent>]
    member _.FocusHomeRequested = focusHome.Publish
