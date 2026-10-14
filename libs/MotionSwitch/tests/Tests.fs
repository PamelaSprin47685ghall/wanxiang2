module Wanxiang2.MotionSwitch.Tests.Tests

open System
open System.IO
open Xunit
open Wanxiang2.MotionSwitch

/// 金样由照抄的旧 MotionLedger 生成
[<Fact>]
let ``golden: every timing matches the old ledger`` () =
    let expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "motionswitch.txt"))
    Assert.Equal(expected, Render.render [ for t in MotionTimings.all -> sprintf "%A" t, MotionTimings.initialMs t ])

[<Fact>]
let ``reduced motion zeroes durations and notifies only on change`` () =
    let m = MotionSwitch()
    let seen = Collections.Generic.List<bool>()
    m.Changed.Add seen.Add
    Assert.Equal(TimeSpan.FromMilliseconds 200.0, m.Duration 200.0)
    m.SetReduced true
    m.SetReduced true
    Assert.Equal(TimeSpan.Zero, m.Duration 200.0)
    m.SetReduced false
    Assert.Equal<bool list>([ true; false ], List.ofSeq seen)
