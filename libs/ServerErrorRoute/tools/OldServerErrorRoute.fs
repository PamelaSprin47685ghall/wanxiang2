namespace Wanxiang.UI

open System

// 原样摘自 AppShell.fs HandleEvent 的 `| ServerError d ->` 分支；副作用换成返回同名结果。
[<RequireQualifiedAccess>]
type ServerErrorRoute =
    | MissingAttachment of sha256: string
    | ConversationFailure of message: string
    | Toast of text: string

module ServerErrorRoute =
    let toastText (message: string) = sprintf "主机报告了错误：%s" message

    let classify (hasActiveConversation: bool) (message: string) : ServerErrorRoute =
        let d = {| message = message |}
        let mutable result = ServerErrorRoute.Toast ""
        let toast text _ = result <- ServerErrorRoute.Toast text
        if d.message.StartsWith("attachment ", StringComparison.Ordinal) && d.message.Contains "not found" then
            let parts = d.message.Split(' ')
            if parts.Length > 1 then
                result <- ServerErrorRoute.MissingAttachment(parts[1].ToLowerInvariant())
        elif d.message.StartsWith("消息记账失败", StringComparison.Ordinal) && hasActiveConversation then
            result <- ServerErrorRoute.ConversationFailure d.message
        else
            toast (sprintf "主机报告了错误：%s" d.message) ()
        result
