namespace Wanxiang.UI

/// 全局 Escape 落到谁（旧 AppShell.HandleShortcut 的 Escape 分支）。浮层（对话框 / 弹出菜单 / 提示条）先由宿主自己试，
/// 没接住时按这里的优先级：设置页 → 窄屏抽屉 → 正在生成。抽屉先于停止生成（D3/D4）。
[<RequireQualifiedAccess>]
type EscapeTarget =
    | Overlay
    | CloseSettings
    | CloseDrawer
    | StopGeneration
    | Unhandled

module EscapeChain =

    let decide (overlayHandled: bool) (settingsVisible: bool) (drawerOpen: bool) (generating: bool) : EscapeTarget =
        if overlayHandled then EscapeTarget.Overlay
        elif settingsVisible then EscapeTarget.CloseSettings
        elif drawerOpen then EscapeTarget.CloseDrawer
        elif generating then EscapeTarget.StopGeneration
        else EscapeTarget.Unhandled

    /// 除 Escape 外，对话框或弹出菜单打开时其余快捷键一律不接管。
    let blockedByOverlay (dialogOpen: bool) (popupOpen: bool) = dialogOpen || popupOpen

    /// Ctrl+1..9：按侧栏当前可见顺序取第 index 个对话（C2）。
    let conversationAt (index: int) (visibleOrder: string list) : System.Guid option =
        if index >= 0 && index < visibleOrder.Length then
            match System.Guid.TryParse visibleOrder[index] with
            | true, id -> Some id
            | _ -> None
        else None
