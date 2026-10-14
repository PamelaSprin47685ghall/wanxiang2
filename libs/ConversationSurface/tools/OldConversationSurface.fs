namespace Wanxiang.UI

// 照抄旧 AppShell.fs Render 已打开对话分支（第 407–434 行）与 UpdateHostStatusLine；控件调用换成记录字段
[<RequireQualifiedAccess>]
type TailError = | None | RunError | Unanswered

type ConversationSurfaceView =
    { showEmpty: bool; tailError: TailError; generatingText: string; composerEnabled: bool; composerReason: string; canStop: bool }

module ConversationSurface =
    let decide (messagesEmpty: bool) (streamingSome: bool) (runErrorSome: bool) (partialSome: bool) (generating: bool) (unanswered: bool) (authenticated: bool) (generationIdSome: bool) =
        let mutable v = { showEmpty = false; tailError = TailError.None; generatingText = ""; composerEnabled = false; composerReason = ""; canStop = false }
        if messagesEmpty && not streamingSome && not runErrorSome then
            v <- { v with showEmpty = true }
        else
            v <- { v with generatingText = (if generating then "生成中" else "") }
            v <- { v with
                    tailError =
                        (match runErrorSome with
                         | true -> TailError.RunError
                         | false when not generating && not streamingSome && not partialSome && unanswered -> TailError.Unanswered
                         | false -> TailError.None) }
        v <- { v with composerEnabled = authenticated; composerReason = (if authenticated then "" else "连接已断开，重新连接后可继续发送。") }
        { v with canStop = authenticated && generationIdSome }

    let hostStatusLine (configIssue: bool) =
        let suffix = if configIssue then " · 配置有误" else ""
        "已连接" + suffix
