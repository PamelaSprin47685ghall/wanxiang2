namespace Wanxiang.UI

/// 认证成功（auth.accepted）时壳层要做的事，按旧 AppShell.HandleEvent 的 AuthAccepted 分支的顺序列出。
/// 宿主逐项执行；这里只决定做哪些。
[<RequireQualifiedAccess>]
type InstanceStep =
    // —— 换了一台主机（旧实例非空且不同）才做 ——
    /// 导出对话框开着就关掉（它属于旧主机）。
    | CloseExportDialog
    /// 把输入框文字存回旧主机当前对话的草稿。
    | SaveComposerDraft
    /// 旧主机的未确认消息转为断开状态。
    | DisconnectOutbox of previousInstance: string
    | ResetClientState
    | ClearRuns
    | ClearCatalog
    | ResetHostHealth
    | ClearActiveConversation
    | ClearConversationList
    /// 输入框换成新主机「新对话」的草稿。
    | LoadNewInstanceDraft
    // —— 每次认证成功都做 ——
    | ResetHostFeatures
    | AnnounceApprovalCapability
    | ClearConnectionIssue
    | ResetReconnectDelay
    /// 上一代连接残留的失败卡片已过期：清掉列表里各对话与当前对话的错误。
    | ClearRunErrors
    /// 导出对话框开着时不关（导出跨重连继续）；其余对话框关掉。
    | CloseDialogs
    | RestoreLocalDrafts
    | RefreshDevicesIfShown
    /// 浏览器：有令牌时保存这条连接（Q191）。
    | SaveBrowserConnection
    | RequestCatalog
    | ObserveConversationList
    /// 有当前对话时重新订阅它。
    | ObserveActiveConversation

module InstanceSwitch =

    /// 换主机：之前连过一台（非空）且和这次的不是同一台。
    let isSwitch (previousInstance: string) (newInstance: string) =
        previousInstance <> "" && previousInstance <> newInstance

    let plan (previousInstance: string) (newInstance: string) (exportDialogOpen: bool) (isBrowser: bool) (hasToken: bool) (hasActiveConversation: bool) : InstanceStep list =
        [ if isSwitch previousInstance newInstance then
              if exportDialogOpen then InstanceStep.CloseExportDialog
              InstanceStep.SaveComposerDraft
              InstanceStep.DisconnectOutbox previousInstance
              InstanceStep.ResetClientState
              InstanceStep.ClearRuns
              InstanceStep.ClearCatalog
              InstanceStep.ResetHostHealth
              InstanceStep.ClearActiveConversation
              InstanceStep.ClearConversationList
              InstanceStep.LoadNewInstanceDraft
          InstanceStep.ResetHostFeatures
          InstanceStep.AnnounceApprovalCapability
          InstanceStep.ClearConnectionIssue
          InstanceStep.ResetReconnectDelay
          InstanceStep.ClearRunErrors
          // 换主机时导出框已在上面关掉
          if not (exportDialogOpen && not (isSwitch previousInstance newInstance)) then InstanceStep.CloseDialogs
          InstanceStep.RestoreLocalDrafts
          InstanceStep.RefreshDevicesIfShown
          if isBrowser && hasToken then InstanceStep.SaveBrowserConnection
          InstanceStep.RequestCatalog
          InstanceStep.ObserveConversationList
          if hasActiveConversation then InstanceStep.ObserveActiveConversation ]
