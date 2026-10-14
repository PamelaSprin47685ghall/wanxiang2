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

