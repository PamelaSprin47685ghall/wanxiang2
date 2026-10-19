module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

[<Fact>]
let ``the curve starts at 0, ends at 1 and front-loads the motion`` () =
    Assert.Equal(0.0, EaseOutCubic.ease 0.0, 6)
    Assert.Equal(1.0, EaseOutCubic.ease 1.0, 6)
    Assert.True(EaseOutCubic.ease 0.5 > 0.6)

[<Fact>]
let ``frames follow a moving target and land on it after 180 ms`` () =
    let s = ScrollTween()
    Assert.True(s.Start(t0, 0.0, 100.0, false))
    match s.Frame(t0.AddMilliseconds 90.0, 200.0) with
    | ScrollFrame.Offset y -> Assert.InRange(y, 100.0, 200.0); Assert.True(s.IsOwnOffset y)
    | other -> failwithf "%A" other
    Assert.Equal(ScrollFrame.Done 200.0, s.Frame(t0.AddMilliseconds 180.0, 200.0))
    Assert.False s.IsRunning

[<Fact>]
let ``near targets and reduced motion do not animate`` () =
    let s = ScrollTween()
    Assert.False(s.Start(t0, 0.0, 4.9, false))
    Assert.False(s.Start(t0, 0.0, 400.0, true))
    Assert.False s.IsRunning

[<Fact>]
let ``a user wheel cancels the tween and its offsets stop being ours`` () =
    let s = ScrollTween()
    s.Start(t0, 0.0, 100.0, false) |> ignore
    s.Cancel()
    Assert.Equal(ScrollFrame.Done 100.0, s.Frame(t0.AddMilliseconds 10.0, 100.0))
    Assert.False(s.IsOwnOffset 50.0)
