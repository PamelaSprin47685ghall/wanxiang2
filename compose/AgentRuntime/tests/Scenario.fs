module Scenario

// 新组合与旧 AgentRuntime.fs、HistoryProvider.fs 共用的场景；输出逐字节比较。
// 一个本机假 OpenAI 兼容服务按顺序回答：流式成功、流式 429、非流式 500、补全成功、连不上；记下收到的请求体。
// 另有一组直接构造的异常，覆盖 ProviderFailure.classify 的各条分支。

open System
open System.IO
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Text.Json.Nodes
open Microsoft.Extensions.AI
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Agent

let private freePort () =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let p = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    p

let private showError (e: GenerationError) =
    sprintf "%A|%s|%A|%A|%b" e.kind e.message e.detail e.retryAfterSeconds e.retryable

let private showResult (r: AgentCallResult) =
    match r with
    | AgentCallResult.Completed u ->
        sprintf
            "completed %s"
            (match u with
             | Some u -> sprintf "%A/%A/%A" u.InputTokenCount u.OutputTokenCount u.TotalTokenCount
             | None -> "-")
    | AgentCallResult.Failed e -> "failed " + showError e
    | AgentCallResult.Cancelled -> "cancelled"

let private sse (chunks: string list) =
    (chunks |> List.map (fun c -> "data: " + c + "\n\n") |> String.concat "")
    + "data: [DONE]\n\n"

let private replies =
    [
        200,
        [ "Content-Type", "text/event-stream" ],
        sse
            [
                """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"m1","choices":[{"index":0,"delta":{"role":"assistant","content":"你"},"finish_reason":null}]}"""
                """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"m1","choices":[{"index":0,"delta":{"content":"好"},"finish_reason":null}]}"""
                """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"m1","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call1","type":"function","function":{"name":"builtin_time","arguments":"{}"}}]},"finish_reason":"tool_calls"}]}"""
                """{"id":"c1","object":"chat.completion.chunk","created":1,"model":"m1","choices":[],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}"""
            ]
        429,
        [
            "Content-Type", "application/json"
            "Retry-After", "7"
            "x-ratelimit-remaining-requests", "0"
            "x-ratelimit-limit-requests", "60"
        ],
        """{"error":{"message":"slow down","type":"rate_limit"}}"""
        500, [ "Content-Type", "text/html" ], "<html><body><h1>Internal   Server\nError</h1></body></html>"
        200,
        [ "Content-Type", "application/json" ],
        """{"id":"x","object":"chat.completion","created":1,"model":"m1","choices":[{"index":0,"message":{"role":"assistant","content":"  标题  "},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}"""
        400,
        [ "Content-Type", "application/json" ],
        """{"error":{"code":"context_length_exceeded","message":"too long"}}"""
    ]

