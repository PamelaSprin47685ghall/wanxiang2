module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "colorblend.txt"))

/// 金样由旧代码（从 旧 Controls/Interaction.fs 的 blendOver，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

[<Fact>]
let ``opaque overlay replaces color but keeps base alpha`` () =
    Assert.Equal((10uy, 1uy, 2uy, 3uy), ColorBlend.over (10uy, 200uy, 200uy, 200uy) (255uy, 1uy, 2uy, 3uy))

[<Fact>]
let ``transparent overlay leaves the base`` () =
    Assert.Equal((255uy, 9uy, 8uy, 7uy), ColorBlend.over (255uy, 9uy, 8uy, 7uy) (0uy, 255uy, 255uy, 255uy))
