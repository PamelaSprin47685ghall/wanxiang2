namespace Wanxiang.UI

/// §215：生成失败的就地恢复动作。错误卡不只说「请到设置里检查」，
/// 而是直接给通往那里的按钮——提示里指了路，按钮就得能走过去。
/// 哪类错误给哪个动作（forKind）要看协议的 GenerationErrorKind，归组合者 ConversationRuns。
type ErrorRecovery =
    /// 打开设置 → 模型服务（密钥失效、模型名错）
    | OpenProviderSettings
    /// 打开本对话的设置（换模型 / 调参数）
    | OpenConversationSettings
    /// 决策 366：限流 / 额度用尽只给一件事——直接打开模型切换器
    | SwitchModel
    /// 新建对话（上下文超长）
    | StartNewConversation

module ErrorRecovery =

    let label (r: ErrorRecovery) : string =
        match r with
        | OpenProviderSettings -> "打开模型服务设置"
        | OpenConversationSettings -> "换模型或调整参数"
        | SwitchModel -> "换个模型"
        | StartNewConversation -> "新建对话"
