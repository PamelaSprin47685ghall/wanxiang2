namespace Wanxiang.App

open System
open System.IO
open Wanxiang.Config

/// 主机与桌面同进程时，本机桌面客户端自动获得永久令牌（决策 64 + Q190；从旧 Program.fs 拆出）。
/// 协作：ConfigStore（主机 TOML 只存哈希，原子改写）、ClientToken（生成与哈希）、DesktopClient（保留名）、
/// ClientTomlFile（客户端令牌原文，决策 54）。任一侧丢失或已吊销就发新令牌。
module DesktopPairing =

    /// 主机配置旁的 client.toml。
    let clientTomlOf (configPath: string) =
        ClientTomlFile(Path.Combine(Path.GetDirectoryName configPath, "client.toml"))

    /// client.toml 里的 url 用主机配置的 listen；配置读不了用默认地址（与旧实现相同）。
    let private listenOf (configPath: string) =
        match TomlCodec.tryParse (File.ReadAllText configPath) with
        | Ok c -> c.listen
        | Error _ -> "127.0.0.1:8765"

    /// 确保本机桌面令牌有效。错误文字由调用方记为「desktop auto-pairing skipped: …」，不阻止启动。
    let ensure (configPath: string) : Result<unit, string> =
        let clientToml = clientTomlOf configPath
        // 首次启动 TOML 不存在：ConfigStore.Open 先生成完整默认 TOML（决策 182，原子路径）
        let ensureConfig () =
            if File.Exists configPath then
                Ok()
            else
                match ConfigStore.Open(configPath, ignore, ignore) with
                | Ok cs ->
                    (cs :> IDisposable).Dispose()
                    Ok()
                | Error e -> Error e

        match ensureConfig () with
        | Error e -> Error e
        | Ok() ->
            match ConfigStore.Open(configPath, ignore, ignore) with
            | Error e -> Error e
            | Ok cs ->
                let cfg = cs.Current

                let tokenValid =
                    clientToml.TryReadToken()
                    |> Option.exists (fun tok ->
                        cfg.authClients
                        |> List.exists (fun c -> c.tokenHash = ClientToken.ClientToken.hash tok && not c.revoked))

                let outcome =
                    if tokenValid then
                        Ok()
                    else
                        let token = ClientToken.ClientToken.generate ()

                        let newCfg =
                            { cfg with
                                authClients =
                                    {
                                        tokenHash = ClientToken.ClientToken.hash token
                                        name = DesktopClient.DesktopClient.Name
                                        createdAtUtc = DateTimeOffset.UtcNow
                                        lastSeenUtc = None
                                        revoked = false
                                    }
                                    :: cfg.authClients
                            }

                        match cs.Rewrite newCfg with
                        | Ok() ->
                            (try
                                clientToml.Write(listenOf configPath, token)
                             with e ->
                                 Error e.Message)
                        | Error e -> Error e

                (cs :> IDisposable).Dispose()
                outcome
