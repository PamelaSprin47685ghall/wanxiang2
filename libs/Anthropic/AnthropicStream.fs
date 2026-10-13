namespace Wanxiang.Agent

open System
open System.Collections.Generic
open System.Text
open System.Text.Json.Nodes
open Microsoft.Extensions.AI

/// Anthropic Messages API 的端点和请求头。
module AnthropicApi =

    /// baseUrl 可能已含 `/v1`（用户从兼容端点配置迁移过来），两种都接受。
    let endpoint (baseUrl: string) : string =
        let trimmed = baseUrl.TrimEnd '/'
        if trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) then trimmed + "/messages"
        else trimmed + "/v1/messages"

    /// 请求头。没有密钥就不发 x-api-key；开了思维链才发 beta 头。
    let headers (apiKey: string option) (thinkingBudget: int) : (string * string) list =
        [ yield "anthropic-version", AnthropicRequest.ApiVersion
          match apiKey with
          | Some key when not (String.IsNullOrWhiteSpace key) -> yield "x-api-key", key
          | _ -> ()
          if thinkingBudget > 0 then yield "anthropic-beta", "interleaved-thinking-2025-05-14" ]

/// 一次喂一个流事件后的结果。
type AnthropicStep =
    /// 要交给下游的增量（可能为空）
    | Updates of ChatResponseUpdate list
    /// 收到 message_stop，流已结束
    | Stopped
    /// 流里的 error 事件，带上游给的说明
    | Failed of string

