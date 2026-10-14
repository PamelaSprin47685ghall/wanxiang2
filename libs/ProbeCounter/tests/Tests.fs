module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由旧程序集跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

// 迁自旧 R28PwaParityTests。
[<Fact>]
let ``probe counts only when enabled`` () =
    let probe = ProbeCounter()
    probe.Hit "test.r28.off" |> ignore
    Assert.Equal(0, probe.Count "test.r28.off")
    probe.Enabled <- true
    Assert.True(probe.Hit "test.r28.on")
    Assert.False(probe.Hit "test.r28.on")
    Assert.True(probe.Count "test.r28.on" >= 2)
