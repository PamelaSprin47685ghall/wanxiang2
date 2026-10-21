module StreamableHttpTests

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Text.Json.Nodes
open System.Threading
open Xunit
open Wanxiang.Server

[<Fact>]
let ``SSE body yields the last frame that has an id`` () =
    let body =
        "data: {\"id\":1,\"r\":\"a\"}\n\ndata: {\"method\":\"progress\"}\n\ndata: {\"id\":2,\"r\":\"b\"}\n\ndata: oops\n"

    Assert.Equal("""{"id":2,"r":"b"}""", (StreamableHttpSession.PickResponse body).Value.ToJsonString())
    Assert.True((StreamableHttpSession.PickResponse "data: {\"method\":\"x\"}\n").IsNone)

let serve (handler: HttpListenerContext -> unit) =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let port = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    let url = sprintf "http://127.0.0.1:%d/" port
    let listener = new HttpListener()
    listener.Prefixes.Add url
    listener.Start()

    Threading.Tasks.Task.Run(fun () ->
        try
            (while true do
                handler (listener.GetContext()))
        with _ ->
            ())
    |> ignore

    listener, url

let reply (ctx: HttpListenerContext) (status: int) (ctype: string) (body: string) =
    ctx.Response.StatusCode <- status
    ctx.Response.ContentType <- ctype
    let b = Encoding.UTF8.GetBytes body
    ctx.Response.OutputStream.Write(b, 0, b.Length)
    ctx.Response.Close()

[<Fact>]
let ``session id from the server is sent back; extra headers are sent`` () =
    let seen = Collections.Concurrent.ConcurrentQueue<string>()

    let listener, url =
        serve (fun ctx ->
            seen.Enqueue(
                sprintf "%s|%s" (string ctx.Request.Headers["Mcp-Session-Id"]) (string ctx.Request.Headers["X-T"])
            )

            ctx.Response.Headers["Mcp-Session-Id"] <- "s1"
            reply ctx 200 "application/json" """{"id":1,"result":{}}""")

    use s =
        new StreamableHttpSession(url, Map.ofList [ "X-T", "t" ], TimeSpan.FromSeconds 10.0)

    Assert.True(s.Post(JsonObject(), 5000, CancellationToken.None).Result |> Result.isOk)
    Assert.True(s.Post(JsonObject(), 5000, CancellationToken.None).Result |> Result.isOk)
    Assert.Equal<string list>([ "|t"; "s1|t" ], List.ofSeq seen)
    listener.Stop()

[<Fact>]
let ``failures come back as readable errors`` () =
    let mutable mode = 0

    let listener, url =
        serve (fun ctx ->
            match mode with
            | 0 -> reply ctx 500 "text/plain" "boom"
            | 1 -> reply ctx 200 "application/json" ""
            | 2 -> reply ctx 200 "text/event-stream" "data: {\"method\":\"x\"}\n\n"
            | _ -> reply ctx 200 "application/json" "{bad")

    use s = new StreamableHttpSession(url, Map.empty, TimeSpan.FromSeconds 10.0)

    let post () =
        match s.Post(JsonObject(), 5000, CancellationToken.None).Result with
        | Error e -> e
        | Ok _ -> "ok"

    Assert.Equal("MCP HTTP 500：boom", post ())
    mode <- 1
    Assert.Equal("MCP 响应为空", post ())
    mode <- 2
    Assert.Equal("MCP SSE 响应中没有可用帧", post ())
    mode <- 3
    Assert.StartsWith("MCP 响应解析失败", post ())
    listener.Stop()
