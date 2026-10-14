module Tests

open System
open Xunit
open Wanxiang.UI

let t0 = DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero)

// 迁自旧 DevicesUiTests（原用例起无头窗口、点开设备分区；这里只留等待状态与说明文字）。
[<Fact>]
let ``device list read without a reply times out with the older host hint`` () =
    let w = BoundedWait(BoundedWaits.devicesLimit)
    w.Begin t0
    Assert.True w.IsWaiting
    Assert.True(w.TimeOut())
    Assert.False w.IsWaiting
    Assert.Contains("主机版本较旧", BoundedWaits.devicesTimeoutText)

[<Fact>]
let ``回复先到，超时不再生效；重新请求重新计时`` () =
    let w = BoundedWait(BoundedWaits.doctorLimit)
    w.Begin t0
    Assert.True(w.Settle())
    Assert.False(w.TimeOut())
    Assert.False(w.Tick(t0.AddMinutes 1.0))
    w.Begin(t0.AddSeconds 10.0)
    Assert.False(w.Tick(t0.AddSeconds 39.9))
    Assert.True w.IsWaiting
    Assert.True(w.Tick(t0.AddSeconds 40.0))
    Assert.False(w.Tick(t0.AddSeconds 41.0))

[<Fact>]
let ``上限与旧代码相同`` () =
    Assert.Equal(TimeSpan.FromSeconds 8.0, BoundedWaits.devicesLimit)
    Assert.Equal(TimeSpan.FromSeconds 30.0, BoundedWaits.doctorLimit)
