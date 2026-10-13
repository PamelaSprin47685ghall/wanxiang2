module Tests

open System
open System.Net
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiang.Client

/// 本机真实 WebSocket 服务：每个连接交给 onClient。
type private Server(onClient: WebSocket -> Task) =
    let port =
        let l = new Sockets.TcpListener(IPAddress.Loopback, 0)
        l.Start()
        let p = (l.LocalEndpoint :?> IPEndPoint).Port
        l.Stop()
        p
    let listener = new HttpListener()
    do
        listener.Prefixes.Add(sprintf "http://127.0.0.1:%d/ts/" port)
        listener.Start()
        Task.Run(fun () ->
            task {
                try
                    while listener.IsListening do
                        let! ctx = listener.GetContextAsync()
                        let! wsc = ctx.AcceptWebSocketAsync(null)
                        Task.Run(fun () -> onClient wsc.WebSocket) |> ignore
                with _ -> ()
            } :> Task) |> ignore
    member _.Uri = Uri(sprintf "ws://127.0.0.1:%d/ts/" port)
    interface IDisposable with
        member _.Dispose() = try listener.Close() with _ -> ()

let private send (ws: WebSocket) (s: string) =
    ws.SendAsync(ArraySegment(Encoding.UTF8.GetBytes s), WebSocketMessageType.Text, true, CancellationToken.None)

let private within (t: Task<'T>) = task {
    let! w = Task.WhenAny(t :> Task, Task.Delay 5000)
    if w <> (t :> Task) then failwith "timeout"
    return t.Result }

[<Fact>]
let ``未连接时发送返回 false，断开让代号加一`` () =
    task {
        let s = TextSocket()
        Assert.Equal(0, s.Generation)
        Assert.False(s.IsConnected)
        let! ok = s.TrySendAsync("x", None)
        Assert.False ok
        s.Disconnect()
        Assert.Equal(1, s.Generation)
    }

[<Fact>]
let ``分片文本拼成一条消息，回显往返`` () =
    task {
        use srv = new Server(fun ws -> task {
            do! ws.SendAsync(ArraySegment(Encoding.UTF8.GetBytes "he"), WebSocketMessageType.Text, false, CancellationToken.None)
            do! ws.SendAsync(ArraySegment(Encoding.UTF8.GetBytes "llo万"), WebSocketMessageType.Text, true, CancellationToken.None)
            let buf = Array.zeroCreate 1024
            let! r = ws.ReceiveAsync(ArraySegment buf, CancellationToken.None)
            do! send ws ("echo:" + Encoding.UTF8.GetString(buf, 0, r.Count)) })
        let s = TextSocket()
        let got = new Collections.Concurrent.BlockingCollection<int * string>()
        s.TextReceived.Add(fun x -> got.Add x)
        let! g = s.ConnectAsync(srv.Uri, CancellationToken.None)
        let! first = within (Task.Run(fun () -> got.Take()))
        Assert.Equal((g, "hello万"), first)
        let! ok = s.TrySendAsync("ping", Some g)
        Assert.True ok
        let! second = within (Task.Run(fun () -> got.Take()))
        Assert.Equal((g, "echo:ping"), second)
        s.Disconnect()
    }

[<Fact>]
let ``代号不符的发送被拒绝`` () =
    task {
        use srv = new Server(fun _ -> Task.Delay 3000)
        let s = TextSocket()
        let! g = s.ConnectAsync(srv.Uri, CancellationToken.None)
        let! ok = s.TrySendAsync("x", Some(g + 1))
        Assert.False ok
        s.Disconnect()
    }

[<Fact>]
let ``二进制帧带错误关闭`` () =
    task {
        use srv = new Server(fun ws ->
            ws.SendAsync(ArraySegment [| 1uy |], WebSocketMessageType.Binary, true, CancellationToken.None))
        let s = TextSocket()
        let closed = TaskCompletionSource<int * exn option>()
        s.Closed.Add(closed.TrySetResult >> ignore)
        let! g = s.ConnectAsync(srv.Uri, CancellationToken.None)
        let! (cg, err) = within closed.Task
        Assert.Equal(g, cg)
        Assert.Equal("binary frame received", err.Value.Message)
    }

[<Fact>]
let ``对端干净关闭时无错误通知一次`` () =
    task {
        use srv = new Server(fun ws -> ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None))
        let s = TextSocket()
        let mutable count = 0
        let closed = TaskCompletionSource<exn option>()
        s.Closed.Add(fun (_, e) -> Interlocked.Increment(&count) |> ignore; closed.TrySetResult e |> ignore)
        let! _ = s.ConnectAsync(srv.Uri, CancellationToken.None)
        let! err = within closed.Task
        Assert.True(err.IsNone)
        do! Task.Delay 200
        Assert.Equal(1, count)
    }

[<Fact>]
let ``主动断开后旧代的帧与关闭都不再通知`` () =
    task {
        let release = TaskCompletionSource()
        use srv = new Server(fun ws -> task {
            do! release.Task
            try do! send ws "late" with _ -> () })
        let s = TextSocket()
        let mutable events = 0
        s.TextReceived.Add(fun _ -> Interlocked.Increment(&events) |> ignore)
        s.Closed.Add(fun _ -> Interlocked.Increment(&events) |> ignore)
        let! _ = s.ConnectAsync(srv.Uri, CancellationToken.None)
        s.Disconnect()
        release.SetResult()
        do! Task.Delay 300
        Assert.Equal(0, events)
        Assert.False(s.IsConnected)
    }

[<Fact>]
let ``连不上时抛出原异常且代号已前进`` () =
    task {
        let l = new Sockets.TcpListener(IPAddress.Loopback, 0)
        l.Start()
        let port = (l.LocalEndpoint :?> IPEndPoint).Port
        l.Stop()
        let s = TextSocket()
        let! ex = Assert.ThrowsAnyAsync<exn>(fun () -> s.ConnectAsync(Uri(sprintf "ws://127.0.0.1:%d/" port), CancellationToken.None) :> Task)
        Assert.NotNull ex
        Assert.Equal(1, s.Generation)
        Assert.False(s.IsConnected)
    }
