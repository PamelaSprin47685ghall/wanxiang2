namespace Wanxiang.UI

open System
open System.Text.Json.Nodes

/// 审批卡上的文案与参数展示（旧 ToolApprovalCard 模块里的纯函数）。
module ApprovalCardText =

    /// 参数给人看：主机下发的 JSON 可能把中文转义成 \uXXXX（视觉走查 11 号截图），
    /// 这里解析后用宽松编码重写，中文原样显示；解析失败就原样返回。
    let displayArguments (json: string) : string =
        if String.IsNullOrWhiteSpace json then
            json
        else
            try
                match JsonNode.Parse json with
                | null -> json
                | node ->
                    let options =
                        System.Text.Json.JsonSerializerOptions(
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        )

                    node.ToJsonString options
            with _ ->
                json

    let argsToggleText (expanded: bool) = if expanded then "收起参数" else "查看参数"

    /// 审批卡上给用户看的策略说明：批准会产生什么后续效果要说清楚。
    let policyNote (policy: string) : string =
        match policy with
        | "first" -> "批准后，这个对话里再用同一个工具不会再问。"
        | _ -> "每次调用这个工具都会询问。"

    /// 状态文案：用户点了按钮、等主机确认期间。
    let sentText (approved: bool) =
        if approved then "已批准，正在继续…" else "已拒绝，正在通知模型…"

    /// 「已处理」说明（晚到的决定，或服务重启后失效的审批）。
    let alreadyHandledText (status: string) : string =
        match status with
        | "approved" -> "这个审批已经被批准。"
        | "denied" -> "这个审批已经被拒绝。"
        | "expired" -> "这个审批已超时或已失效。"
        | "cancelled" -> "这次生成已取消，审批作废。"
        | _ -> "这个审批已经处理过了。"

    /// 别的对话（不是正在看的）来了审批请求时的提示（迁自旧 AppShell 的 ToolApprovalRequested 分支）。
    let otherConversationWaitingText = "另一个对话里有工具调用在等你批准。"
