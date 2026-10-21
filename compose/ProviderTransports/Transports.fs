namespace Wanxiang.Agent

// FS3511：task {} 里异步枚举器加可变量，编译器退回动态状态机。只是略慢，不是缺陷。
#nowarn "3511"

open System
open System.Net.Http
open System.Text
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Channels
open Microsoft.Extensions.AI
open Wanxiang.Config

/// 两家原生传输共用的流程：建请求、首字节前重试、非 2xx 抛错、按 SSE 分帧把事件交给解码器。
module internal StreamPump =

    let request (endpoint: string) (headers: (string * string) list) (body: JsonObject) () =
        let r = new HttpRequestMessage(HttpMethod.Post, endpoint)
        r.Content <- new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")

        for (k, v) in headers do
            r.Headers.TryAddWithoutValidation(k, v) |> ignore

        r

    /// feed 返回 (要发出的增量, 是否停止)；抛出的异常让流以错误结束。
    let run
        (http: HttpClient)
        (maxRetries: int)
        (makeRequest: unit -> HttpRequestMessage)
        (feed: SseEvent -> ChatResponseUpdate list * bool)
        (closing: unit -> ChatResponseUpdate)
        (ct: CancellationToken)
        =
        let channel = Channel.CreateUnbounded<ChatResponseUpdate>()

        let pump =
            task {
                try
                    use! response = ProviderSse.sendWithRetry http makeRequest maxRetries ct

                    if not response.IsSuccessStatusCode then
                        let! text = response.Content.ReadAsStringAsync ct
                        ProviderSse.raiseForStatus response text

                    use! raw = response.Content.ReadAsStreamAsync ct
                    let enumerator = (Sse.read raw ct).GetAsyncEnumerator ct

                    try
                        let mutable go = true

                        while go do
                            let! moved = enumerator.MoveNextAsync()

                            if not moved then
                                go <- false
                            else
                                let updates, stop = feed enumerator.Current

                                for u in updates do
                                    channel.Writer.TryWrite u |> ignore

                                if stop then
                                    go <- false
                    finally
                        enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult()

                    channel.Writer.TryWrite(closing ()) |> ignore
                    channel.Writer.Complete()
                with ex ->
                    channel.Writer.Complete ex
            }

        ignore pump
        channel.Reader.ReadAllAsync ct

/// Anthropic Messages API 的原生传输（思维链、prompt caching、工具入参增量只在原生协议里有）。
type AnthropicChatClient(provider: ProviderConfig, model: string, http: HttpClient, thinkingBudget: int) =

    let stream (messages: ChatMessage seq) (options: ChatOptions) (ct: CancellationToken) =
        let body =
            AnthropicRequest.build model messages options true thinkingBudget provider.promptCaching

        let decoder = AnthropicStream()

        let feed (e: SseEvent) =
            match decoder.Feed(e.name, e.data) with
            | AnthropicStep.Updates us -> us, false
            | AnthropicStep.Stopped -> [], true
            | AnthropicStep.Failed message -> raise (HttpRequestException message)

        let makeRequest =
            StreamPump.request
                (AnthropicApi.endpoint provider.baseUrl)
                (AnthropicApi.headers provider.apiKey thinkingBudget)
                body

        StreamPump.run http provider.maxRetries makeRequest feed decoder.Closing ct

    interface IChatClient with
        member _.GetStreamingResponseAsync(messages, options, ct) = stream messages options ct
        // 非流式直接聚合流式结果：两条路径共用一套解析
        member this.GetResponseAsync(messages, options, ct) =
            task {
                return! ((this :> IChatClient).GetStreamingResponseAsync(messages, options, ct)).ToChatResponseAsync ct
            }

        member this.GetService(serviceType, _) =
            if serviceType = typeof<AnthropicChatClient> then
                box this
            else
                null

    interface IDisposable with
        // HttpClient 由调用方按 provider 共享，不在这里释放
        member _.Dispose() = ()

/// Gemini generateContent 的原生传输。
type GeminiChatClient(provider: ProviderConfig, model: string, http: HttpClient, thinkingBudget: int) =

    let stream (messages: ChatMessage seq) (options: ChatOptions) (ct: CancellationToken) =
        let body = GeminiRequest.build messages options thinkingBudget
        let decoder = GeminiStream()

        let feed (e: SseEvent) =
            match decoder.Feed e.data with
            | GeminiStep.Updates us -> us, false
            | GeminiStep.Failed message -> raise (HttpRequestException message)

        let makeRequest =
            StreamPump.request (GeminiApi.endpoint provider.baseUrl model) (GeminiApi.headers provider.apiKey) body

        StreamPump.run http provider.maxRetries makeRequest feed decoder.Closing ct

    interface IChatClient with
        member _.GetStreamingResponseAsync(messages, options, ct) = stream messages options ct

        member this.GetResponseAsync(messages, options, ct) =
            task {
                return! ((this :> IChatClient).GetStreamingResponseAsync(messages, options, ct)).ToChatResponseAsync ct
            }
        // 与旧实现一致：GetService 对自身类型返回 unit 装箱值（旧行为，见 AGENTS.md LEMMA 4）
        member _.GetService(serviceType, _) =
            if serviceType = typeof<GeminiChatClient> then
                box ()
            else
                null

    interface IDisposable with
        member _.Dispose() = ()
