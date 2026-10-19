namespace Wanxiang.UI

/// 当前打开的对话处于哪种状态。
[<RequireQualifiedAccess>]
type ActiveConversation =
    /// 没有打开对话（新对话）
    | None
    /// 打开了但快照还没到
    | Loading
    /// 已加载，附消息条数
    | Loaded of messageCount: int

/// 「开始对话」界面是否就绪（旧 AppShell.fs Refresh 的 startReady）。
module StartReady =

    /// 已认证、目录就绪、没在生成、上一轮没有错误，且当前是新对话或空对话。
    let isReady (authenticated: bool) (catalogReady: bool) (generating: bool) (hasError: bool) (active: ActiveConversation) =
        authenticated && catalogReady && not generating && not hasError
        && (match active with
            | ActiveConversation.None -> true
            | ActiveConversation.Loaded count -> count = 0
            | ActiveConversation.Loading -> false)
