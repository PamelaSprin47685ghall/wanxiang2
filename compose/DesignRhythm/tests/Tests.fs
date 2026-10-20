module Tests

open Xunit
open Wanxiang.UI

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

[<Fact>]
let ``断点从小到大排开，最窄窗口落在 compact 档`` () =
    Assert.True(LayoutPolicy.formSingleColumnBreakpoint < LayoutPolicy.compactBreakpoint)
    Assert.True(LayoutPolicy.compactBreakpoint < LayoutPolicy.wideLayoutBreakpoint)
    // 最窄桌面窗口（700）已经越过单列断点（440），但还没到 compact 断点（720）：
    // 留白取的是同一档里的中间档，与 719 宽的窗口完全一致。
    Assert.Equal(
        LayoutPolicy.horizontalInset (LayoutPolicy.compactBreakpoint - 1.0),
        LayoutPolicy.horizontalInset LayoutPolicy.desktopMinWidth)
    Assert.Equal(Spacing.space4xl, LayoutPolicy.horizontalInset LayoutPolicy.desktopMinWidth)

[<Fact>]
let ``水平留白随窗口变宽单调不减，只在断点处跳档`` () =
    let widths = [ 0.0; 320.0; 439.0; 440.0; 500.0; 719.0; 720.0; 800.0; 1023.0; 1024.0; 4000.0 ]
    let insets = widths |> List.map LayoutPolicy.horizontalInset
    // 不减：没有哪一段更宽的窗口留白反而更小。
    Assert.True(insets |> List.pairwise |> List.forall (fun (a, b) -> b >= a))
    // 只在断点处跳档：相邻断点之间留白不变。
    for bp in [ LayoutPolicy.formSingleColumnBreakpoint; LayoutPolicy.compactBreakpoint; LayoutPolicy.wideLayoutBreakpoint ] do
        let before = LayoutPolicy.horizontalInset (bp - 0.01)
        let after = LayoutPolicy.horizontalInset bp
        Assert.True(after > before)
        // 断点两侧是同一个档：断点前一档的宽度与更早的宽度一致。
        Assert.Equal(before, LayoutPolicy.horizontalInset (bp - 1.0))

[<Fact>]
let ``四档留白取自间距刻度的固定档位`` () =
    Assert.Equal(Spacing.space2xl, LayoutPolicy.horizontalInset 0.0)
    Assert.Equal(Spacing.space4xl, LayoutPolicy.horizontalInset (LayoutPolicy.formSingleColumnBreakpoint))
    Assert.Equal(Spacing.space6xl, LayoutPolicy.horizontalInset LayoutPolicy.compactBreakpoint)
    Assert.Equal(Spacing.space10xl, LayoutPolicy.horizontalInset LayoutPolicy.wideLayoutBreakpoint)
    // 档位本身也在刻度上，且四档严格递增。
    let tiers = [ 0.0; LayoutPolicy.formSingleColumnBreakpoint; LayoutPolicy.compactBreakpoint; LayoutPolicy.wideLayoutBreakpoint ]
                |> List.map LayoutPolicy.horizontalInset
    Assert.True(tiers |> List.forall Spacing.isRamp)
    Assert.True(tiers |> List.pairwise |> List.forall (fun (a, b) -> b > a))

[<Fact>]
let ``非法宽度落回最窄档，无穷宽度落回最宽档`` () =
    // 负宽度没有意义，按最窄的一档处理。
    Assert.Equal(Spacing.space2xl, LayoutPolicy.horizontalInset -100.0)
    Assert.Equal(Spacing.space2xl, LayoutPolicy.horizontalInset 0.0)
    // 无穷与极大宽度都取最宽的一档。
    Assert.Equal(Spacing.space10xl, LayoutPolicy.horizontalInset System.Double.PositiveInfinity)
    Assert.Equal(Spacing.space10xl, LayoutPolicy.horizontalInset 1e12)

[<Fact>]
let ``正文行高高于次级与技术行高，且三者随字号同比缩放`` () =
    for size in [ 12.0; 14.5; 16.0; 20.0 ] do
        let prose = ReadingRhythm.proseLineHeight size
        let secondary = ReadingRhythm.secondaryLineHeight size
        let technical = ReadingRhythm.technicalLineHeight size
        Assert.True(prose > secondary)
        Assert.True(secondary > technical)
        // 同比缩放：字号翻倍，行高也翻倍（比例与字号无关）。
        Assert.Equal(prose * 2.0, ReadingRhythm.proseLineHeight (size * 2.0), 9)
        Assert.Equal(secondary * 2.0, ReadingRhythm.secondaryLineHeight (size * 2.0), 9)
        Assert.Equal(technical * 2.0, ReadingRhythm.technicalLineHeight (size * 2.0), 9)

[<Fact>]
let ``标题字号在四级之前逐级收紧，之后不再下降`` () =
    let baseSize = 14.0
    let sizes = [ for l in 1 .. 6 -> ReadingRhythm.headingFontSize baseSize l ]
    // 一到四级严格递减。
    Assert.True(sizes |> List.take 3 |> List.pairwise |> List.forall (fun (a, b) -> b < a))
    // 五、六级与更深的级别都停在最小的一档。
    let floorSize = ReadingRhythm.headingFontSize baseSize 5
    Assert.Equal(floorSize, ReadingRhythm.headingFontSize baseSize 6)
    Assert.Equal(floorSize, ReadingRhythm.headingFontSize baseSize 0)
    Assert.True(floorSize > baseSize)

[<Fact>]
let ``标题前后间距随级别收紧并落在刻度上`` () =
    let befores = [ for l in 1 .. 6 -> ReadingRhythm.headingBefore l ]
    Assert.True(befores |> List.pairwise |> List.forall (fun (a, b) -> b <= a))
    Assert.True(befores |> List.forall Spacing.isRamp)
    // 前间距：一级最松，往下逐级收紧。
    Assert.Equal(Spacing.space4xl, ReadingRhythm.headingBefore 1)
    Assert.Equal(Spacing.space3xl, ReadingRhythm.headingBefore 2)
    Assert.Equal(Spacing.space2xl, ReadingRhythm.headingBefore 3)
    // 后间距：一、二级用正文节奏，更深的级别收紧一档。
    Assert.Equal(Spacing.spaceMd, ReadingRhythm.headingAfter 1)
    Assert.Equal(Spacing.spaceMd, ReadingRhythm.headingAfter 2)
    Assert.Equal(Spacing.spaceXs, ReadingRhythm.headingAfter 3)
    Assert.Equal(Spacing.spaceXs, ReadingRhythm.headingAfter 6)
