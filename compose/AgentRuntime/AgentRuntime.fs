namespace Wanxiang.Agent

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Microsoft.Agents.AI
open Microsoft.Extensions.AI
open Wanxiang.Config
open Wanxiang.Core

/// 单次生成调用（一次 Provider 往返）。
type AgentCallResult =
    | Completed of usage: UsageDetails option
    | Failed of GenerationError
    | Cancelled

/// Agent 运行时：构建与执行。
/// 每个对话一个运行时 agent + session；消息历史由应用托管（投影），
/// 不依赖 Provider 侧的对话状态（决策 20）。
///
/// 模型来自**对话配置**（`SessionConfig.model`），provider 只提供端点与凭据；
/// provider 的 `defaultModel` 仅在对话未指定或指定了列表外模型时兜底。
/// `thinkingBudget` 是**已解析**的有效值（对话覆盖 → [generation] 默认），
/// 因为这一层看不到全局配置。0 表示不开思维链。
type AgentRuntime(
    provider: ProviderConfig,
    session: SessionConfig,
    tools: AITool list,
    historyProvider: ChatHistoryProvider,
    thinkingBudget: int) =

    let resolvedModel = ProviderConfig.resolveModel session.model provider
    /// 每个 provider 共享一个 HttpClient（连接池复用，超时与附加请求头在此固化）。
    let http () = SharedHttpClients.Shared.Get(provider.id, provider.timeoutSeconds, provider.headers)
    let providerLabel = ProviderConfig.displayName provider

    /// OpenAI 兼容传输交给官方 SDK；两个原生协议是本项目自己实现的
    /// `IChatClient`，共用 ProviderHttp 的连接池与请求头。
    let openAiChatClient () =
        let clientOptions = OpenAI.OpenAIClientOptions()
        clientOptions.Endpoint <- Uri provider.baseUrl
        clientOptions.NetworkTimeout <- TimeSpan.FromSeconds(float provider.timeoutSeconds)
        clientOptions.RetryPolicy <- System.ClientModel.Primitives.ClientRetryPolicy(provider.maxRetries)
        clientOptions.Transport <- new System.ClientModel.Primitives.HttpClientPipelineTransport(http ())
        let credential = System.ClientModel.ApiKeyCredential(provider.apiKey |> Option.defaultValue "unused")
        let client = new OpenAI.OpenAIClient(credential, clientOptions)
        client.GetChatClient(resolvedModel).AsIChatClient()

    let chatClient: IChatClient =
        match provider.kind with
        | "anthropic" -> new AnthropicChatClient(provider, resolvedModel, http (), thinkingBudget)
        | "gemini" -> new GeminiChatClient(provider, resolvedModel, http (), thinkingBudget)
        | _ -> openAiChatClient ()

    let agentOptions = ChatClientAgentOptions()
    /// 最近一次失败调用带回的限流信号（决策 241）；成功调用会清空。
    let mutable lastRateLimit: ProviderRateLimit option = None

    do
        agentOptions.Name <- provider.id
        let chatOptions = ChatOptions()
        chatOptions.ModelId <- resolvedModel
        chatOptions.Instructions <- session.instructions |> Option.defaultValue null
        chatOptions.Tools <- ResizeArray<AITool>(tools)
        chatOptions.Temperature <- (match session.temperature with Some t -> Nullable(float32 t) | None -> Nullable())
        chatOptions.TopP <- (match session.topP with Some p -> Nullable(float32 p) | None -> Nullable())
        chatOptions.MaxOutputTokens <- (match session.maxTokens with Some m -> Nullable m | None -> Nullable())
        // 对话 extraJson 原样透传：模型专属参数（reasoning_effort、enable_thinking…）
        // 不需要万象逐个建模即可使用。
        match session.extraJson with
        | Some node when not (isNull node) ->
            match node with
            | :? System.Text.Json.Nodes.JsonObject as o ->
                let bag = Dictionary<string, obj>()
                for KeyValue(key, value) in o do
                    if not (isNull value) then bag[key] <- box (value.DeepClone())
                if bag.Count > 0 then chatOptions.AdditionalProperties <- AdditionalPropertiesDictionary(bag)
            | _ -> ()
        | _ -> ()
        agentOptions.ChatOptions <- chatOptions
        agentOptions.ChatHistoryProvider <- historyProvider
        // 禁用默认中间件（FunctionInvokingChatClient 等）：工具循环由万象编排层控制（决策 92/93）
        agentOptions.UseProvidedChatClientAsIs <- true

    let agent = chatClient.AsAIAgent(agentOptions)

    /// 实际生效的模型（对话请求 + provider 列表解析后的结果）。
    member _.Model = resolvedModel

    /// 实际生效的 provider id。
    member _.ProviderId = provider.id

    /// 最近一次失败调用携带的限流 / 额度信号（决策 241）。
    member _.LastRateLimit : ProviderRateLimit option = lastRateLimit

    /// 创建对话并绑定 conversationId（运行时对象）。
    member _.CreateSession(conversationId: Guid) : AgentSession =
        let session = agent.CreateSessionAsync().AsTask().GetAwaiter().GetResult()
        session.StateBag.SetValue<string>(WanxiangHistoryProvider.ConversationIdKey, conversationId.ToString("D"))
        session

    /// 流式执行一次调用（上下文消息列表由编排层显式构造）。delta 通过回调转发（临时展示，不记账）。
    member _.RunStreaming(session: AgentSession, messages: ChatMessage list, onDelta: ChatMessage -> unit, ct: CancellationToken) : Task<AgentCallResult> =
        task {
            try
                let updates = agent.RunStreamingAsync(messages, session, cancellationToken = ct)
                let mutable deltaMsg: ChatMessage = null
                let collected = ResizeArray<AgentResponseUpdate>()
                let enumerator = updates.GetAsyncEnumerator(ct)
                let mutable running = true
                while running do
                    let! hasNext = enumerator.MoveNextAsync()
                    if hasNext then
                        let update = enumerator.Current
                        collected.Add(update)
                        if update.Role.HasValue && update.Role.Value = ChatRole.Assistant then
                            // 正文、思维链与工具调用都转发（决策 18 透明映射）：
                            // 工具调用在流里就可见，UI 不必等整轮提交才显示「正在调用工具」。
                            let pieces =
                                update.Contents
                                |> Seq.choose (fun c ->
                                    match c with
                                    | :? TextContent as t when not (String.IsNullOrEmpty t.Text) ->
                                        Some(TextContent(t.Text) :> AIContent)
                                    | :? TextReasoningContent as r when not (String.IsNullOrEmpty r.Text) ->
                                        Some(TextReasoningContent(r.Text) :> AIContent)
                                    | :? FunctionCallContent as f -> Some(f :> AIContent)
                                    | _ -> None)
                                |> List.ofSeq
                            if not pieces.IsEmpty then
                                if isNull deltaMsg then
                                    deltaMsg <- ChatMessage()
                                    deltaMsg.Role <- ChatRole.Assistant
                                for p in pieces do deltaMsg.Contents.Add p
                                onDelta deltaMsg
                    else
                        running <- false
                let usage =
                    try
                        let response = AgentResponseExtensions.ToAgentResponse(collected)
                        if isNull response || isNull response.Usage then None else Some response.Usage
                    with _ ->
                        None
                lastRateLimit <- None
                return Completed usage
            with
            | :? OperationCanceledException when ct.IsCancellationRequested -> return Cancelled
            | ex ->
                lastRateLimit <- ProviderFailure.rateLimitOf ex
                return Failed(ProviderFailure.classify providerLabel resolvedModel ex)
        }

    /// 非流式执行一次调用（工具结果回传、标题生成等）。
    member _.Run(session: AgentSession, message: ChatMessage, ct: CancellationToken) : Task<AgentCallResult> =
        task {
            try
                let! agentResponse = agent.RunAsync(message, session, cancellationToken = ct)
                let usage = if isNull agentResponse || isNull agentResponse.Usage then None else Some agentResponse.Usage
                lastRateLimit <- None
                return Completed usage
            with
            | :? OperationCanceledException when ct.IsCancellationRequested -> return Cancelled
            | ex ->
                lastRateLimit <- ProviderFailure.rateLimitOf ex
                return Failed(ProviderFailure.classify providerLabel resolvedModel ex)
        }

    /// 一次性补全，不经 AgentSession、不写历史（标题生成等旁路用途）。
    member _.CompleteOnce(messages: ChatMessage list, maxTokens: int, ct: CancellationToken) : Task<Result<string, GenerationError>> =
        task {
            try
                let options = ChatOptions()
                options.ModelId <- resolvedModel
                options.MaxOutputTokens <- Nullable maxTokens
                options.Temperature <- Nullable 0.3f
                let! response = chatClient.GetResponseAsync(messages, options, ct)
                let text = if isNull response then "" else response.Text
                return Ok(if isNull text then "" else text.Trim())
            with
            | :? OperationCanceledException when ct.IsCancellationRequested ->
                return Error(GenerationError.create ProviderTimeout "标题生成被取消。")
            | ex -> return Error(ProviderFailure.classify providerLabel resolvedModel ex)
        }
