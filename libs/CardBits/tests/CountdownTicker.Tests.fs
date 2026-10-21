module CountdownTickerTests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``ready is announced once and the timer stops at zero`` () =
    let t = CountdownTicker()
    Assert.True((t.Refresh 2).KeepTicking)
    let f = t.Refresh 0
    Assert.True(f.Ready && f.Announce && not f.KeepTicking)
    Assert.False((t.Refresh 0).Announce)
    Assert.False((t.Refresh -1).Announce)
