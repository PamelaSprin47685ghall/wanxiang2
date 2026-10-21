module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``inline formula aligns to the host text baseline`` () =
    Assert.Equal(10.0, MathBaseline.drawBaseline false 7.0 (Some 13.0) 3.0)
    Assert.Equal(7.0, MathBaseline.drawBaseline false 7.0 None 3.0)
    Assert.Equal(7.0, MathBaseline.drawBaseline true 7.0 (Some 13.0) 3.0)

// ---- 文档规则的真实行为

[<Fact>]
let ``box height spans above and below and the baseline sits at above`` () =
    for w, a, b in [ 10.0, 7.5, 2.25; 1.0, 0.0, 4.0; 3.0, 9.0, 0.0 ] do
        let width, height, baseline = MathBaseline.metrics (Some(w, a, b))
        Assert.Equal(w, width)
        Assert.Equal(a + b, height)
        Assert.Equal(a, baseline)

    Assert.Equal((0.0, 0.0, 0.0), MathBaseline.metrics None)

[<Fact>]
let ``display mode ignores the host baseline`` () =
    for host in [ None; Some 0.0; Some 99.0 ] do
        for y in [ -3.0; 0.0; 12.0 ] do
            Assert.Equal(5.5, MathBaseline.drawBaseline true 5.5 host y)

[<Fact>]
let ``inline baseline lands on the host line baseline in host coordinates`` () =
    // 控件在文本块里偏移 y，绘制基线加上 y 必须回到宿主基线
    for host in [ 0.0; 14.0; 20.5 ] do
        for y in [ -2.0; 0.0; 3.5 ] do
            Assert.Equal(host, MathBaseline.drawBaseline false 9.0 (Some host) y + y)
