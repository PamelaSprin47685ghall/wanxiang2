namespace Wanxiang.UI

/// 空对话的起手式：标签 → 填进输入框的开头。只给开头不给整句，用户补完再发（决策 222）。
module ChatStarters =
    let items =
        [ "解释概念", "用通俗的话解释："
          "总结要点", "帮我总结下面这段内容的要点：\n\n"
          "改写润色", "帮我改写得更清楚、更简洁：\n\n"
          "写代码", "写一段代码，实现：" ]

/// 空态在不同前提下要说不同的话，否则用户不知道下一步做什么。
type ChatEmptyState =
    /// 未连接（无保存凭据）
    | NotConnected
    /// 连接或认证失败，附失败原因
    | ConnectionFailed of reason: string
    /// 已连接但没有可用模型服务
    | NoProvider
    /// 有模型服务但没选对话
    | NoConversation
    /// 对话是空的
    | EmptyConversation

module ChatEmptyState =

    /// 未连接、连接失败、没有模型服务时展示品牌标；有对话可开时展示起手式。
    let showsBrand (state: ChatEmptyState) =
        match state with
        | NotConnected | ConnectionFailed _ | NoProvider -> true
        | NoConversation | EmptyConversation -> false


module ChatEmptyText =

    /// 空态的标题与提示（旧 ChatView.ShowEmpty 里的文案表）。
    let text (state: ChatEmptyState) : string * string =
        match state with
        | NotConnected -> "先连接一台万象主机", "主机是运行万象的那台电脑：模型、密钥和对话都在它上面。"
        | ConnectionFailed reason -> "没能连上主机", sprintf "%s。请确认主机在运行、网络可达，然后重新连接。" reason
        | NoProvider -> "还没有可用的模型", "添加一个模型服务并填入密钥，就可以开始对话了。"
        | NoConversation -> "今天想讨论什么？", "发出第一条消息后，会自动建立对话。"
        | EmptyConversation -> "开始这段对话", "输入问题，或附上一份文件。"
