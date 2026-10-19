namespace Wanxiang.UI

/// 没有打开对话时，聊天区显示什么。
[<RequireQualifiedAccess>]
type IdleEmpty =
    /// 未连接；primary 为空态按钮文案
    | NotConnected
    /// 连接失败，附错误文案
    | ConnectionFailed of error: string
    /// 首次运行：内嵌「连接一个模型」卡片
    | Onboarding
    /// 可以开始新对话
    | NoConversation

/// 没有打开对话时的界面决定。
type IdleSurfaceView =
    { empty: IdleEmpty
      /// 空态主按钮文案；None 不出按钮
      primary: string option
      composerEnabled: bool
      /// 输入框禁用时的说明（启用时为空串）
      composerReason: string
      /// 模型标签；None 表示按当前选择的模型描述（由调用方查目录）
      modelLabel: string option }

/// 旧 AppShell.fs 刷新界面时「没有打开对话」分支的决定（MainView.Refresh）。
module IdleSurface =

    /// rePair：当前连接错误的重试动作是「重新配对」（令牌失效）。
    let decide (authenticated: bool) (connectionError: string option) (rePair: bool) (hasSavedCredentials: bool)
               (catalogReady: bool) (onboardingBusy: bool) : IdleSurfaceView =
        if not authenticated then
            match connectionError with
            | Some err ->
                // 令牌失效时入口直接叫“重新配对”，而不是泛泛的“重新连接”
                { empty = IdleEmpty.ConnectionFailed err
                  primary = Some(if rePair then "重新配对" else "重新连接")
                  composerEnabled = false
                  composerReason = sprintf "连接失败：%s" err
                  modelLabel = Some "未连接" }
            | None ->
                if not hasSavedCredentials then
                    { empty = IdleEmpty.NotConnected; primary = Some "连接主机"; composerEnabled = false
                      composerReason = "可以先写草稿，连上主机后再发送。"; modelLabel = Some "未连接" }
                else
                    { empty = IdleEmpty.NotConnected; primary = Some "连接主机"; composerEnabled = false
                      composerReason = "正在连接主机…草稿已保留。"; modelLabel = Some "正在连接" }
        elif not catalogReady || onboardingBusy then
            // 首次运行：卡片就在聊天界面里；连接进行中即使目录暂时就绪也保留卡片
            { empty = IdleEmpty.Onboarding; primary = None; composerEnabled = false
              composerReason = "先在上方添加一个模型服务，就能在这里输入。"; modelLabel = Some "未配置模型" }
        else
            { empty = IdleEmpty.NoConversation; primary = None; composerEnabled = true; composerReason = ""; modelLabel = None }

    /// 打开的对话还在加载时的输入框说明。
    [<Literal>]
    let LoadingReason = "正在加载对话…"

    /// 打开的对话已加载时，输入框是否可用及说明。
    let openComposer (authenticated: bool) : bool * string =
        authenticated, (if authenticated then "" else "连接已断开，重新连接后可继续发送。")
