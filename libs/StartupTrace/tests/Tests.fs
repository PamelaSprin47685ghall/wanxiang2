module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由旧程序集跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

[<Fact>]
let ``290 未设置环境变量时不打点；值为 exit 时打点并退出`` () =
    Assert.Equal((false, false), StartupTrace.modeOf null)
    Assert.Equal((false, false), StartupTrace.modeOf "")
    Assert.Equal((true, false), StartupTrace.modeOf "1")
    Assert.Equal((true, true), StartupTrace.modeOf "exit")
    Assert.Equal("startup.firstFrame uptime_ms=1235", StartupTrace.line "firstFrame" 1234.6)
