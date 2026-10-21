module StartupTraceTests

open System
open System.IO
open Xunit
open Wanxiang.UI

[<Fact>]
let ``290 未设置环境变量时不打点；值为 exit 时打点并退出`` () =
    Assert.Equal((false, false), StartupTrace.modeOf null)
    Assert.Equal((false, false), StartupTrace.modeOf "")
    Assert.Equal((true, false), StartupTrace.modeOf "1")
    Assert.Equal((true, true), StartupTrace.modeOf "exit")
    Assert.Equal("startup.firstFrame uptime_ms=1235", StartupTrace.line "firstFrame" 1234.6)

// ---- 执行侧（迁自旧 Model/StartupTrace.fs 的环境变量 / mark / requestExit）----
// mode 在模块初始化时读一次环境变量；测试进程（与 CI）未设置 WANXIANG_STARTUP_TRACE，enabled 为 false。

[<Fact>]
let ``290 未启用时 mark 不记录；requestExit 可替换`` () =
    if isNull (Environment.GetEnvironmentVariable "WANXIANG_STARTUP_TRACE") then
        Assert.False(StartupTrace.mark "test-milestone")

    let called = ref false
    let saved = StartupTrace.requestExit
    StartupTrace.requestExit <- fun () -> called := true
    StartupTrace.requestExit ()
    Assert.True(!called)
    StartupTrace.requestExit <- saved
