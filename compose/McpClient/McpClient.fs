namespace Wanxiang.Server

open System
open System.Collections.Concurrent
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Wanxiang.Config

/// MCP 传输层：把 JSON-RPC 请求送出去并等回响应。失败也以 `{"error":...}` 节点返回，不抛异常。
type IMcpTransport =
    abstract Request: method: string * paramsObj: JsonObject * timeoutMs: int * ct: CancellationToken -> Task<JsonNode>
    abstract Notify: method: string * paramsObj: JsonObject -> unit
    abstract Stop: unit -> unit
    /// 在途请求数（排空判定）
    abstract Pending: int

/// 旧仓库的 McpJson 入口：权威实现在 McpProtocol、JsonRpc。
module McpJson =
    [<Literal>]
    let ProtocolVersion = McpProtocol.ProtocolVersion

    let errorNode (message: string) : JsonNode = JsonRpc.errorNode message
    let initializeParams () : JsonObject = McpProtocol.initializeParams ()

/// 本地 stdio MCP（决策 96-100）：LineProcess 说行，PendingCalls 配对响应，McpProtocol 给握手内容。
type McpStdioTransport(config: McpServerConfig, onLog: string -> unit) =

    let gate = obj ()
    let pending = PendingCalls()
    let mutable nextId = 0
    let mutable stopped = false

    let onLine (line: string) =
        try
            match JsonNode.Parse line with
            | :? JsonObject as o -> pending.Complete o |> ignore
            | _ -> ()
        with e ->
            onLog (sprintf "[mcp:%s] malformed response line dropped: %s" config.id e.Message)

    let onExit (failure: string option) =
        // O-001：读取循环异常必须留下证据（R6）
        failure
        |> Option.iter (fun m -> onLog (sprintf "[mcp:%s] stderr read loop failed: %s" config.id m))
        // 子进程退出：在途调用结束为失败结果（决策 97：不自动重放）
        pending.FailAll "MCP 进程已退出"

    let mutable proc: LineProcess option = None

    let send (o: JsonObject) =
        match proc with
        | Some p -> p.WriteLine(o.ToJsonString())
        | None -> failwith "mcp stdin not ready"

    member private _.SendRaw
        (method: string, paramsObj: JsonObject, timeoutMs: int, ct: CancellationToken)
        : Task<JsonNode> =
        task {
            let id = Interlocked.Increment(&nextId)
            let waiting = pending.Register id
            let mutable writeError = None

            try
                send (JsonRpc.request id method paramsObj)
            with ex ->
                pending.Forget id
                writeError <- Some(sprintf "写入 MCP 失败：%s" ex.Message)

            match writeError with
            | Some message -> return JsonRpc.errorNode message
            | None -> return! pending.Await(id, waiting, timeoutMs, ct)
        }

    /// 按需启动子进程并完成 initialize 握手。
    member private this.EnsureStarted() : Result<unit, string> =
        lock gate (fun () ->
            if stopped then
                Error(sprintf "MCP %s 已停止" config.id)
            elif proc |> Option.exists _.IsRunning then
                Ok()
            else
                match config.command with
                | None -> Error(sprintf "MCP %s 未配置 command" config.id)
                | Some cmd ->
                    let kill () = proc |> Option.iter _.Kill()

                    try
                        let p =
                            new LineProcess(
                                cmd,
                                config.args,
                                config.env,
                                onLine,
                                (fun pid text -> onLog (sprintf "[mcp:%s pid=%d] %s" config.id pid text)),
                                onExit
                            )

                        proc <- Some p
                        p.Start()

                        let response =
                            this
                                .SendRaw("initialize", McpProtocol.initializeParams (), 15000, CancellationToken.None)
                                .GetAwaiter()
                                .GetResult()

                        match response with
                        | :? JsonObject as o ->
                            match JsonRpc.resultOf o with
                            | Ok _ ->
                                send (JsonRpc.notification "notifications/initialized" (JsonObject()))
                                Ok()
                            | Error _ when not (isNull o["error"]) ->
                                kill ()
                                Error(sprintf "MCP %s initialize 被拒绝：%s" config.id (o["error"].ToJsonString()))
                            | Error _ ->
                                kill ()
                                Error(sprintf "MCP %s initialize 响应异常" config.id)
                        | _ ->
                            kill ()
                            Error(sprintf "MCP %s initialize 响应不是 JSON 对象" config.id)
                    with ex ->
                        kill ()
                        Error(sprintf "MCP %s 启动失败：%s" config.id ex.Message))

    interface IMcpTransport with
        member this.Request(method, paramsObj, timeoutMs, ct) =
            task {
                match this.EnsureStarted() with
                | Error e -> return JsonRpc.errorNode e
                | Ok() -> return! this.SendRaw(method, paramsObj, timeoutMs, ct)
            }

        member _.Notify(method, paramsObj) =
            try
                send (JsonRpc.notification method paramsObj)
            with e ->
                onLog (sprintf "[mcp:%s] notify %s failed: %s" config.id method e.Message)

        member _.Pending = pending.Count

        member _.Stop() =
            lock gate (fun () -> stopped <- true)
            pending.FailAll "MCP 正在关闭"
            lock gate (fun () -> proc |> Option.iter _.Kill())

