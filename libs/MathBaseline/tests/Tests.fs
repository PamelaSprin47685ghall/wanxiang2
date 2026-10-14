module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "mathbaseline.txt"))

/// 金样由旧代码（从 旧 Rich/MathVisual.fs 的 Metrics 与基线选择，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

[<Fact>]
let ``inline formula aligns to the host text baseline`` () =
    Assert.Equal(10.0, MathBaseline.drawBaseline false 7.0 (Some 13.0) 3.0)
    Assert.Equal(7.0, MathBaseline.drawBaseline false 7.0 None 3.0)
    Assert.Equal(7.0, MathBaseline.drawBaseline true 7.0 (Some 13.0) 3.0)