let runtime () =
    let b = StringBuilder()
    let line (s: string) = b.AppendLine s |> ignore
    let port = freePort ()
    let listener = new HttpListener()
    listener.Prefixes.Add(sprintf "http://127.0.0.1:%d/" port)
    listener.Start()
    let bodies = ResizeArray<string>()

    let server =
        Task.Run(fun () ->
            for (status, headers, body) in replies do
                let ctx = listener.GetContext()
                use r = new StreamReader(ctx.Request.InputStream)
                bodies.Add(sprintf "%s %s %s" ctx.Request.HttpMethod ctx.Request.Url.AbsolutePath (r.ReadToEnd()))
                ctx.Response.StatusCode <- status

                for (k, v) in headers do
                    if k = "Content-Type" then
                        ctx.Response.ContentType <- v
                    else
                        ctx.Response.Headers[k] <- v

                let bytes = Encoding.UTF8.GetBytes(body: string)
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length)
                ctx.Response.Close())

    let provider =
        {
            id = "p"
            kind = "openai"
            label = "假服务"
            baseUrl = sprintf "http://127.0.0.1:%d/v1" port
            apiKey = Some "sk"
            models = [ "m1"; "m2" ]
            defaultModel = "m1"
            timeoutSeconds = 30
            maxRetries = 0
            enabled = true
            promptCaching = false
            headers = Map [ "X-Extra", "1" ]
            extraJson = None
            visionOverrides = Map.empty
        }

    let session =
        { SessionConfig.empty with
            provider = "p"
            model = "nope"
            instructions = Some "做个好助手"
            temperature = Some 0.5
            topP = Some 0.9
            maxTokens = Some 100
            extraJson = Some(JsonNode.Parse """{"reasoning_effort":"low"}""")
        }

    let history = ResizeArray<string>()

    let hp =
        WanxiangHistoryProvider(
            (fun _ ->
                [
                    MessageSerde.textMessage ChatRole.User "旧的"
                    MessageSerde.textMessage ChatRole.Assistant "旧答"
                ]),
            (fun _ msgs ->
                history.Add(
                    sprintf "response %d %s" msgs.Length (msgs |> List.map MessageSerde.textOf |> String.concat "+")
                )),
            (fun _ ex -> history.Add("failure " + ex.GetType().Name))
        )

    let tool =
        AIFunctionFactory.Create(Func<string>(fun () -> "t"), "builtin_time", "时间")

    let rt = AgentRuntime(provider, session, [ tool :> AITool ], hp, 0)
    line (sprintf "model %s provider %s" rt.Model rt.ProviderId)
    let s = rt.CreateSession(Guid.Parse "11111111-1111-1111-1111-111111111111")
    let deltas = ResizeArray<string>()

    let onDelta (m: ChatMessage) =
        deltas.Add(
            m.Contents
            |> Seq.map (fun c ->
                match c with
                | :? TextContent as t -> t.Text
                | :? FunctionCallContent as f -> "call:" + f.Name
                | x -> x.GetType().Name)
            |> String.concat ","
        )

    let r1 =
        rt.RunStreaming(s, [ MessageSerde.textMessage ChatRole.User "你好吗" ], onDelta, CancellationToken.None).Result

    line (showResult r1)

    for d in deltas do
        line ("delta " + d)

    let r2 =
        rt.RunStreaming(s, [ MessageSerde.textMessage ChatRole.User "再来" ], ignore, CancellationToken.None).Result

    line (showResult r2)

    line (
        sprintf
            "rateLimit %A"
            (rt.LastRateLimit
             |> Option.map (fun l -> l.remainingRequests, l.retryAfterSeconds))
    )

    let r3 =
        rt.Run(s, MessageSerde.textMessage ChatRole.User "三", CancellationToken.None).Result

    line (showResult r3)

    let r4 =
        rt.CompleteOnce([ MessageSerde.textMessage ChatRole.User "起个标题" ], 20, CancellationToken.None).Result

    line (sprintf "%A" (r4 |> Result.mapError showError))

    let r5 =
        rt.CompleteOnce([ MessageSerde.textMessage ChatRole.User "太长" ], 20, CancellationToken.None).Result

    line (sprintf "%A" (r5 |> Result.mapError showError))
    server.Wait()
    listener.Stop()
    use cts = new CancellationTokenSource()
    cts.Cancel()
    line (showResult (rt.Run(s, MessageSerde.textMessage ChatRole.User "取消", cts.Token).Result))

    for h in history do
        line h

    for body in bodies do
        line ("request " + body)

    b.ToString()

let classify () =
    let withData (ex: exn) (k: string) (v: obj) =
        ex.Data[k] <- v
        ex

    let cases: (string * exn) list =
        [
            "timeout", TimeoutException "slow"
            "taskcancel", TaskCanceledException "gone"
            "http400", HttpRequestException("bad", null, Nullable HttpStatusCode.BadRequest)
            "http401", HttpRequestException("no", null, Nullable HttpStatusCode.Unauthorized)
            "http404", HttpRequestException("missing", null, Nullable HttpStatusCode.NotFound)
            "http408", HttpRequestException("late", null, Nullable HttpStatusCode.RequestTimeout)
            "http429",
            withData
                (HttpRequestException("limit", null, Nullable HttpStatusCode.TooManyRequests))
                "RetryAfterSeconds"
                (box 9)
            "http503", HttpRequestException("down", null, Nullable HttpStatusCode.ServiceUnavailable)
            "http418", HttpRequestException("teapot", null, Nullable(enum<HttpStatusCode> 418))
            "http400ctx", HttpRequestException("maximum context exceeded", null, Nullable HttpStatusCode.BadRequest)
            "httpnet", HttpRequestException("refused")
            "socket", SocketException 111
            "wrapped", Exception("outer", SocketException 111)
            "refusedtext", Exception("Connection refused (127.0.0.1)")
            "filter", Exception("blocked by content_filter")
            "badgateway", Exception("upstream said Bad Gateway")
            "unknown", Exception("<b>weird</b>\n\n  thing " + String('x', 700))
        ]

    let b = StringBuilder()

    for (name, ex) in cases do
        b.AppendLine(sprintf "%s %s" name (showError (ProviderFailure.classify "假服务" "m1" ex)))
        |> ignore

    let resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
    resp.Headers.TryAddWithoutValidation("Retry-After", "12") |> ignore

    b.AppendLine(
        sprintf
            "retryAfter %A %A"
            (ProviderFailure.retryAfterOfResponse resp)
            (ProviderFailure.retryAfterOfResponse null)
    )
    |> ignore

    let limited =
        Exception(
            "x",
            withData
                (Exception "inner")
                "RateLimit"
                (box
                    { ProviderRateLimit.empty with
                        remainingRequests = Some 0
                    })
        )

    b.AppendLine(
        sprintf
            "rateLimitOf %A %A"
            ((ProviderFailure.rateLimitOf limited) |> Option.map _.remainingRequests)
            (ProviderFailure.rateLimitOf (Exception "y"))
    )
    |> ignore

    b.ToString()

let all () = classify () + runtime ()
