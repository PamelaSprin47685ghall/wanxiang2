module Tests

open System
open System.IO
open Xunit
open Wanxiang.Config
open Wanxiang.App

let private withDir f =
    let d = Path.Combine(Path.GetTempPath(), "wx-dp-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore

    try
        f d
    finally
        (try
            Directory.Delete(d, true)
         with _ ->
             ())

let private current (configPath: string) =
    match TomlCodec.tryParse (File.ReadAllText configPath) with
    | Ok c -> c
    | Error e -> failwithf "%A" e

[<Fact>]
let ``首次启动：生成默认配置、登记桌面令牌哈希、写 client.toml`` () =
    withDir (fun d ->
        let cfgPath = Path.Combine(d, "config.toml")
        Assert.Equal(Ok(), DesktopPairing.ensure cfgPath)
        let cfg = current cfgPath
        let tok = (DesktopPairing.clientTomlOf cfgPath).TryReadToken() |> Option.get
        let c = cfg.authClients |> List.exactlyOne
        Assert.Equal(ClientToken.ClientToken.hash tok, c.tokenHash)
        Assert.Equal(DesktopClient.DesktopClient.Name, c.name)
        Assert.False c.revoked
        Assert.DoesNotContain(tok, File.ReadAllText cfgPath)
        Assert.Contains(sprintf "url = \"ws://%s/ws\"" cfg.listen, File.ReadAllText(Path.Combine(d, "client.toml"))))

[<Fact>]
let ``令牌仍有效时不改写任何文件`` () =
    withDir (fun d ->
        let cfgPath = Path.Combine(d, "config.toml")
        DesktopPairing.ensure cfgPath |> ignore
        let cfgBytes = File.ReadAllBytes cfgPath
        let clientBytes = File.ReadAllBytes(Path.Combine(d, "client.toml"))
        Assert.Equal(Ok(), DesktopPairing.ensure cfgPath)
        Assert.Equal<byte[]>(cfgBytes, File.ReadAllBytes cfgPath)
        Assert.Equal<byte[]>(clientBytes, File.ReadAllBytes(Path.Combine(d, "client.toml"))))

[<Fact>]
let ``client.toml 丢失时发新令牌，旧记录保留`` () =
    withDir (fun d ->
        let cfgPath = Path.Combine(d, "config.toml")
        DesktopPairing.ensure cfgPath |> ignore
        File.Delete(Path.Combine(d, "client.toml"))
        Assert.Equal(Ok(), DesktopPairing.ensure cfgPath)
        let cfg = current cfgPath
        Assert.Equal(2, cfg.authClients.Length)
        let tok = (DesktopPairing.clientTomlOf cfgPath).TryReadToken() |> Option.get
        Assert.Equal(ClientToken.ClientToken.hash tok, cfg.authClients.Head.tokenHash))

[<Fact>]
let ``令牌被吊销时发新令牌`` () =
    withDir (fun d ->
        let cfgPath = Path.Combine(d, "config.toml")
        DesktopPairing.ensure cfgPath |> ignore
        let old = (DesktopPairing.clientTomlOf cfgPath).TryReadToken() |> Option.get

        match ConfigStore.Open(cfgPath, ignore, ignore) with
        | Ok cs ->
            let cfg = cs.Current

            cs.Rewrite
                { cfg with
                    authClients = cfg.authClients |> List.map (fun c -> { c with revoked = true })
                }
            |> ignore

            (cs :> IDisposable).Dispose()
        | Error e -> failwith e

        Assert.Equal(Ok(), DesktopPairing.ensure cfgPath)
        let fresh = (DesktopPairing.clientTomlOf cfgPath).TryReadToken() |> Option.get
        Assert.NotEqual<string>(old, fresh))

[<Fact>]
let ``配置无效时返回错误且不写 client.toml`` () =
    withDir (fun d ->
        let cfgPath = Path.Combine(d, "config.toml")
        File.WriteAllText(cfgPath, "this is = = not toml [")

        match DesktopPairing.ensure cfgPath with
        | Error _ -> ()
        | Ok() -> failwith "expected error"

        Assert.False(File.Exists(Path.Combine(d, "client.toml"))))
