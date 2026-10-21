module RunModesTests

open Xunit
open Wanxiang.App

let private s server client pwa fix : RunSwitches =
    {
        server = server
        client = client
        pwa = pwa
        fix = fix
    }

[<Fact>]
let ``命令行优先于 TOML，TOML 优先于程序默认`` () =
    let toml = Some(s false false true true)
    Assert.Equal(s true false true true, RunModes.resolve (Some true) None None None toml)
    Assert.Equal(RunModes.programDefaults, RunModes.resolve None None None None None)
    Assert.Equal(s true true true false, RunModes.programDefaults)

[<Fact>]
let ``三个都关才是只跑 doctor`` () =
    Assert.True(RunModes.isDoctorOnly (s false false false true))
    Assert.False(RunModes.isDoctorOnly (s false false true false))

[<Fact>]
let ``pwa 隐含主机`` () =
    Assert.True(RunModes.runsHost (s false false true false))
    Assert.False(RunModes.runsHost (s false true false false))

[<Fact>]
let ``自动配对只看 server 开关，不含 pwa 隐含的主机`` () =
    Assert.True(RunModes.autoPairsDesktop (s true true false false))
    Assert.False(RunModes.autoPairsDesktop (s false true true false))

[<Fact>]
let ``主机只在有桌面时后台启动`` () =
    Assert.True(RunModes.hostInBackground (s false true true false))
    Assert.False(RunModes.hostInBackground (s true false true false))
    Assert.False(RunModes.hostInBackground (s false true false false))
