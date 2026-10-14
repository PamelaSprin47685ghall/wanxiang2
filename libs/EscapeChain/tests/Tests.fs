module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由 tools/ 下原样摘出的旧代码跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

[<Fact>]
let ``D3/D4 抽屉开着时 Escape 先收抽屉，不停止生成`` () =
    Assert.Equal(EscapeTarget.CloseDrawer, EscapeChain.decide false false true true)
    Assert.Equal(EscapeTarget.CloseSettings, EscapeChain.decide false true true true)
    Assert.Equal(EscapeTarget.StopGeneration, EscapeChain.decide false false false true)
