namespace Wanxiang.UI

open System

// 照抄旧 Views/MainLayout.fs apply 与 NoteUserSidebarWidth 的计算
module StartLayout =

    /// 起始页把输入区居中：处于起始态且窗口够高（LayoutPolicy.startWorkspaceMinHeight）
    let centered (starting: bool) (height: float) (minHeight: float) = starting && height >= minHeight

    /// 用户拖拽后的侧栏宽度：钳进 [min, max]（Tokens.sidebarMinWidth/MaxWidth）
    let sidebarWidth (actual: float) (minWidth: float) (maxWidth: float) = Math.Clamp(actual, minWidth, maxWidth)
