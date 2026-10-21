namespace Wanxiang.Tests

open System
open System.IO
open System.Net.WebSockets
open System.Security.Cryptography
open System.Text
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Store
open Wanxiang.Server
open Wanxiang.Tests.Helpers

module ServerAppTests =

    let private createTestDir () =
        let dir = Path.Combine(Path.GetTempPath(), sprintf "wanxiang-srv-test-%s" (Guid.NewGuid().ToString("N")))
        Directory.CreateDirectory(dir) |> ignore
        dir

    let private writeConfigFile dir port listenHost =
        let configPath = Path.Combine(dir, "config.toml")
        let instanceId = Guid.CreateVersion7()
        let token = "test-token-secret"
        let hash = ClientToken.ClientToken.hash token
        let cfg =
            { AppConfig.defaults instanceId with
                listen = sprintf "%s:%d" listenHost port
                generation = { (AppConfig.defaults instanceId).generation with autoTitle = false }
                providers =
                    Map.ofList
                        [ "mock-provider",
                          { id = "mock-provider"
                            kind = "openai"
                            label = "Mock Provider"
                            baseUrl = "http://127.0.0.1:1/v1"
                            apiKey = Some "sk-mock-key-12345"
                            models = [ "mock-model" ]
                            defaultModel = "mock-model"
                            timeoutSeconds = 5
                            maxRetries = 0
                            enabled = true
                            promptCaching = false
                            headers = Map.empty
                            extraJson = None; visionOverrides = Map.empty } ]
                authClients =
                    [ { tokenHash = hash
                        name = "srv-test-client"
                        createdAtUtc = DateTimeOffset.UtcNow
                        lastSeenUtc = None
                        revoked = false } ] }
        File.WriteAllText(configPath, TomlCodec.serialize cfg)
        configPath

    // =========================================================================
    // 1. DataLock 单实例排他锁测试（Wanxiang.Store.DataLock 真实行为契约）
    // =========================================================================

    [<Fact>]
    let ``数据目录锁：两个存储无法同时打开同一目录，释放后可重新打开`` () =
        let dir = createTestDir ()
        try
            // 首次获取必须成功
            // 数据目录唯一的锁是存储自己的（ledger/.lock）
            let first = new EventStore(dir)
            let ex = Assert.ThrowsAny<InvalidOperationException>(fun () -> new EventStore(dir) |> ignore)
            Assert.Contains("is locked", ex.Message)
            first.Shutdown()
            let again = new EventStore(dir)
            again.Shutdown()
        finally
            try Directory.Delete(dir, true) with _ -> ()

    // =========================================================================
    // 2. ServerApp 构造与生命周期契约测试（五参数真实签名）
    // =========================================================================

    [<Fact>]
    let ``ServerApp 两个实例无法同时在同一 dataDir 启动（严格单进程保护）`` () =
        let dir = createTestDir ()
        try
            let port1 = Random.Shared.Next(35000, 45000)
            let configPath1 = writeConfigFile dir port1 "127.0.0.1"

            // 构造第一个实例（持有目录锁）
            let app1 = new ServerApp(dir, configPath1, false, None, ignore)

            // 第二个实例尝试在相同 dataDir 构造必须因为抢占锁而抛出异常（failwith）
            let ex = Assert.Throws<Exception>(fun () ->
                new ServerApp(dir, configPath1, false, None, ignore) |> ignore)
            Assert.Contains("is locked", ex.Message)

            // 释放 app1
            (app1 :> IDisposable).Dispose()

            // app1 释放锁后，新实例可以正常构造并持有锁
            let app2 = new ServerApp(dir, configPath1, false, None, ignore)
            (app2 :> IDisposable).Dispose()
        finally
            try Directory.Delete(dir, true) with _ -> ()

    [<Fact>]
    let ``ServerApp 正常启动与停止生命周期（Start 与 Stop 幂等释放）`` () =
        let dir = createTestDir ()
        try
            let port = Random.Shared.Next(45001, 55000)
            let configPath = writeConfigFile dir port "127.0.0.1"

            let mutable loggedMessages = []
            let logInfo msg = loggedMessages <- msg :: loggedMessages

            let app = new ServerApp(dir, configPath, false, None, logInfo)
            app.Start(false)

            // 验证已记录启动监听日志
            Assert.Contains(loggedMessages, fun m -> m.Contains("wanxiang server listening on"))

            // 停止主机
            app.Stop()

            // 验证 Stop 幂等：再次调用 Dispose 不会抛出异常
            (app :> IDisposable).Dispose()
        finally
            try Directory.Delete(dir, true) with _ -> ()

    // =========================================================================
    // 3. ServerApp 启动遇到截尾记录时的恢复（Replay 与 onTruncated 契约）
    // =========================================================================

    [<Fact>]
    let ``ServerApp 启动时正确处理截尾事件文件并恢复运行`` () =
        let dir = createTestDir ()
        try
            let port = Random.Shared.Next(35000, 45000)
            let configPath = writeConfigFile dir port "127.0.0.1"

            // 预先写入正常的事件结构目录
            DataPaths.ensureDataDirs dir
            let eventPath = DataPaths.logFile dir

            // 写入一个半截损坏的非正常字节（模拟崩溃时的残余字节）
            let brokenBytes = Encoding.UTF8.GetBytes(""" {"streamId":"partial-json-not-closed""")
            File.WriteAllBytes(eventPath, brokenBytes)

            // 启动 ServerApp（fix = true 执行截尾恢复）
            let app = new ServerApp(dir, configPath, true, None, ignore)
            app.Start(false)

            // 截尾事件文件的**真实契约**（S-001 / STOP-1）：正常启动**不再**自动截尾，
            // 而是以最长有效前缀进入「只读降级模式」——拒绝一切永久写入，
            // 并留下 read-only-degraded 诊断。旧写法只有「能启停」（不抛异常），没验结果。
            let degraded =
                Wanxiang.Server.Stderr.recentErrorLines ()
                |> List.filter (fun l -> l.Contains("read-only-degraded"))
            Assert.True(
                degraded.Length > 0,
                "截尾事件文件应进入只读降级模式并留下 read-only-degraded 诊断")
            // 半截字节仍在原处（启动没有偷偷改用户的日志）。
            let text = Encoding.UTF8.GetString(File.ReadAllBytes(eventPath))
            Assert.Contains("partial-json-not-closed", text)

            app.Stop()
            (app :> IDisposable).Dispose()
        finally
            try Directory.Delete(dir, true) with _ -> ()

    // =========================================================================
    // 4. ServerApp 暴露非本地监听且无 TLS 时的安全告警分支测试
    // =========================================================================

    [<Fact>]
    let ``ServerApp 监听 0.0.0.0 且无 TLS 时正常启动并执行 WarnIfExposedWithoutTls 逻辑`` () =
        let dir = createTestDir ()
        try
            let port = Random.Shared.Next(45000, 55000)
            let configPath = writeConfigFile dir port "0.0.0.0"

            let app = new ServerApp(dir, configPath, false, None, ignore)
            app.Start(false)

            // 「暴露到非回环且无 TLS」必须**被报告出来**，不是默默启动。
            // 告警走 stderr 的结构化通道（Stderr.write "insecure-listen"），
            // 所以从最近错误行里读（不捕获 Console.Error——xunit 并行下会拿到别人的行）。
            // 旧写法只有「能启停」，WarnIfExposedWithoutTls 从未被断言。
            let insecureLines =
                Wanxiang.Server.Stderr.recentErrorLines ()
                |> List.filter (fun l -> l.Contains("insecure-listen"))
            Assert.True(
                insecureLines.Length > 0,
                "监听 0.0.0.0 且无 TLS 时应写出 insecure-listen 告警")

            // 能正常安全停止并释放端口与锁。
            app.Stop()
            (app :> IDisposable).Dispose()
        finally
            try Directory.Delete(dir, true) with _ -> ()


    // =========================================================================
    // 5. ServerApp 反复启停幂等性与资源释放深度契约测试
    // =========================================================================

    [<Fact>]
    let ``ServerApp 连续多次 Stop 与 Dispose 保持严格幂等不抛出异常`` () =
        let dir = createTestDir ()
        try
            let port = Random.Shared.Next(45000, 55000)
            let configPath = writeConfigFile dir port "127.0.0.1"

            let app = new ServerApp(dir, configPath, false, None, ignore)
            app.Start(false)

            // 第一次主动 Stop
            app.Stop()

            // 第二次重复 Stop，必须静默幂等
            app.Stop()

            // Dispose 接口实现必须幂等且不抛出异常
            (app :> IDisposable).Dispose()
            (app :> IDisposable).Dispose()

            // 幂等不是「不抛异常」就完了：目录锁必须真的放开，
            // 同一个 dataDir 能再开一个 ServerApp。
            // 旧写法只验「不抛」——锁没释放也照样绿。
            let port2 = Random.Shared.Next(55001, 65000)
            let configPath2 = writeConfigFile dir port2 "127.0.0.1"
            let app2 =
                try new ServerApp(dir, configPath2, false, None, ignore)
                with ex -> failwithf "Stop/Dispose 之后目录锁应释放，却抛: %s" ex.Message
            Assert.NotNull(app2)
            (app2 :> IDisposable).Dispose()
        finally
            try Directory.Delete(dir, true) with _ -> ()

    [<Fact>]
    let ``ServerApp 启动时配置不存在安全回退到默认配置并正常启停`` () =
        let dir = createTestDir ()
        try
            let nonExistentConfig = Path.Combine(dir, "does-not-exist.toml")
            let mutable loggedMessages = []
            let logInfo msg = loggedMessages <- msg :: loggedMessages
            let app = new ServerApp(dir, nonExistentConfig, false, None, logInfo)
            // 配置不存在时生产实现**创建一份默认配置**并安全回退，不裸抛未捕获异常
            app.Start(false)

            // 回退这件事必须**看得见**：默认配置文件被写出来了，且内容是默认监听地址。
            // 旧写法只有「能启停」，没验回退到哪、报没报。
            Assert.True(File.Exists nonExistentConfig, "配置缺失时应写出默认配置文件")
            let written = File.ReadAllText nonExistentConfig
            Assert.Contains("127.0.0.1:8765", written)
            Assert.True(
                loggedMessages |> List.exists (fun m -> m.Contains("wanxiang server listening on")),
                "回退默认配置后应正常监听")

            app.Stop()
            (app :> IDisposable).Dispose()
        finally
            try Directory.Delete(dir, true) with _ -> ()

    [<Fact>]
    let ``ServerApp 优雅关闭写快照且能从快照续播`` () =
        let dir = createTestDir ()
        try
            // 预写 3 条日志（启动重放来源）
            let day = DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            let path = DataPaths.logFile dir
            Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
            for i in 1..3 do
                let c =
                    Events.Commit.create (uint64 i) (DateTimeOffset(day.AddSeconds(float i)))
                        [ ConversationCreated { conversationId = Guid.NewGuid(); title = sprintf "srv-%d" i; config = SessionConfig.empty } ]
                File.AppendAllText(path, Wanxiang.Store.Assembly.frame c |> System.Text.Encoding.UTF8.GetString)
            let port = Random.Shared.Next(45001, 55000)
            let configPath = writeConfigFile dir port "127.0.0.1"
            let app = new ServerApp(dir, configPath, false, None, ignore)
            app.Start(false)
            app.Stop()
            (app :> IDisposable).Dispose()
            // 优雅关闭必须写出 v2 快照（batch 6：AllCommits 在 Shutdown 前取）
            match Wanxiang.Store.Inspect.run dir with
            | Wanxiang.Store.Healthy o ->
                Assert.Equal(LedgerStore.Used 3UL, o.startup.snapshot)
                Assert.Equal(3UL, o.lastCommitId)
            | other -> failwithf "expected snapshot after graceful stop, got %A" other
        finally
            try Directory.Delete(dir, true) with _ -> ()