/// 远程 MCP：Streamable HTTP。env 在这里的含义是附加请求头（stdio 下才是环境变量）。
type McpHttpTransport(config: McpServerConfig, endpoint: string, onLog: string -> unit) =

    let session =
        new StreamableHttpSession(
            endpoint,
            config.env,
            TimeSpan.FromSeconds(float (max 30 config.callTimeoutSeconds) + 10.0)
        )

    let mutable nextId = 0
    let mutable initialized = false
    let mutable inFlight = 0
    let gate = obj ()

    let post (envelope: JsonObject) (timeoutMs: int) (ct: CancellationToken) : Task<JsonNode> =
        task {
            match! session.Post(envelope, timeoutMs, ct) with
            | Ok node -> return node
            | Error message -> return JsonRpc.errorNode message
        }

    member private _.EnsureInitialized(ct: CancellationToken) : Task<Result<unit, string>> =
        task {
            if initialized then
                return Ok()
            else
                let! response =
                    post
                        (JsonRpc.request (Interlocked.Increment(&nextId)) "initialize" (McpProtocol.initializeParams ()))
                        15000
                        ct

                match response with
                | :? JsonObject as o ->
                    if not (isNull o["error"]) then
                        return Error(sprintf "MCP %s initialize 被拒绝：%s" config.id (o["error"].ToJsonString()))
                    else
                        let! _ =
                            post (JsonRpc.notification "notifications/initialized" (JsonObject())) 5000 ct

                        lock gate (fun () -> initialized <- true)
                        onLog (sprintf "[mcp:%s] connected to %s" config.id endpoint)
                        return Ok()
                | _ -> return Error(sprintf "MCP %s initialize 响应异常" config.id)
        }

    interface IMcpTransport with
        member this.Request(method, paramsObj, timeoutMs, ct) =
            task {
                Interlocked.Increment(&inFlight) |> ignore

                try
                    match! this.EnsureInitialized ct with
                    | Error e -> return JsonRpc.errorNode e
                    | Ok() ->
                        let envelope = JsonRpc.request (Interlocked.Increment(&nextId)) method paramsObj

                        try
                            return! post envelope timeoutMs ct
                        with
                        | :? OperationCanceledException when not ct.IsCancellationRequested ->
                            return JsonRpc.errorNode (sprintf "MCP 调用超时（%d 秒）" (timeoutMs / 1000))
                        | :? OperationCanceledException -> return JsonRpc.errorNode "调用已取消"
                        | ex -> return JsonRpc.errorNode (sprintf "MCP 调用失败：%s" ex.Message)
                finally
                    Interlocked.Decrement(&inFlight) |> ignore
            }

        member _.Notify(method, paramsObj) =
            // O-001：HTTP 通知失败不得静默——记录到 onLog（不抛给调用方）
            task {
                let! r = post (JsonRpc.notification method paramsObj) 5000 CancellationToken.None

                if not (isNull r) && not (isNull r["error"]) then
                    onLog (sprintf "[mcp:%s] notify %s failed: %s" config.id method (r["error"].ToJsonString()))
            }
            |> fun t ->
                (t :> Task)
                    .ContinueWith((fun (t: Task) -> t.Exception |> ignore), TaskContinuationOptions.OnlyOnFaulted)
            |> ignore

        member _.Pending = inFlight
        member _.Stop() = (session :> IDisposable).Dispose()

