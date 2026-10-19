module CaretBreathTests
open System
open System.IO
open Xunit
open Wanxiang.UI


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
