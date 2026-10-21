namespace Wanxiang.Agent

open System
open System.Collections.Generic
open System.Text.Json.Nodes
open Microsoft.Extensions.AI

/// Gemini generateContent 流式端点和请求头。
module GeminiApi =

    /// baseUrl 没带版本段就补 `/v1beta`；带了就照用（便于指向代理或自建网关）。
    let endpoint (baseUrl: string) (model: string) : string =
        let trimmed = baseUrl.TrimEnd '/'

        let root =
            if trimmed.Contains "/v1" then
                trimmed
            else
                trimmed + "/v1beta"

        $"{root}/models/{model}:streamGenerateContent?alt=sse"

    /// 密钥走请求头而不是 ?key=：URL 会进日志和错误详情，密钥不该出现在那里。
    let headers (apiKey: string option) : (string * string) list =
        match apiKey with
        | Some key when not (String.IsNullOrWhiteSpace key) -> [ "x-goog-api-key", key ]
        | _ -> []

/// 一次喂一个流事件后的结果。
[<RequireQualifiedAccess>]
type GeminiStep =
    | Updates of ChatResponseUpdate list
    /// 上游把错误放在 200 的流里时，body 里会有 error 对象
    | Failed of string

/// 一次 Gemini 流式响应的解码器。data 不是 JSON 时 Feed 抛 JsonException（与旧实现一致：坏流应当报错，不能静默吞掉）。
type GeminiStream() =
    let mutable promptTokens = 0
    let mutable outputTokens = 0
    let mutable totalTokens = 0
    let mutable finish = ""
    let mutable callIndex = 0

    let property (node: JsonNode) (key: string) : JsonNode option =
        match node with
        | :? JsonObject as o ->
            (match o[key] with
             | null -> None
             | v -> Some v)
        | _ -> None

    let str node key =
        property node key
        |> Option.bind (fun v ->
            try
                (if v.GetValueKind() = System.Text.Json.JsonValueKind.String then
                     Some(v.GetValue<string>())
                 else
                     None)
            with _ ->
                None)

    let int' node key =
        property node key
        |> Option.bind (fun v ->
            try
                if v.GetValueKind() <> System.Text.Json.JsonValueKind.Number then
                    None
                else
                    let mutable i = 0

                    if v.AsValue().TryGetValue<int>(&i) then
                        Some i
                    else
                        let mutable l = 0L

                        if v.AsValue().TryGetValue<int64>(&l) then
                            Some(int l)
                        else
                            None
            with _ ->
                None)

    let bool' node key =
        property node key
        |> Option.bind (fun v ->
            try
                Some(v.GetValue<bool>())
            with _ ->
                None)

    let obj node key =
        match property node key with
        | Some(:? JsonObject as o) -> Some(o :> JsonNode)
        | _ -> None

    let arr node key =
        match property node key with
        | Some(:? JsonArray as a) -> Some a
        | _ -> None

    let update (contents: AIContent list) =
        ChatResponseUpdate(Nullable ChatRole.Assistant, List.toArray contents)

    member _.Feed(data: string) : GeminiStep =
        match JsonNode.Parse data with
        | null -> GeminiStep.Updates []
        | node ->
            match obj node "error" with
            | Some error -> GeminiStep.Failed(str error "message" |> Option.defaultValue "gemini stream error")
            | None ->
                match obj node "usageMetadata" with
                | Some usage ->
                    promptTokens <- int' usage "promptTokenCount" |> Option.defaultValue promptTokens
                    outputTokens <- int' usage "candidatesTokenCount" |> Option.defaultValue outputTokens
                    totalTokens <- int' usage "totalTokenCount" |> Option.defaultValue totalTokens
                | None -> ()

                let out = ResizeArray<ChatResponseUpdate>()

                match arr node "candidates" with
                | Some candidates ->
                    for candidate in candidates do
                        if not (isNull candidate) then
                            finish <- str candidate "finishReason" |> Option.defaultValue finish

                            match obj candidate "content" |> Option.bind (fun c -> arr c "parts") with
                            | Some parts ->
                                for part in parts do
                                    if not (isNull part) then
                                        match str part "text" with
                                        | Some text when text.Length > 0 ->
                                            // thought=true 的片段是思考内容，与正式回答分开呈现
                                            if bool' part "thought" = Some true then
                                                out.Add(update [ TextReasoningContent text :> AIContent ])
                                            else
                                                out.Add(update [ TextContent text :> AIContent ])
                                        | _ -> ()

                                        match obj part "functionCall" with
                                        | Some call ->
                                            let name = str call "name" |> Option.defaultValue ""
                                            let arguments = Dictionary<string, obj>()

                                            match obj call "args" with
                                            | Some(:? JsonObject as args) ->
                                                for KeyValue(key, value) in args do
                                                    arguments[key] <- box value
                                            | _ -> ()
                                            // Gemini 不给调用 id；用「名字@序号」造一个，回传结果时再取回名字
                                            let callId = GeminiRequest.makeCallId name callIndex
                                            callIndex <- callIndex + 1

                                            out.Add(
                                                update [ FunctionCallContent(callId, name, arguments) :> AIContent ]
                                            )
                                        | None -> ()
                            | None -> ()
                | None -> ()

                GeminiStep.Updates(List.ofSeq out)

    member _.Closing() : ChatResponseUpdate =
        let usage = UsageDetails()
        usage.InputTokenCount <- Nullable(int64 promptTokens)
        usage.OutputTokenCount <- Nullable(int64 outputTokens)

        usage.TotalTokenCount <-
            Nullable(
                int64 (
                    if totalTokens > 0 then
                        totalTokens
                    else
                        promptTokens + outputTokens
                )
            )

        let closing =
            ChatResponseUpdate(Nullable ChatRole.Assistant, [| UsageContent usage :> AIContent |])

        if not (String.IsNullOrEmpty finish) then
            closing.FinishReason <- Nullable(ChatFinishReason finish)

        closing
