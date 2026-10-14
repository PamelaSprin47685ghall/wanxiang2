namespace Wanxiang.UI

/// 旧 AppShell Refresh 的 startReady 照抄（activeConvId 与 activeConversation() 换成两个 option）
module Adapter =
    let isReady (authenticated: bool) (catalogReady: bool) (generating: bool) (hasError: bool) (activeConvId: int option) (view: int option) =
        authenticated && catalogReady && not generating && (if hasError then Some () else None).IsNone
        && (match activeConvId, view with
            | None, _ -> true
            | Some _, Some messageCount -> messageCount = 0
            | _ -> false)
