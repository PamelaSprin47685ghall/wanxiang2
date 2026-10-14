module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "caretbreath.txt"))

/// 金样由旧代码（旧 Views/MessageCard.fs caretBreath*，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``starts at peak, bottoms at half period, back at full period`` () =
    Assert.Equal(1.0, CaretBreath.opacity 0.55 1.0 900.0 0.0, 9)
    Assert.Equal(0.55, CaretBreath.opacity 0.55 1.0 900.0 900.0, 9)
    Assert.Equal(1.0, CaretBreath.opacity 0.55 1.0 900.0 1800.0, 9)

[<Fact>]
let ``opacity always stays inside the band`` () =
    for t in -5000.0 .. 37.0 .. 5000.0 do
        let o = CaretBreath.opacity 0.55 1.0 900.0 t
        Assert.InRange(o, 0.55, 1.0)
