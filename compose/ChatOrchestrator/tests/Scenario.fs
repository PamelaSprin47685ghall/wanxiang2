module Scenario

// 新组合与旧 ChatOrchestrator.fs、GenerationContext.fs、TitleGenerator.fs 共用的真实组合场景；输出逐字节比较。
// 真实 EventStore（临时目录）+ ToolRegistry + 本机假 OpenAI 兼容服务：
// 第一条消息 → 模型调用 builtin_echo → 工具结果回传 → 模型回答 → 自动标题；
// 第二条消息 → 上游 429 → 失败结束。记录全部广播事件（线格式）、提交的消息与发给上游的请求体。
// GUID 按出现顺序编号，时间戳与耗时归一化。

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
open System.Text.Json.Nodes
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Store
open Wanxiang.Server

let private freePort () =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let p = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    p

let private sse (chunks: string list) =
    (chunks |> List.map (fun c -> "data: " + c + "\n\n") |> String.concat "") + "data: [DONE]\n\n"

let private chunk (delta: string) (finish: string) =
    sprintf """{"id":"c","object":"chat.completion.chunk","created":1,"model":"m1","choices":[{"index":0,"delta":%s,"finish_reason":%s}]}""" delta finish

let private usage p c =
    sprintf """{"id":"c","object":"chat.completion.chunk","created":1,"model":"m1","choices":[],"usage":{"prompt_tokens":%d,"completion_tokens":%d,"total_tokens":%d}}""" p c (p + c)

let private replies =
    [ 200, "text/event-stream",
      sse [ chunk """{"role":"assistant","tool_calls":[{"index":0,"id":"call1","type":"function","function":{"name":"builtin_echo","arguments":"{\"text\":\"回声\"}"}}]}""" "\"tool_calls\""
            usage 10 3 ]
      200, "text/event-stream",
      sse [ chunk """{"role":"assistant","content":"工具说"}""" "null"
            chunk """{"content":"：回声"}""" "\"stop\""
            usage 20 4 ]
      200, "application/json",
      """{"id":"x","object":"chat.completion","created":1,"model":"m1","choices":[{"index":0,"message":{"role":"assistant","content":"「回声测试」。"},"finish_reason":"stop"}]}"""
      429, "application/json", """{"error":{"message":"slow down"}}""" ]

let private normalize (text: string) =
    let ids = Collections.Generic.Dictionary<string, int>()
    let s =
        Regex.Replace(text, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", fun m ->
            let k = m.Value.ToLowerInvariant()
            if not (ids.ContainsKey k) then ids[k] <- ids.Count + 1
            sprintf "<g%d>" ids[k])
    let s = Regex.Replace(s, "\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}(\\.\\d+)?(Z|[+-]\\d{2}:\\d{2})", "<t>")
    let s = Regex.Replace(s, "\"durationMs\":\\d+", "\"durationMs\":<ms>")
    Regex.Replace(s, "\"commandId\":\"[0-9a-f]{64}\"", "\"commandId\":<cid>")

let private waitUntil (cond: unit -> bool) =
    let sw = Diagnostics.Stopwatch.StartNew()
    while not (cond ()) && sw.Elapsed < TimeSpan.FromSeconds 20.0 do Thread.Sleep 20

let all () =
    let dir = Path.Combine(Path.GetTempPath(), "wx-orch-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    let port = freePort ()
    let listener = new HttpListener()
    listener.Prefixes.Add(sprintf "http://127.0.0.1:%d/" port)
    listener.Start()
    let bodies = ResizeArray<string>()
    let server =
        Task.Run(fun () ->
            for (status, ctype, body) in replies do
                let ctx = listener.GetContext()
                use r = new StreamReader(ctx.Request.InputStream)
                lock bodies (fun () -> bodies.Add(r.ReadToEnd()))
                ctx.Response.StatusCode <- status
                ctx.Response.ContentType <- ctype
                let bytes = Encoding.UTF8.GetBytes(body: string)
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length)
                ctx.Response.Close())
    let provider =
        { id = "p"; kind = "openai"; label = "假服务"; baseUrl = sprintf "http://127.0.0.1:%d/v1" port; apiKey = Some "sk"
          models = [ "m1" ]; defaultModel = "m1"; timeoutSeconds = 30; maxRetries = 0; enabled = true; promptCaching = false
          headers = Map.empty; extraJson = None; visionOverrides = Map.empty }
    let cfg = { AppConfig.defaults Guid.Empty with providers = Map [ "p", provider ] }
    let events = ResizeArray<string>()
    let store = new EventStore(dir)
    let registry = ToolRegistry((fun () -> cfg), ignore)
    let logs = ResizeArray<string>()
    let orch =
        ChatOrchestrator(store, (fun () -> store.Projection), (fun _ ev -> lock events (fun () -> events.Add(WireCodec.encode ev))),
                         registry, (fun () -> cfg), (fun _ -> None), (fun m -> lock logs (fun () -> logs.Add m)))
    let convId = Guid.Parse "aaaaaaaa-0000-0000-0000-000000000001"
    let session = { SessionConfig.empty with provider = "p"; model = "m1"; tools = [ "builtin:echo" ] }
    store.SubmitEvents [ ConversationCreated { conversationId = convId; title = "新对话"; config = session } ] |> ignore
    let send (inv: string) (text: string) =
        let msg = JsonNode.Parse(sprintf """{"role":"user","contents":[{"text":"%s"}]}""" text)
        orch.HandleSendUserMessage(SendUserMessage {| invocationId = Guid.Parse inv; conversationId = convId; messageJson = msg |})
    let finishedCount () = lock events (fun () -> events |> Seq.filter (fun e -> e.Contains "generation.finished") |> Seq.length)
    let title () = (Projection.tryConversation store.Projection convId).Value.title
    send "bbbbbbbb-0000-0000-0000-000000000001" "请回声"
    waitUntil (fun () -> finishedCount () = 1 && title () <> "新对话")
    send "bbbbbbbb-0000-0000-0000-000000000002" "再来"
    waitUntil (fun () -> finishedCount () = 2)
    server.Wait(TimeSpan.FromSeconds 20.0) |> ignore
    listener.Stop()
    let b = StringBuilder()
    let line (s: string) = b.AppendLine s |> ignore
    line ("title " + title ())
    line (sprintf "state %s runtimes %d" (orch.RuntimeStateOf convId) orch.RuntimeCount)
    for e in lock events (fun () -> List.ofSeq events) do line ("event " + e)
    let proj = store.Projection
    for m in Projection.effectiveMessages proj (Projection.tryConversation proj convId).Value do
        line ("message " + m.payloadJson.ToJsonString())
    for body in bodies do line ("request " + body)
    for l in logs do line ("log " + l)
    (store :> IDisposable).Dispose()
    normalize (b.ToString())
