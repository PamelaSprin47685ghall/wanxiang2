module ConfigStoreTests

open System
open System.IO
open Xunit
open Wanxiang.Config


[<Fact>]
let ``config store reload keeps last valid configuration on invalid file`` () =
    let dir = Path.Combine(Path.GetTempPath(), "wanxiang-config-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "wanxiang.toml")
    try
        let initial = AppConfig.defaults (Guid.NewGuid())
        File.WriteAllText(path, TomlCodec.serialize initial)
        let rejected = ResizeArray<string>()
        use store =
            match ConfigStore.Open(path, ignore, rejected.Add) with
            | Ok value -> value
            | Error e -> failwith e
        File.WriteAllText(path, "configVersion = 2\ninvalid = true\n")
        store.TriggerReload()
        // 等「拒绝」回调本身（事件），不按墙钟睡固定时长：满载下 debounce 计时器可能晚于 250ms 才触发。
        let deadline = DateTime.UtcNow.AddSeconds 30.0
        while rejected.Count = 0 && DateTime.UtcNow < deadline do System.Threading.Thread.Sleep 10
        Assert.NotEmpty rejected
        Assert.Equal(initial.instanceId, store.Current.instanceId)
    finally
        if Directory.Exists dir then Directory.Delete(dir, true)

[<Fact>]
let ``config store rewrite persists and reloads complete configuration`` () =
    let dir = Path.Combine(Path.GetTempPath(), "wanxiang-config-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "wanxiang.toml")
    try
        let initial = AppConfig.defaults (Guid.NewGuid())
        File.WriteAllText(path, TomlCodec.serialize initial)
        use store =
            match ConfigStore.Open(path, ignore, ignore) with
            | Ok value -> value
            | Error e -> failwith e
        let updated = { initial with listen = "127.0.0.1:9876" }
        match store.Rewrite updated with
        | Error e -> failwith e
        | Ok () ->
            let reloaded =
                match TomlCodec.tryParse (File.ReadAllText path) with
                | Ok value -> value
                | Error errors -> failwith (String.concat "; " errors)
            Assert.Equal("127.0.0.1:9876", reloaded.listen)
    finally
        if Directory.Exists dir then Directory.Delete(dir, true)

    let cfg = AppConfig.defaults (Guid.NewGuid())
    let text = TomlCodec.serialize cfg
    match TomlCodec.tryParse text with
    | Error errs -> failwith (String.concat "; " errs)
    | Ok parsed ->
        Assert.Equal(cfg.instanceId, parsed.instanceId)
        Assert.True parsed.runtime.server
        Assert.True parsed.runtime.client
        Assert.True parsed.runtime.pwa
        Assert.False parsed.runtime.fix

[<Fact>]
let ``rewrite 在 reload 失败时返回 Error 并保留旧配置`` () =
    let dir = Path.Combine(Path.GetTempPath(), "wanxiang-config-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "wanxiang.toml")
    try
        let initial = AppConfig.defaults (Guid.CreateVersion7())
        File.WriteAllText(path, TomlCodec.serialize initial)
        use rejectedSignal = new System.Threading.ManualResetEventSlim(false)
        use store =
            match ConfigStore.Open(path, ignore, (fun _ -> rejectedSignal.Set())) with
            | Ok value -> value
            | Error e -> failwith e
        // 成功路径：Rewrite 后内存与磁盘一致（决策 44）
        let updated = { initial with listen = "127.0.0.1:12345" }
        match store.Rewrite updated with
        | Error e -> failwith e
        | Ok () -> Assert.Equal("127.0.0.1:12345", store.Current.listen)
        // 制造磁盘文件非法（reload 失败）：外部写入非法内容后 watcher 触发 reload，保留旧配置
        File.WriteAllText(path, "configVersion = 2\ninstanceId = 1\n") // instanceId 非法 → reload 失败
        store.TriggerReload()
        // 等 reload 真的失败（拒绝回调），而不是睡 250ms 后假定它已发生。
        Assert.True(rejectedSignal.Wait(TimeSpan.FromSeconds 30.0), "外部非法写入未触发拒绝")
        Assert.Equal("127.0.0.1:12345", store.Current.listen) // 保留最后有效配置
    finally
        if Directory.Exists dir then Directory.Delete(dir, true)

[<Fact>]
let ``首次创建的 instanceId 是 UUIDv7`` () =
    let dir = Path.Combine(Path.GetTempPath(), "wanxiang-config-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "wanxiang.toml")
    try
        use store =
            match ConfigStore.Open(path, ignore, ignore) with
            | Ok value -> value
            | Error e -> failwith e
        // UUIDv7 版本号 = 7（Guid.Version 属性在 .NET 8+ 可用）
        Assert.Equal(7, int (store.Current.instanceId.Version))
        // 再次打开仍保持同一 instanceId（稳定）
        let id = store.Current.instanceId
        store.Dispose()
        use store2 =
            match ConfigStore.Open(path, ignore, ignore) with
            | Ok value -> value
            | Error e -> failwith e
        Assert.Equal(id, store2.Current.instanceId)
    finally
        if Directory.Exists dir then Directory.Delete(dir, true)

[<Fact>]
let ``TOML 配置文件权限为 0600`` () =
    if OperatingSystem.IsLinux() then
        let dir = Path.Combine(Path.GetTempPath(), "wanxiang-config-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory dir |> ignore
        let path = Path.Combine(dir, "wanxiang.toml")
        try
            let initial = AppConfig.defaults (Guid.CreateVersion7())
            File.WriteAllText(path, TomlCodec.serialize initial)
            use store =
                match ConfigStore.Open(path, ignore, ignore) with
                | Ok value -> value
                | Error e -> failwith e
            let mode = File.GetUnixFileMode path
            Assert.Equal(UnixFileMode.UserRead ||| UnixFileMode.UserWrite, mode &&& (UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.GroupRead ||| UnixFileMode.GroupWrite ||| UnixFileMode.OtherRead ||| UnixFileMode.OtherWrite))
            // 写回后权限保持
            store.Rewrite { initial with listen = "127.0.0.1:54321" } |> ignore
            let mode2 = File.GetUnixFileMode path
            Assert.Equal(mode, mode2)
        finally
            if Directory.Exists dir then Directory.Delete(dir, true)

// ---------------------------------------------------------------- TLS

[<Fact>]
let ``ConfigStore Update 原子读改写与 Path 属性覆盖`` () =
    let dir = Path.Combine(Path.GetTempPath(), "wanxiang-config-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "wanxiang.toml")
    try
        let initial = AppConfig.defaults (Guid.NewGuid())
        File.WriteAllText(path, TomlCodec.serialize initial)
        use store =
            match ConfigStore.Open(path, ignore, ignore) with
            | Ok s -> s
            | Error e -> failwith e
        Assert.Equal(path, store.Path)
        match store.Update(fun c -> { c with listen = "127.0.0.1:4444" }) with
        | Ok () ->
            Assert.Equal("127.0.0.1:4444", store.Current.listen)
        | Error e -> failwith e
    finally
        if Directory.Exists dir then Directory.Delete(dir, true)



[<Fact>]
let ``hand-written config without instanceId gets one generated instead of failing`` () =
    // 决策 272：r9 走查中手写最小 config.toml 缺 instanceId 时主机直接退出。
    let dir = Path.Combine(Path.GetTempPath(), "wx-cfg-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "wanxiang.toml")
    try
        let minimal =
            (TomlCodec.serialize (AppConfig.defaults (Guid.NewGuid()))).Split('\n')
            |> Array.filter (fun line -> not (line.TrimStart().StartsWith "instanceId"))
            |> String.concat "\n"
        Assert.True(Result.isError (TomlCodec.tryParse minimal))
        use store =
            match ConfigStore.Open(path |> fun p -> (File.WriteAllText(p, minimal); p), ignore, ignore) with
            | Ok value -> value
            | Error e -> failwith e
        Assert.NotEqual(Guid.Empty, store.Current.instanceId)
        // 补写落盘：重启后仍是同一个实例标识
        match TomlCodec.tryParse (File.ReadAllText path) with
        | Ok cfg -> Assert.Equal(store.Current.instanceId, cfg.instanceId)
        | Error e -> failwithf "%A" e
        Assert.True((TomlCodec.ensureInstanceId (File.ReadAllText path) (Guid.NewGuid())).IsNone)
    finally
        try Directory.Delete(dir, true) with _ -> ()

// 决策 462：lastSeen 回写这类应用内读-改-写，不得覆盖监视器尚未处理的外部编辑（CI 偶发：
// auth.accepted 之后的 lastSeen 回写把测试刚写进去的非法配置换回旧的有效配置，config.rejected 永远不来）。
[<Fact>]
let ``app-side update never clobbers a pending external edit`` () =
    let dir = Path.Combine(Path.GetTempPath(), "wanxiang-config-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory dir |> ignore
    let path = Path.Combine(dir, "wanxiang.toml")
    try
        let initial = AppConfig.defaults (Guid.NewGuid())
        File.WriteAllText(path, TomlCodec.serialize initial)
        use store =
            match ConfigStore.Open(path, ignore, ignore) with
            | Ok value -> value
            | Error e -> failwith e
        // 外部改成非法：应用内更新被拒，文件原样保留
        File.WriteAllText(path, "this is = = not valid toml [[[")
        Assert.True(Result.isError (store.Update(fun c -> { c with listen = "127.0.0.1:9001" })))
        Assert.Equal("this is = = not valid toml [[[", File.ReadAllText path)
        // 外部改成另一份有效配置：应用内更新以它为基础，不丢外部改动
        File.WriteAllText(path, TomlCodec.serialize { initial with pairingFreezeMinutes = 37 })
        match store.Update(fun c -> { c with listen = "127.0.0.1:9002" }) with
        | Error e -> failwith e
        | Ok () ->
            match TomlCodec.tryParse (File.ReadAllText path) with
            | Ok cfg ->
                Assert.Equal(37, cfg.pairingFreezeMinutes)
                Assert.Equal("127.0.0.1:9002", cfg.listen)
            | Error e -> failwithf "%A" e
    finally
        try Directory.Delete(dir, true) with _ -> ()


// 真实组合：监视器发现有效的外部编辑 → 采纳并通知；文件被删 → 报告并保留上一份有效配置
[<Fact>]
let ``watcher adopts a valid external edit and reports deletion`` () =
    let dir = Path.Combine(Path.GetTempPath(), "cfgstore-" + Guid.NewGuid().ToString("N"))
    let path = Path.Combine(dir, "config.toml")
    let reloaded = new System.Threading.ManualResetEventSlim()
    let rejected = new System.Threading.ManualResetEventSlim()
    match ConfigStore.Open(path, (fun _ -> reloaded.Set()), (fun _ -> rejected.Set())) with
    | Error e -> failwith e
    | Ok store ->
        use store = store
        let edited = TomlCodec.serialize { store.Current with generation = { store.Current.generation with maxToolRounds = 3 } }
        File.WriteAllText(path, edited)
        Assert.True(reloaded.Wait(TimeSpan.FromSeconds 5.0), "edit not adopted")
        Assert.Equal(3, store.Current.generation.maxToolRounds)
        File.Delete path
        Assert.True(rejected.Wait(TimeSpan.FromSeconds 5.0), "deletion not reported")
        Assert.Equal(3, store.Current.generation.maxToolRounds)
