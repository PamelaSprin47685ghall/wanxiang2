namespace Wanxiang.Client

open System
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks

/// 按「代」管理的文本 WebSocket 客户端（从旧 WsClient 拆出传输部分）。
/// 每次连接或断开都让代号加一；旧代的帧与关闭通知一律丢弃，发送可以要求「只在某一代上发」。
/// 只认文本帧：收到二进制帧视为协议错误，带错误关闭。文本内容怎么解释归调用方。
type TextSocket() =

    let mutable ws: ClientWebSocket = null
    let mutable cts: CancellationTokenSource = null
    let mutable generation = 0
    let sendGate = new SemaphoreSlim(1, 1)
    let connectionLock = obj ()

    let onText = Event<int * string>()
    let onClosed = Event<int * exn option>()

    let detach () =
        let g, previousSocket, previousCts =
            lock connectionLock (fun () ->
                let g = Interlocked.Increment(&generation)
                let s, c = ws, cts
                ws <- null
                cts <- null
                g, s, c)

        if not (isNull previousCts) then
            try
                previousCts.Cancel()
            with _ ->
                ()

            previousCts.Dispose()

        if not (isNull previousSocket) then
            try
                previousSocket.Dispose()
            with _ ->
                ()

        g

    /// 收到一条完整文本消息：(代号, 文本)。只有当时仍是当前代才触发。
    member _.TextReceived = onText.Publish
    /// 当前代的连接结束：(代号, 错误)。干净关闭、取消、被替换时错误为 None。
    member _.Closed = onClosed.Publish
    member _.Generation = Volatile.Read(&generation)
    member _.IsCurrent(g: int) = g = Volatile.Read(&generation)

    member _.IsConnected =
        lock connectionLock (fun () -> not (isNull ws) && ws.State = WebSocketState.Open)

    /// 连接并启动接收循环，返回这次连接的代号。连接途中被新的连接或断开替换时抛 OperationCanceledException。
    member this.ConnectAsync(uri: Uri, ct: CancellationToken) : Task<int> =
        task {
            let g = detach ()
            let newWs = new ClientWebSocket()
            let newCts = CancellationTokenSource.CreateLinkedTokenSource ct

            try
                do! newWs.ConnectAsync(uri, newCts.Token)

                let installed =
                    lock connectionLock (fun () ->
                        if g = generation then
                            ws <- newWs
                            cts <- newCts
                            true
                        else
                            false)

                if installed then
                    Async.Start(this.ReceiveLoop(newWs, newCts.Token, g)) |> ignore
                    return g
                else
                    newWs.Dispose()
                    newCts.Dispose()
                    return raise (OperationCanceledException "connection superseded")
            with e ->
                newCts.Dispose()
                newWs.Dispose()
                return raise e
        }

    member _.Disconnect() = detach () |> ignore

    /// 返回传输是否完成；断线、取消、代号不符都返回 false，不抛异常。
    member _.TrySendAsync(text: string, expectedGeneration: int option) : Task<bool> =
        task {
            let socket, tokenSource =
                lock connectionLock (fun () ->
                    if expectedGeneration |> Option.exists (fun e -> e <> generation) then
                        null, null
                    else
                        ws, cts)

            if
                not (isNull socket)
                && not (isNull tokenSource)
                && socket.State = WebSocketState.Open
            then
                let bytes = Encoding.UTF8.GetBytes text

                try
                    do! sendGate.WaitAsync(tokenSource.Token)

                    try
                        if socket.State = WebSocketState.Open then
                            do!
                                socket.SendAsync(
                                    ArraySegment<byte>(bytes),
                                    WebSocketMessageType.Text,
                                    true,
                                    tokenSource.Token
                                )

                            return true
                        else
                            return false
                    finally
                        sendGate.Release() |> ignore
                with
                | :? OperationCanceledException -> return false
                | :? ObjectDisposedException -> return false
                | :? WebSocketException -> return false
            else
                return false
        }

    /// 接收循环用 async 而不是 task：browser-wasm 单线程运行时中，task CE 对 pending task 的 await 会
    /// 走 Task.InternalWait 阻塞等待（“Cannot wait on monitors on this runtime”），async 的 trampoline 不会。
    member private this.ReceiveLoop(socket: ClientWebSocket, ct: CancellationToken, g: int) : Async<unit> =
        async {
            let mutable shouldNotify = true

            let closed error =
                if this.IsCurrent g then
                    onClosed.Trigger(g, error)

            try
                let buffer = Array.zeroCreate<byte>(1024 * 1024)
                use ms = new System.IO.MemoryStream()
                let mutable doneReceiving = false

                while not doneReceiving && socket.State = WebSocketState.Open do
                    let! result = socket.ReceiveAsync(ArraySegment<byte>(buffer), ct) |> Async.AwaitTask

                    if result.MessageType = WebSocketMessageType.Close then
                        doneReceiving <- true
                    elif result.MessageType = WebSocketMessageType.Binary then
                        doneReceiving <- true
                        closed (Some(exn "binary frame received"))
                        shouldNotify <- false
                    else
                        ms.Write(buffer, 0, result.Count)

                        if result.EndOfMessage then
                            let text = Encoding.UTF8.GetString(ms.ToArray())
                            ms.SetLength 0L

                            if this.IsCurrent g then
                                onText.Trigger(g, text)

                if shouldNotify then
                    closed None
            with
            | :? OperationCanceledException ->
                if shouldNotify then
                    closed None
            | :? ObjectDisposedException ->
                if shouldNotify then
                    closed None
            | e ->
                if shouldNotify then
                    closed (Some e)
        }
