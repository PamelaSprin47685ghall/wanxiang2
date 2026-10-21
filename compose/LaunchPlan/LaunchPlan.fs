namespace Wanxiang.App

open System.IO
open Wanxiang.Config

/// 本次启动要做什么（从旧 Program.fs 拆出）：命令行 + 主机 TOML 的 [runtime] 开关 → 路径与模式。
/// 协作：CliArgs（解析与路径）、AppConfig（读 TOML 的开关）、RunModes（优先级与模式推导）。
type LaunchPlan =
    {
        options: CliOptions
        configPath: string
        dataDir: string
        switches: RunSwitches
    }

module LaunchPlan =

    /// TOML 不存在或解析失败时为 None（退回程序默认）。
    let tomlSwitches (configPath: string) : RunSwitches option =
        if File.Exists configPath then
            match TomlCodec.tryParse (File.ReadAllText configPath) with
            | Ok cfg ->
                Some
                    {
                        server = cfg.runtime.server
                        client = cfg.runtime.client
                        pwa = cfg.runtime.pwa
                        fix = cfg.runtime.fix
                    }
            | Error _ -> None
        else
            None

    let ofOptions (o: CliOptions) (home: string) : LaunchPlan =
        let configPath, dataDir = Cli.paths o home

        {
            options = o
            configPath = configPath
            dataDir = dataDir
            switches = RunModes.resolve o.server o.client o.pwa o.fix (tomlSwitches configPath)
        }
