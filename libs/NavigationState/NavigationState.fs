namespace Wanxiang.UI

open System

/// 主壳导航的纯状态。视图只负责把这个状态投影成 Grid/Visibility；
/// “窗口变窄时该开什么、Ctrl+B 在 compact/desktop 分别意味着什么”集中在这里。
type NavigationSnapshot =
    {
        compactMode: bool
        compactNavigationOpen: bool
        sidebarCollapsed: bool
    }

/// compactBreakpoint：窄屏断点，唯一事实来源在设计层（LayoutPolicy.compactBreakpoint），由宿主传入。
type NavigationController(initialSidebarCollapsed: bool, compactBreakpoint: float) =
    let mutable state =
        {
            compactMode = false
            compactNavigationOpen = false
            sidebarCollapsed = initialSidebarCollapsed
        }

    let mutable viewportApplied = false

    member _.State = state

    /// 返回是否需要重新投影布局，以及更新后的状态。
    member _.ApplyViewport(width: float) : bool * NavigationSnapshot =
        if width <= 0.0 then
            false, state
        else
            let compact = width < compactBreakpoint
            let changed = compact <> state.compactMode
            let needsApply = changed || not viewportApplied

            if needsApply then
                viewportApplied <- true

                state <-
                    if compact then
                        { state with
                            compactMode = true
                            // 窄屏起步仍先看到输入区。只有显式导航动作才打开抽屉。
                            compactNavigationOpen = if changed then false else state.compactNavigationOpen
                        }
                    else
                        { state with
                            compactMode = false
                            compactNavigationOpen = false
                        }

            needsApply, state

    member _.ToggleSidebar() =
        state <-
            if state.compactMode then
                { state with
                    compactNavigationOpen = not state.compactNavigationOpen
                }
            else
                { state with
                    sidebarCollapsed = not state.sidebarCollapsed
                }

        state

    /// 窄屏抽屉开合（单槽投影，幂等）：返回更新后的状态快照。
    member _.ApplyCompactNavigation(opened: bool) =
        if state.compactMode then
            state <-
                { state with
                    compactNavigationOpen = opened
                }

        state

    member _.EnsureSearchVisible() =
        state <-
            if state.compactMode then
                { state with
                    compactNavigationOpen = true
                }
            elif state.sidebarCollapsed then
                { state with sidebarCollapsed = false }
            else
                state

        state

module NavigationSnapshot =

    /// 窄屏抽屉是否刚从「未开」翻到「窄屏下打开」（迁自旧 AppShell.ApplyResponsiveLayout / SetCompactNavigation 的同口径条件）。
    let drawerJustOpened (before: NavigationSnapshot) (after: NavigationSnapshot) =
        after.compactMode
        && after.compactNavigationOpen
        && not (before.compactMode && before.compactNavigationOpen)

    /// 视口变化后是否把焦点送进抽屉（搜索框）：抽屉刚打开，且不是触屏优先（触屏聚焦会弹出软键盘）。
    let autofocusDrawer (before: NavigationSnapshot) (after: NavigationSnapshot) (prefersTouch: bool) =
        drawerJustOpened before after && not prefersTouch
