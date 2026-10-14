module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let private moduleType name =
    AppDomain.CurrentDomain.GetAssemblies()
    |> Array.pick (fun a -> match a.GetType("Wanxiang.UI." + name) with null -> None | t -> Some t)

/// 金样由旧 LayoutPolicy.fs 与 Metrics.fs 的 ReadingRhythm 反射列出全部公开值并对函数求值
[<Fact>]
let ``golden: every value and function matches the old code`` () =
    LayoutPolicy.horizontalInset 0.0 |> ignore
    ReadingRhythm.proseLineHeight 1.0 |> ignore
    let expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "rhythm.txt"))
    Assert.Equal(expected, Dump.render [ moduleType "LayoutPolicy"; moduleType "ReadingRhythm" ] (Probes.rhythm ()))

[<Fact>]
let ``inset steps exactly at the breakpoints`` () =
    Assert.Equal(16.0, LayoutPolicy.horizontalInset (LayoutPolicy.formSingleColumnBreakpoint - 0.01))
    Assert.Equal(24.0, LayoutPolicy.horizontalInset LayoutPolicy.formSingleColumnBreakpoint)
    Assert.Equal(32.0, LayoutPolicy.horizontalInset LayoutPolicy.compactBreakpoint)
    Assert.Equal(48.0, LayoutPolicy.horizontalInset LayoutPolicy.wideLayoutBreakpoint)

[<Fact>]
let ``narrowest desktop window is already compact`` () =
    Assert.True(LayoutPolicy.desktopMinWidth < LayoutPolicy.compactBreakpoint)

[<Fact>]
let ``inset and rhythm values stay on the spacing ramp`` () =
    for w in [ 0.0; 500.0; 800.0; 2000.0 ] do
        Assert.True(Spacing.isRamp (LayoutPolicy.horizontalInset w))
    for l in 1 .. 6 do
        Assert.True(Spacing.isRamp (ReadingRhythm.headingBefore l))
        Assert.True(Spacing.isRamp (ReadingRhythm.headingAfter l))

[<Fact>]
let ``heading sizes shrink with level and floor at plus one`` () =
    let sizes = [ for l in 1 .. 6 -> ReadingRhythm.headingFontSize 14.0 l ]
    Assert.True(sizes |> List.pairwise |> List.forall (fun (a, b) -> a >= b))
    Assert.Equal(15.0, ReadingRhythm.headingFontSize 14.0 0)
