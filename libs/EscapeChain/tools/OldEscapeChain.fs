namespace Wanxiang.UI

// 原样摘自 AppShell.fs HandleShortcut：Escape 分支、`| _ when overlay.IsDialogOpen || overlay.IsPopupOpen -> ()`、OpenConversationAt 分支。
// 副作用换成返回目标；overlay.HandleEscape() 的返回值、settingsHost.IsVisible、抽屉状态、generationId.IsSome 换成参数。
[<RequireQualifiedAccess>]
type EscapeTarget =
    | Overlay
    | CloseSettings
    | CloseDrawer
    | StopGeneration
    | Unhandled

module EscapeChain =
    let decide (overlayHandled: bool) (settingsVisible: bool) (drawerOpen: bool) (generating: bool) : EscapeTarget =
        let mutable r = EscapeTarget.Unhandled
        if overlayHandled then r <- EscapeTarget.Overlay
        elif settingsVisible then
            r <- EscapeTarget.CloseSettings
        elif drawerOpen then
            r <- EscapeTarget.CloseDrawer
        elif generating then
            r <- EscapeTarget.StopGeneration
        r

    let blockedByOverlay (dialogOpen: bool) (popupOpen: bool) = dialogOpen || popupOpen

    let conversationAt (index: int) (visibleOrder: string list) : System.Guid option =
        let order = visibleOrder
        let mutable r = None
        if index >= 0 && index < order.Length then
            match System.Guid.TryParse order[index] with
            | true, id ->
                r <- Some id
            | _ -> ()
        r
