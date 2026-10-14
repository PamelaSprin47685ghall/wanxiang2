module Wanxiang2.SkeletonList.Tests.Tests

open System
open System.IO
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.SkeletonList
open Wanxiang2.SkeletonList.Tests

let private bar = SolidColorBrush(Color.Parse "#E3E0D3") :> IBrush
let private edge = SolidColorBrush(Color.Parse "#DDD9CA") :> IBrush

let private mk (reduced: Cell<bool>) (frame: Cell<TimeSpan>) =
    { Ink = (function SkeletonInk.Bar -> bar | SkeletonInk.Edge -> edge)
      Gauge =
        function
        | SkeletonGauge.Gap -> Cell 24.0 :> _
        | SkeletonGauge.EndBreathing -> Cell 48.0 :> _
        | SkeletonGauge.ReadingWidth -> Cell 748.0 :> _
        | SkeletonGauge.AvatarSize -> Cell 28.0 :> _
        | SkeletonGauge.RadiusSm -> Cell 6.0 :> _
        | SkeletonGauge.RadiusLg -> Cell 12.0 :> _
        | SkeletonGauge.RadiusPill -> Cell 999.0 :> _
        | SkeletonGauge.SpaceMd -> Cell 8.0 :> _
        | SkeletonGauge.SpaceXl -> Cell 12.0 :> _
        | SkeletonGauge.BorderWidth -> Cell 1.0 :> _
        | SkeletonGauge.OpacityBase -> Cell 0.65 :> _
        | SkeletonGauge.OpacityBreathMin -> Cell 0.55 :> _
        | SkeletonGauge.OpacityReduced -> Cell 0.85 :> _
      Frame = frame
      Reduced = reduced }

/// 与 BreathPhase 相同的曲线（测试不引用兄弟库）
let private phaseBreath () =
    let mutable phase = 0.0
    { Begin = fun () -> phase <- 0.0
      Step =
        fun () ->
            phase <- phase + 0.12
            0.75 + 0.2 * Math.Sin phase }

let private same (expected: string) (actual: string) =
    let e = expected.Split '\n'
    let a = actual.Split '\n'
    for i in 0 .. min e.Length a.Length - 1 do
        if e.[i] <> a.[i] then
            Assert.Fail(sprintf "line %d\nold: %s\nnew: %s" (i + 1) e.[i] a.[i])
    Assert.Equal(e.Length, a.Length)

[<Fact>]
let ``骨架树与旧 ChatView ShowSkeletonLoading 相同`` () =
    Headless.run (fun () ->
        let s = SkeletonList(mk (Cell true) (Cell(TimeSpan.FromMilliseconds 50.0)), phaseBreath ())
        s.Show()
        let got = Dump.tree FontFamily.Default s.Host
        let expect = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "skeleton.txt"))
        same expect got)

[<Fact>]
let ``显示且挂上才呼吸；摘下停；减弱动效停在静态透明度`` () =
    Headless.run (fun () ->
        let reduced = Cell false
        let s = SkeletonList(mk reduced (Cell(TimeSpan.FromMilliseconds 50.0)), phaseBreath ())
        Assert.False s.IsShown
        Assert.False s.IsBreathing
        s.Show()
        Assert.True s.IsShown
        // 尚未挂树：旧代码不启动计时器，透明度取减弱档
        Assert.False s.IsBreathing
        Assert.Equal(0.85, s.Host.Opacity, 6)
        let w = Window(Width = 600.0, Height = 400.0, Content = s.Host)
        w.Show()
        Assert.True s.IsBreathing
        s.Step()
        Assert.True(s.Host.Opacity < 1.0)
        reduced.Set true
        Assert.False s.IsBreathing
        Assert.Equal(0.85, s.Host.Opacity, 6)
        reduced.Set false
        Assert.True s.IsBreathing
        s.Hide()
        Assert.False s.IsShown
        Assert.False s.IsBreathing
        w.Close())
