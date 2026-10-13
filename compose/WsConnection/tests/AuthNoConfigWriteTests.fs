module AuthNoConfigWriteTests

// 真实组合：ConfigStore（真实 TOML 文件）、EventStore、ToolRegistry、ChatOrchestrator、AttachmentStore、PairingDesk、
// DeviceSightings 与 WsConnection，经本机 WebSocket 认证。钉住：认证（含重复认证、跨连接）对用户配置文件零写入，
// 最近认证时间只进主机自有的 device-sightings.json。
// 根因记录：旧实现每次认证对用户编辑的 TOML 读-改-写 lastSeen，与外部编辑竞争（extract-ledger watcher 测试间歇失败）。

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiang.Config
open Wanxiang.Protocol
open Wanxiang.Store
open Wanxiang.Server

let private freePort () =
    let l = new TcpListener(IPAddress.Loopback, 0)
    l.Start()
    let p = (l.LocalEndpoint :?> IPEndPoint).Port
    l.Stop()
    p

let private receiveText (ws: WebSocket) (timeout: TimeSpan) : string option =
    use cts = new CancellationTokenSource(timeout)
    let buf = Array.zeroCreate<byte> 65536
    let sb = StringBuilder()
    try
        let mutable fin = false
        while not fin do
            let r = ws.ReceiveAsync(ArraySegment buf, cts.Token).Result
            if r.MessageType = WebSocketMessageType.Close then fin <- true
            else
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count)) |> ignore
                fin <- r.EndOfMessage
        Some(sb.ToString())
    with _ -> None

[<Fact>]
let ``auth produces zero config writes`` () =
    let dir = Path.Combine(Path.GetTempPath(), "wx-ws-auth-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    try
        let token = ClientToken.ClientToken.generate ()
        let hash = ClientToken.ClientToken.hash token
        let client = { tokenHash = hash; name = "手机"; createdAtUtc = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); lastSeenUtc = None; revoked = false }
        let configPath = Path.Combine(dir, "wanxiang.toml")
        File.WriteAllText(configPath, TomlCodec.serialize { AppConfig.defaults (Guid "11111111-2222-3333-4444-555555555555") with authClients = [ client ] })
        let textBefore = File.ReadAllText configPath
        let mtimeBefore = File.GetLastWriteTimeUtc configPath
        let cs = match ConfigStore.Open(configPath, ignore, ignore) with Ok c -> c | Error e -> failwith e
        let mutable updates = 0
        let updateConfig (f: AppConfig -> AppConfig) =
            Interlocked.Increment(&updates) |> ignore
            cs.Rewrite(f cs.Current)
        use store = new EventStore(Path.Combine(dir, "data"))
        let registry = ToolRegistry((fun () -> cs.Current), ignore)
        let orch = ChatOrchestrator(store, (fun () -> store.Projection), (fun _ _ -> ()), registry, (fun () -> cs.Current), (fun _ -> None), ignore)
        let attachments = AttachmentStore(Path.Combine(dir, "data"), 1024L * 1024L)
        let desk = PairingDesk.PairingDesk({ window = TimeSpan.FromMinutes 1.0; maxFailures = 5; freeze = TimeSpan.FromMinutes 5.0 }, TimeSpan.FromMinutes 5.0)
        let sightingsPath = Path.Combine(dir, "data", "device-sightings.json")
        let sightings = DeviceSightings(sightingsPath)
        let port = freePort ()
        use listener = new HttpListener()
        listener.Prefixes.Add(sprintf "http://127.0.0.1:%d/" port)
        listener.Start()
        let mutable nextId = 0
        let serve () =
            task {
                let! ctx = listener.GetContextAsync()
                let! wsCtx = ctx.AcceptWebSocketAsync(null)
                nextId <- nextId + 1
                let conn =
                    WsConnection(nextId, wsCtx.WebSocket, "127.0.0.1", (fun () -> store.Projection), (fun () -> cs.Current), updateConfig,
                                 (fun _ _ -> CommandQueued), orch, attachments, (fun () -> System.Text.Json.Nodes.JsonArray()),
                                 (fun c n -> store.CommitsAfter(c, n)), desk, ignore, (fun () -> []), sightings, ignore)
                conn.HostVersion <- "9.9.9-injected"
                do! conn.Run(CancellationToken.None)
            }
        for _ in 1 .. 3 do
            let serverTask = serve ()
            use ws = new ClientWebSocket()
            Assert.True(ws.ConnectAsync(Uri(sprintf "ws://127.0.0.1:%d/" port), CancellationToken.None).Wait(TimeSpan.FromSeconds 10.0), "connect")
            let msg = Encoding.UTF8.GetBytes(WireAgui.encode (AuthPresent {| token = token |}))
            ws.SendAsync(ArraySegment msg, WebSocketMessageType.Text, true, CancellationToken.None).Wait()
            let first = receiveText ws (TimeSpan.FromSeconds 10.0)
            Assert.True(first |> Option.exists (fun s -> s.Contains "auth-accepted" || s.Contains "auth.accepted"), sprintf "expected auth.accepted, got %A" first)
            let second = receiveText ws (TimeSpan.FromSeconds 10.0)
            Assert.True(second |> Option.exists (fun s -> s.Contains "9.9.9-injected"), sprintf "expected injected hostVersion, got %A" second)
            ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).Wait(TimeSpan.FromSeconds 5.0) |> ignore
            serverTask.Wait(TimeSpan.FromSeconds 10.0) |> ignore
            ws.Abort()
        Assert.Equal(0, updates)
        Assert.Equal(textBefore, File.ReadAllText configPath)
        Assert.Equal(mtimeBefore, File.GetLastWriteTimeUtc configPath)
        Assert.Empty(Directory.GetFiles(dir, "*.tmp*"))
        Assert.True((sightings.LastSeen hash).IsSome)
        Assert.True((DeviceSightings(sightingsPath).LastSeen hash).IsSome)
        (cs :> IDisposable).Dispose()
    finally
        try Directory.Delete(dir, true) with _ -> ()
