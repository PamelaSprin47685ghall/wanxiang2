namespace Wanxiang.UI

/// 工具调用卡的状态（用例名是泛名，限定访问）
[<RequireQualifiedAccess>]
type ToolCallState =
    | Running
    | AwaitingApproval
    | Success
    | Failed of string option
    | Unknown

module ToolCallState =

    /// 旧 MessageCard.resolveToolCallStatusWith。
    /// outcome：None = 还没有结果；Some None = 有结果且没认出错误；Some (Some e) = 认出错误 e
    /// （认错误由调用方用 ToolErrorSummary 做，本库不引用它）。
    let resolve (awaiting: Set<string>) (isStreaming: bool) (callId: string) (outcome: string option option) =
        match outcome with
        | Some(Some err) -> ToolCallState.Failed(Some err)
        | Some None -> ToolCallState.Success
        | None ->
            if isStreaming && awaiting.Contains callId then
                ToolCallState.AwaitingApproval
            elif isStreaming then
                ToolCallState.Running
            else
                ToolCallState.Unknown
