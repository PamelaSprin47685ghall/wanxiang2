namespace Wanxiang.UI

// 照抄旧 Views/MessageCard.fs 的 resolveToolCallStatusWith；
// tryExtractToolError (Some res) 的结果以 outcome 传入（Some r 即 call.result = Some _）
[<RequireQualifiedAccess>]
type ToolCallState =
    | Running
    | AwaitingApproval
    | Success
    | Failed of string option
    | Unknown

module ToolCallState =
    let resolve (awaiting: Set<string>) (isStreaming: bool) (callId: string) (outcome: string option option) =
        match outcome with
        | Some res ->
            match res with
            | Some err -> ToolCallState.Failed(Some err)
            | None -> ToolCallState.Success
        | None ->
            if isStreaming && awaiting.Contains callId then ToolCallState.AwaitingApproval
            elif isStreaming then ToolCallState.Running
            else ToolCallState.Unknown
