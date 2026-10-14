module Tests

open System
open Xunit
open Wanxiang.UI

// 迁自旧 UiStabilityTests；窄屏断点 720 与旧 LayoutPolicy.compactBreakpoint 相同，由宿主传入。
[<Fact>]
let ``navigation controller owns compact and collapsed state transitions`` () =
    let navigation = NavigationController(true, 720.0)
    let applyWide, wide = navigation.ApplyViewport(1200.0)
    Assert.True applyWide
    Assert.False wide.compactMode
    Assert.True wide.sidebarCollapsed

    let opened = navigation.EnsureSearchVisible()
    Assert.False opened.sidebarCollapsed

    let applyCompact, compact = navigation.ApplyViewport(600.0)
    Assert.True applyCompact
    Assert.True compact.compactMode
    Assert.False compact.compactNavigationOpen

    let toggled = navigation.ToggleSidebar()
    Assert.True toggled.compactNavigationOpen
    let closed = navigation.SetCompactNavigation(false)
    Assert.False closed.compactNavigationOpen


// 迁自旧 AppShell.ApplyResponsiveLayout 的聚焦条件：与原样摘录在全部状态组合上逐例比对。
[<Fact>]
let ``drawer autofocus matches old ApplyResponsiveLayout`` () =
    let old (before: NavigationSnapshot) (state: NavigationSnapshot) (prefersTouch: bool) =
        state.compactMode && state.compactNavigationOpen
        && not (before.compactMode && before.compactNavigationOpen)
        && not prefersTouch
    let all =
        [ for c in [ false; true ] do
            for o in [ false; true ] do
                for s in [ false; true ] do
                    { compactMode = c; compactNavigationOpen = o; sidebarCollapsed = s } ]
    for b in all do
        for a in all do
            for t in [ false; true ] do
                Assert.Equal(old b a t, NavigationSnapshot.autofocusDrawer b a t)
