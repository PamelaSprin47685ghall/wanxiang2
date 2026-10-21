module ExportFileNameProgressThrottleTests

open System
open System.IO
open Xunit
open Wanxiang.UI

let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

[<Fact>]
let ``reads inside one window merge into a single tail render`` () =
    let p = ProgressThrottle()
    Assert.Equal(ProgressRefresh.Now, p.Request(t0, false, true))
    p.Rendered t0
    Assert.Equal(ProgressRefresh.Schedule 60.0, p.Request(t0.AddMilliseconds 40.0, false, true))
    Assert.Equal(ProgressRefresh.Skip, p.Request(t0.AddMilliseconds 50.0, false, true))
    Assert.True(p.TailFired())

[<Fact>]
let ``urgent and terminal states are never delayed`` () =
    let p = ProgressThrottle()
    p.Rendered t0
    Assert.Equal(ProgressRefresh.Now, p.Request(t0.AddMilliseconds 1.0, true, true))
    Assert.Equal(ProgressRefresh.Now, p.Request(t0.AddMilliseconds 2.0, false, false))
