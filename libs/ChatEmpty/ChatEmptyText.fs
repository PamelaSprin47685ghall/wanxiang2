namespace Wanxiang.UI

/// 空对话的起手式：标签就是填进输入框的整句。
module ChatStarters =
    let items =
        [
            "用三句话说明这台主机现在能做什么", "用三句话说明这台主机现在能做什么"
            "指出一份 TOML 配置里的错误", "指出一份 TOML 配置里的错误"
            "把工具调用说成人话", "把工具调用说成人话"
        ]

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
        | NotConnected
        | ConnectionFailed _
        | NoProvider -> true
        | NoConversation
        | EmptyConversation -> false


module ChatEmptyText =

    /// 空态的标题与提示（旧 ChatView.ShowEmpty 里的文案表）。
    let text (state: ChatEmptyState) : string * string =
        match state with
        | NotConnected -> "先连接一台万象主机", "主机是运行万象的那台电脑：模型、密钥和对话都在它上面。"
        | ConnectionFailed reason -> "没能连上主机", sprintf "%s。请确认主机在运行、网络可达，然后重新连接。" reason
        | NoProvider -> "还没有可用的模型", "添加一个模型服务并填入密钥，就可以开始对话了。"
        | NoConversation
        | EmptyConversation -> "从一句开始", "模型、附件和发送都在下面。要找回以前的对话，用侧栏。"
