namespace Wanxiang.App

/// 本次运行的四个开关（决策 60：命令行 > TOML > 程序默认）。从旧 Program.fs 拆出。
type RunSwitches = { server: bool; client: bool; pwa: bool; fix: bool }

module RunModes =

    /// TOML 不存在或解析失败时的程序默认。
    let programDefaults = { server = true; client = true; pwa = true; fix = false }

    /// 命令行给了就用命令行，否则用 TOML 的（没有 TOML 用程序默认）。
    let resolve (server: bool option) (client: bool option) (pwa: bool option) (fix: bool option) (toml: RunSwitches option) : RunSwitches =
        let d = toml |> Option.defaultValue programDefaults
        { server = server |> Option.defaultValue d.server
          client = client |> Option.defaultValue d.client
          pwa = pwa |> Option.defaultValue d.pwa
          fix = fix |> Option.defaultValue d.fix }

    /// 三个组件都关 = 只跑 doctor（fix 决定是否修复，决策 51）。
    let isDoctorOnly (s: RunSwitches) = not s.server && not s.client && not s.pwa

    /// 决策 50：PWA 由主机托管并依赖其 /ws，所以 pwa 隐含主机功能（pwa-only = 无 UI 主机 + 静态资源），绝不静默空转。
    let runsHost (s: RunSwitches) = s.server || s.pwa

    /// 主机与桌面同进程时才自动配对本机桌面客户端（决策 64）。注意按 server 开关判断，不含 pwa 隐含的主机。
    let autoPairsDesktop (s: RunSwitches) = s.server && s.client

    /// 决策 291：主机 + 桌面同进程时主机在后台启动，窗口先画；纯主机同步启动。
    let hostInBackground (s: RunSwitches) = s.client && runsHost s
