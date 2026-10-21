namespace Wanxiang.UI

open System

/// 主机报来的 server.error 该落到哪里（旧 AppShell.HandleEvent 的 ServerError 分支）。
[<RequireQualifiedAccess>]
type ServerErrorRoute =
    /// 附件 blob 缺失：把该 sha256 标成缺失，消息里显示「内容已丢失」。
    | MissingAttachment of sha256: string
    /// 消息记账失败且有当前对话：进当前对话的错误卡（自带重试），不叠 toast（D9）。
    | ConversationFailure of message: string
    /// 其余：toast。
    | Toast of text: string

module ServerErrorRoute =

    /// 错误卡放不进去（正有生成占着槽位）时退回的 toast 文案，也是其余错误的 toast 文案。
    let toastText (message: string) = sprintf "主机报告了错误：%s" message

    let classify (hasActiveConversation: bool) (message: string) : ServerErrorRoute =
        if
            message.StartsWith("attachment ", StringComparison.Ordinal)
            && message.Contains "not found"
        then
            // 以 "attachment " 开头，按空格切至少两段
            ServerErrorRoute.MissingAttachment((message.Split(' ')[1]).ToLowerInvariant())
        elif message.StartsWith("消息记账失败", StringComparison.Ordinal) && hasActiveConversation then
            ServerErrorRoute.ConversationFailure message
        else
            ServerErrorRoute.Toast(toastText message)
