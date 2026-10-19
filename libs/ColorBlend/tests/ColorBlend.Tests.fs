module ColorBlendTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``opaque overlay replaces color but keeps base alpha`` () =
    Assert.Equal((10uy, 1uy, 2uy, 3uy), ColorBlend.over (10uy, 200uy, 200uy, 200uy) (255uy, 1uy, 2uy, 3uy))

[<Fact>]
let ``transparent overlay leaves the base`` () =
    Assert.Equal((255uy, 9uy, 8uy, 7uy), ColorBlend.over (255uy, 9uy, 8uy, 7uy) (0uy, 255uy, 255uy, 255uy))

// ---- 文档规则的真实行为（全部 alpha 与若干分量）

[<Fact>]
let ``result keeps base alpha and each channel lies between base and overlay`` () =
    let values = [ 0uy; 1uy; 17uy; 128uy; 200uy; 254uy; 255uy ]
    for a in 0 .. 255 do
        for bc in values do
            for oc in values do
                let rA, r, _, _ = ColorBlend.over (77uy, bc, 0uy, 0uy) (byte a, oc, 0uy, 0uy)
                Assert.Equal(77uy, rA)
                Assert.True(r >= min bc oc && r <= max bc oc)
                // 线性插值向零取整
                Assert.Equal(byte (int bc + (int oc - int bc) * a / 255), r)

[<Fact>]
let ``more overlay alpha moves the channel monotonically toward the overlay`` () =
    let channel a = let _, r, _, _ = ColorBlend.over (255uy, 20uy, 0uy, 0uy) (byte a, 240uy, 0uy, 0uy) in r
    for a in 1 .. 255 do Assert.True(channel a >= channel (a - 1))
    Assert.Equal(20uy, channel 0)
    Assert.Equal(240uy, channel 255)