/// MCP 客户端：一个 MCP 服务一个实例，按需连接；工具声明缓存；每次调用有超时；并发受限；排空关闭。
type McpClient(config: McpServerConfig, onLog: string -> unit) =

    let transport: IMcpTransport =
        match config.url with
        | Some url -> McpHttpTransport(config, url, onLog) :> IMcpTransport
        | None -> McpStdioTransport(config, onLog) :> IMcpTransport

    let semaphore =
        new SemaphoreSlim(config.maxConcurrency |> Option.defaultValue Int32.MaxValue)

    let mutable activeCount = 0
    let mutable stopped = false
    let toolCache = ConcurrentDictionary<string, McpToolInfo>()
    let mutable toolsLoaded = false
    let cacheGate = obj ()
    let callTimeoutMs = max 1000 (config.callTimeoutSeconds * 1000)

    member _.Id = config.id

    /// 并发受限的请求入口。activeCount 覆盖「等名额 + 执行中」，关闭时据此排空（决策 100）。
    member private _.Request(method: string, paramsObj: JsonObject, ct: CancellationToken) : Task<JsonNode> =
        task {
            if stopped then
                return JsonRpc.errorNode (sprintf "MCP %s 已停止" config.id)
            else
                Interlocked.Increment(&activeCount) |> ignore
                let mutable acquired = false

                try
                    try
                        let! ok = semaphore.WaitAsync(Timeout.Infinite, ct)
                        acquired <- ok

                        if not ok then
                            return JsonRpc.errorNode "无法获取 MCP 并发名额"
                        else
                            return! transport.Request(method, paramsObj, callTimeoutMs, ct)
                    with
                    | :? OperationCanceledException -> return JsonRpc.errorNode "调用已取消"
                    | :? ObjectDisposedException -> return JsonRpc.errorNode (sprintf "MCP %s 已停止" config.id)
                    | ex -> return JsonRpc.errorNode ex.Message
                finally
                    if acquired then
                        (try
                            semaphore.Release() |> ignore
                         with _ ->
                             // 退出期间信号量若已先行 Dispose，忽略 Release 的 ObjectDisposedException
                             ())

                    Interlocked.Decrement(&activeCount) |> ignore
        }

    /// 拉取并缓存工具声明。失败返回空列表并记日志（不阻塞对话）。
    member this.LoadTools(ct: CancellationToken) : Task<McpToolInfo list> =
        task {
            let! response = this.Request("tools/list", JsonObject(), ct)

            match JsonRpc.resultOf response with
            | Error e ->
                onLog (sprintf "[mcp:%s] tools/list 失败：%s" config.id e)
                return []
            | Ok result ->
                let infos = McpProtocol.parseTools result

                lock cacheGate (fun () ->
                    toolCache.Clear()

                    for info in infos do
                        toolCache[info.name] <- info

                    toolsLoaded <- true)

                return infos
        }

    /// 已缓存的工具声明；首次访问会同步拉取一次。
    member this.Tools() : McpToolInfo list =
        if not toolsLoaded then
            try
                use timeout = new CancellationTokenSource(TimeSpan.FromSeconds 20.0)
                this.LoadTools(timeout.Token).GetAwaiter().GetResult() |> ignore
            with ex ->
                onLog (sprintf "[mcp:%s] 工具发现失败：%s" config.id ex.Message)

        toolCache.Values |> List.ofSeq |> List.sortBy (fun t -> t.name)

    member this.TryTool(name: string) : McpToolInfo option =
        match toolCache.TryGetValue name with
        | true, info -> Some info
        | _ -> this.Tools() |> List.tryFind (fun t -> t.name = name)

    /// 调用工具。参数解析失败不调用工具，直接返回失败结果（Q170）。
    member this.Call(toolName: string, argsJson: string, ct: CancellationToken) : Task<string> =
        task {
            match McpProtocol.parseArguments argsJson with
            | None -> return McpProtocol.InvalidArguments
            | Some args ->
                let! response = this.Request("tools/call", McpProtocol.callParams toolName args, ct)
                return McpProtocol.renderCallResult (JsonRpc.resultOf response)
        }

    /// 排空关闭（决策 98/100）：不再接新调用，允许在途与排队调用自然完成，最多等 5 秒。
    member _.Stop() =
        stopped <- true
        let deadline = DateTimeOffset.UtcNow.AddSeconds 5.0

        while (transport.Pending > 0 || activeCount > 0) && DateTimeOffset.UtcNow < deadline do
            Thread.Sleep 50

        transport.Stop()

        try
            semaphore.Dispose()
        with _ ->
            // 退出清理：信号量可能已被并发路径释放，忽略即可
            ()
