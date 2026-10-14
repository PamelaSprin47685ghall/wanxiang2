module Tests

open System
open System.IO
open Xunit
open Wanxiang.App

let private withHome f =
    let d = Path.Combine(Path.GetTempPath(), "wx-lp-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore
    try f d finally (try Directory.Delete(d, true) with _ -> ())

[<Fact>]
let ``没有 TOML 时用程序默认`` () =
    withHome (fun h ->
        let p = LaunchPlan.ofOptions Cli.defaults h
        Assert.Equal(RunModes.programDefaults, p.switches)
        Assert.Equal(Path.Combine(h, "config.toml"), p.configPath)
        Assert.Equal(Path.Combine(h, "data"), p.dataDir))

[<Fact>]
let ``TOML 的 runtime 开关生效，命令行覆盖它`` () =
    withHome (fun h ->
        let cfg = Wanxiang.Config.AppConfig.defaults (Guid.NewGuid())
        let cfg = { cfg with runtime = { cfg.runtime with server = false; client = false; pwa = true; fix = false } }
        File.WriteAllText(Path.Combine(h, "config.toml"), Wanxiang.Config.TomlCodec.serialize cfg)
        let p = LaunchPlan.ofOptions Cli.defaults h
        Assert.Equal({ server = false; client = false; pwa = true; fix = false }, p.switches)
        let p2 = LaunchPlan.ofOptions (Cli.parse [| "--pwa=false" |]) h
        Assert.True(RunModes.isDoctorOnly p2.switches))

[<Fact>]
let ``TOML 解析失败时退回程序默认，不抛`` () =
    withHome (fun h ->
        File.WriteAllText(Path.Combine(h, "config.toml"), "[[[ broken")
        Assert.Equal(RunModes.programDefaults, (LaunchPlan.ofOptions Cli.defaults h).switches))
