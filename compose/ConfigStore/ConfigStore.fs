namespace Wanxiang.Config

open System
open AtomicTextFile

/// 组合者 C：TOML 文件是配置的唯一权威来源。
/// 文件对象（AtomicTextFile）拥有字节、原子写、外部改动通知；AppConfig（TomlCodec）拥有格式与校验；
/// 本对象拥有合作：何时读、何时写、外部改动是否采纳，以及「当前生效配置」这一份状态。
/// - 只有完整有效的配置才替换当前配置；无效时保留上一份有效配置并报告；
/// - 本进程的修改也走「写盘 → 以写出的原文为准」一条路径；
/// - 写盘前若磁盘已被外部改过（监视器还没通知），先采纳有效的外部编辑；外部编辑无效就拒绝写，绝不覆盖用户的编辑。
type ConfigStore private (file: AtomicTextFile, initial: AppConfig, onReloaded: AppConfig -> unit, onRejected: string -> unit) =
    let gate = obj ()
    let mutable current = initial

    let adopt (text: string) : Result<AppConfig, string> =
        match TomlCodec.tryParse text with
        | Ok cfg -> current <- cfg; Ok cfg
        | Error errs -> Error(String.concat "; " errs)

    /// 文件可能变了：采纳有效的外部编辑，报告无效的或被删除的。
    let reload () =
        let outcome =
            lock gate (fun () ->
                if not file.Exists then Some(Error "config file is missing; keeping the last valid configuration")
                else
                    match file.ExternalEdit() with
                    | None -> None
                    | Some _ ->
                        match file.Read() with
                        | Error e -> Some(Error e)
                        | Ok text -> Some(adopt text))
        match outcome with
        | Some(Ok cfg) -> onReloaded cfg
        | Some(Error e) -> onRejected e
        | None -> ()

    let syncExternalEdit () : Result<unit, string> =
        match file.ExternalEdit() with
        | None -> Ok()
        | Some text ->
            match TomlCodec.tryParse text with
            | Ok cfg ->
                file.Read() |> ignore
                current <- cfg
                Ok()
            | Error errs ->
                Error(sprintf "config on disk was edited externally and is invalid; not overwriting: %s" (String.concat "; " errs))

    let rewrite (cfg: AppConfig) : Result<AppConfig, string> =
        let text = TomlCodec.serialize cfg
        match file.Write text with
        | Error e -> Error(sprintf "config rewrite failed: %s" e)
        | Ok() ->
            match TomlCodec.tryParse text with
            | Ok written -> current <- written; Ok written
            | Error errs -> Error(sprintf "config reload failed after rewrite: %s" (String.concat "; " errs))

    do
        file.Changed.Add(fun () -> reload ())
        file.Watch()

    /// 当前生效配置（最后一次成功加载）。
    member _.Current : AppConfig = lock gate (fun () -> current)

    member _.Path = file.Path

    /// 立即检查文件（通常由监视器触发，也可手动调用）。
    member _.TriggerReload() = reload ()

    /// 完整重写。只有写出的原文能重新解析才返回 Ok；失败保留旧配置。
    member _.Rewrite(newConfig: AppConfig) : Result<unit, string> =
        let r =
            lock gate (fun () ->
                syncExternalEdit () |> Result.bind (fun () -> rewrite newConfig))
        match r with
        | Ok cfg -> onReloaded cfg; Ok()
        | Error e -> Error e

    /// 原子读-改-写：读最新值、改、落盘在同一把锁里。
    member _.Update(mutate: AppConfig -> AppConfig) : Result<unit, string> =
        let r =
            lock gate (fun () ->
                syncExternalEdit () |> Result.bind (fun () -> rewrite (mutate current)))
        match r with
        | Ok cfg -> onReloaded cfg; Ok()
        | Error e -> Error e

    member _.Dispose() = (file :> IDisposable).Dispose()

    interface IDisposable with
        member this.Dispose() = this.Dispose()

    /// 打开配置；文件不存在时写出完整默认配置（含稳定 instanceId）；缺 instanceId 时补上。
    static member Open(path: string, onReloaded: AppConfig -> unit, onRejected: string -> unit) : Result<ConfigStore, string> =
        let file = new AtomicTextFile(path)
        let fail (e: string) = (file :> IDisposable).Dispose(); Error e
        let created =
            if file.Exists then Ok()
            else file.Write(TomlCodec.serialize { AppConfig.defaults (Guid.CreateVersion7()) with tools = ToolsConfig.newInstall })
        match created |> Result.bind (fun () -> file.Read()) with
        | Error e -> fail e
        | Ok raw ->
            let text =
                match TomlCodec.ensureInstanceId raw (Guid.CreateVersion7()) with
                | Some patched -> file.Write patched |> Result.map (fun () -> patched)
                | None -> Ok raw
            match text with
            | Error e -> fail e
            | Ok text ->
                match TomlCodec.tryParse text with
                | Ok cfg ->
                    file.RestrictToOwner() |> ignore
                    Ok(new ConfigStore(file, cfg, onReloaded, onRejected))
                | Error errs -> fail (String.concat "; " errs)
