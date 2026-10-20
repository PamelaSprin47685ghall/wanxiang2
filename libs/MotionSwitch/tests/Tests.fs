module Wanxiang2.MotionSwitch.Tests.Tests

open System
open System.IO
open Xunit
open Wanxiang2.MotionSwitch

[<Fact>]
let ``reduced motion zeroes durations and notifies only on change`` () =
    let m = MotionSwitch()
    let seen = Collections.Generic.List<bool>()
    m.Changed.Add seen.Add
    Assert.Equal(TimeSpan.FromMilliseconds 200.0, m.Duration 200.0)
    m.ApplyReduced true
    m.ApplyReduced true
    Assert.Equal(TimeSpan.Zero, m.Duration 200.0)
    m.ApplyReduced false
    Assert.Equal<bool list>([ true; false ], List.ofSeq seen)
