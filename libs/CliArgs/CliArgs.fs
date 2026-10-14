namespace Wanxiang.App

open System
open System.IO

/// 命令行选项（从旧 Wanxiang.App/Program.fs 的 Cli 模块拆出）。
type CliOptions = {
    server: bool option
    client: bool option
    pwa: bool option
    fix: bool option
    config: string option
    data: string option
    bundle: string option
    /// 决策 251：系统打开 `wanxiang://` 链接时作为唯一位置参数传入
    openUrl: string option
    /// 要求打印用法。旧实现在解析中途直接打印并退出进程；现在交给入口处理，库不结束进程。
    help: bool
}

module Cli =

    let defaults =
        { server = None
          client = None
          pwa = None
          fix = None
          config = None
          data = None
          bundle = None
          openUrl = None
          help = false }

    [<Literal>]
    let usage = "usage: wanxiang [--server=true|false] [--client=true|false] [--pwa=true|false] [--fix] [--config <path>] [--data <dir>] [--bundle <path>]"

    let parseBool (s: string) : bool option =
        match s.ToLowerInvariant() with
        | "true" | "1" | "yes" | "on" -> Some true
        | "false" | "0" | "no" | "off" -> Some false
        | _ -> None

    /// 解析命令行。支持 --server=true/false、--client、--pwa、--fix、--config <path>、--data <dir>、--bundle <path>。
    /// 未知参数或缺值抛异常（文字与旧实现相同）；遇到 --help/-h 立即返回 help = true。
    let parse (argv: string array) : CliOptions =
        let mutable o = defaults
        let mutable i = 0
        while i < argv.Length && not o.help do
            let arg = argv[i]
            let inline takeValue () =
                i <- i + 1
                if i >= argv.Length then failwithf "missing value for %s" arg
                argv[i]
            if arg.StartsWith "--config=" then o <- { o with config = Some(arg.Substring "--config=".Length) }
            elif arg = "--config" then o <- { o with config = Some(takeValue ()) }
            elif arg.StartsWith "--data=" then o <- { o with data = Some(arg.Substring "--data=".Length) }
            elif arg = "--data" then o <- { o with data = Some(takeValue ()) }
            elif arg.StartsWith "--server=" then o <- { o with server = parseBool (arg.Substring "--server=".Length) }
            elif arg = "--server" then o <- { o with server = Some true }
            elif arg.StartsWith "--client=" then o <- { o with client = parseBool (arg.Substring "--client=".Length) }
            elif arg = "--client" then o <- { o with client = Some true }
            elif arg.StartsWith "--pwa=" then o <- { o with pwa = parseBool (arg.Substring "--pwa=".Length) }
            elif arg = "--pwa" then o <- { o with pwa = Some true }
            elif arg = "--fix" then o <- { o with fix = Some true }
            elif arg.StartsWith "--fix=" then o <- { o with fix = parseBool (arg.Substring "--fix=".Length) }
            elif arg.StartsWith "--bundle=" then o <- { o with bundle = Some(arg.Substring "--bundle=".Length) }
            elif arg = "--bundle" then o <- { o with bundle = Some(takeValue ()) }
            elif arg.StartsWith("wanxiang://", StringComparison.OrdinalIgnoreCase) then o <- { o with openUrl = Some arg }
            elif arg = "--help" || arg = "-h" then o <- { o with help = true }
            else failwithf "unknown argument: %s" arg
            i <- i + 1
        o

    /// 配置目录：WANXIANG_HOME，否则 ~/.config/wanxiang。
    let defaultHome () : string =
        match Environment.GetEnvironmentVariable "WANXIANG_HOME" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ -> Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".config", "wanxiang")

    /// (配置文件路径, 数据目录)，都是绝对路径：--config 默认 <home>/config.toml，--data 默认配置旁的 data。
    let paths (o: CliOptions) (home: string) : string * string =
        let configPath = o.config |> Option.defaultValue (Path.Combine(home, "config.toml")) |> Path.GetFullPath
        let dataDir = o.data |> Option.defaultValue (Path.Combine(Path.GetDirectoryName configPath, "data")) |> Path.GetFullPath
        configPath, dataDir