/// 一次 Anthropic 流式响应的解码器：按顺序喂入（事件名, data），得到增量；最后取收尾更新（用量、结束原因）。
/// 不碰网络，所以可以用录下来的流逐字节回放测试。
type AnthropicStream() =
    // 工具入参被切成 input_json_delta 片段，等 content_block_stop 拼完整才能解析
    let tools = Dictionary<int, string * string * StringBuilder>()
    let mutable responseId = ""
    let mutable inputTokens = 0
    let mutable outputTokens = 0
    let mutable finish = ""

    let property (node: JsonNode) (key: string) : JsonNode option =
        match node with
        | :? JsonObject as o -> (match o[key] with null -> None | v -> Some v)
        | _ -> None
    let str node key =
        property node key |> Option.bind (fun v -> try (if v.GetValueKind() = System.Text.Json.JsonValueKind.String then Some(v.GetValue<string>()) else None) with _ -> None)
    let int' node key =
        property node key
        |> Option.bind (fun v ->
            try
                if v.GetValueKind() <> System.Text.Json.JsonValueKind.Number then None
                else
                    let mutable i = 0
                    if v.AsValue().TryGetValue<int>(&i) then Some i
                    else
                        let mutable l = 0L
                        if v.AsValue().TryGetValue<int64>(&l) then Some(int l) else None
            with _ -> None)
    let obj node key = match property node key with Some(:? JsonObject as o) -> Some(o :> JsonNode) | _ -> None

    let update (contents: AIContent list) =
        let u = ChatResponseUpdate(Nullable ChatRole.Assistant, List.toArray contents)
        if not (String.IsNullOrEmpty responseId) then u.ResponseId <- responseId
        u

    member _.ResponseId = responseId

    /// 喂一个事件。data 不是 JSON 时忽略（与旧实现一致）。
    member _.Feed(eventName: string, data: string) : AnthropicStep =
        let parsed =
            try (if String.IsNullOrWhiteSpace data then None else Some(JsonNode.Parse data)) with _ -> None
        match parsed with
        | None | Some null -> Updates []
        | Some node ->
            match str node "type" |> Option.defaultValue eventName with
            | "message_start" ->
                match obj node "message" with
                | Some message ->
                    responseId <- str message "id" |> Option.defaultValue ""
                    match obj message "usage" with
                    | Some usage ->
                        inputTokens <- int' usage "input_tokens" |> Option.defaultValue 0
                        outputTokens <- int' usage "output_tokens" |> Option.defaultValue 0
                    | None -> ()
                | None -> ()
                Updates []
            | "content_block_start" ->
                let index = int' node "index" |> Option.defaultValue 0
                match obj node "content_block" with
                | Some block when str block "type" = Some "tool_use" ->
                    tools[index] <- (str block "id" |> Option.defaultValue "", str block "name" |> Option.defaultValue "", StringBuilder())
                | _ -> ()
                Updates []
            | "content_block_delta" ->
                let index = int' node "index" |> Option.defaultValue 0
                match obj node "delta" with
                | Some delta ->
                    match str delta "type" with
                    | Some "text_delta" ->
                        match str delta "text" with
                        | Some text when text.Length > 0 -> Updates [ update [ TextContent text :> AIContent ] ]
                        | _ -> Updates []
                    // 思维链走独立的内容类型：界面单独折叠展示，也不并入会话摘要预览
                    | Some "thinking_delta" ->
                        match str delta "thinking" with
                        | Some text when text.Length > 0 ->
                            Updates [ update [ TextReasoningContent text :> AIContent
                                               TextContent(text, RawRepresentation = box "thinking") :> AIContent ] ]
                        | _ -> Updates []
                    | Some "input_json_delta" ->
                        match tools.TryGetValue index, str delta "partial_json" with
                        | (true, (_, _, buffer)), Some fragment -> buffer.Append fragment |> ignore
                        | _ -> ()
                        Updates []
                    | _ -> Updates []
                | None -> Updates []
            | "content_block_stop" ->
                let index = int' node "index" |> Option.defaultValue 0
                match tools.TryGetValue index with
                | true, (id, name, buffer) ->
                    tools.Remove index |> ignore
                    let arguments = Dictionary<string, obj>()
                    let text = buffer.ToString()
                    let mutable parsedOk = true
                    if not (String.IsNullOrWhiteSpace text) then
                        try
                            match JsonNode.Parse text with
                            | :? JsonObject as parsed -> for KeyValue(key, value) in parsed do arguments[key] <- box value
                            | _ -> parsedOk <- false
                        with _ -> parsedOk <- false
                    // 入参拼不成 JSON 对象时丢弃这次调用，而不是用残缺参数去执行工具
                    if parsedOk then Updates [ update [ FunctionCallContent(id, name, arguments) :> AIContent ] ] else Updates []
                | _ -> Updates []
            | "message_delta" ->
                match obj node "delta" with
                | Some delta -> finish <- str delta "stop_reason" |> Option.defaultValue finish
                | None -> ()
                match obj node "usage" with
                | Some usage -> outputTokens <- int' usage "output_tokens" |> Option.defaultValue outputTokens
                | None -> ()
                Updates []
            | "error" ->
                Failed(obj node "error" |> Option.bind (fun e -> str e "message") |> Option.defaultValue "anthropic stream error")
            | "message_stop" -> Stopped
            | _ -> Updates []

    /// 收尾更新：用量与结束原因。编排层按它记账。
    member _.Closing() : ChatResponseUpdate =
        let usage = UsageDetails()
        usage.InputTokenCount <- Nullable(int64 inputTokens)
        usage.OutputTokenCount <- Nullable(int64 outputTokens)
        usage.TotalTokenCount <- Nullable(int64 (inputTokens + outputTokens))
        let closing = ChatResponseUpdate(Nullable ChatRole.Assistant, [| UsageContent usage :> AIContent |])
        if not (String.IsNullOrEmpty responseId) then closing.ResponseId <- responseId
        if not (String.IsNullOrEmpty finish) then
            let mapped =
                match finish with
                | "end_turn" -> ChatFinishReason.Stop
                | "max_tokens" -> ChatFinishReason.Length
                | "tool_use" -> ChatFinishReason.ToolCalls
                | other -> ChatFinishReason other
            closing.FinishReason <- Nullable mapped
        closing
