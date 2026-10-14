namespace Wanxiang.UI

/// 已打开对话时末尾的错误卡来源（限定访问）
[<RequireQualifiedAccess>]
type TailError =
    | None
    | RunError
    | Unanswered

/// 已打开对话时的呈现
type ConversationSurfaceView =
    { /// 没有消息、流式、错误时显示「空对话」
      showEmpty: bool
      tailError: TailError
      generatingText: string
      composerEnabled: bool
      composerReason: string
      canStop: bool }

/// 旧 AppShell.fs 刷新时「已打开对话」分支与主机状态行的决定。控件调用、ReplyTail 判断仍归 AppShell
module ConversationSurface =

    /// unanswered：ReplyTail.isUnanswered 的结果（调用方算）
    let decide (messagesEmpty: bool) (streaming: bool) (hasRunError: bool) (hasPartial: bool) (generating: bool) (unanswered: bool) (authenticated: bool) (hasGenerationId: bool) =
        let showEmpty = messagesEmpty && not streaming && not hasRunError
        { showEmpty = showEmpty
          tailError =
            (if showEmpty then TailError.None
             elif hasRunError then TailError.RunError
             elif not generating && not streaming && not hasPartial && unanswered then TailError.Unanswered
             else TailError.None)
          generatingText = (if not showEmpty && generating then "生成中" else "")
          composerEnabled = authenticated
          composerReason = (if authenticated then "" else "连接已断开，重新连接后可继续发送。")
          canStop = authenticated && hasGenerationId }

    /// 侧栏底部状态行（只在已认证时写）
    let hostStatusLine (configIssue: bool) = "已连接" + (if configIssue then " · 配置有误" else "")
