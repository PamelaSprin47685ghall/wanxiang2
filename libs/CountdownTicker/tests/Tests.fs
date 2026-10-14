module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "countdownticker.txt"))

/// 金样由旧代码（旧 Views/MessageCard.fs refresh，照抄）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``ready is announced once and the timer stops at zero`` () =
    let t = CountdownTicker()
    Assert.True((t.Refresh 2).KeepTicking)
    let f = t.Refresh 0
    Assert.True(f.Ready && f.Announce && not f.KeepTicking)
    Assert.False((t.Refresh 0).Announce)
    Assert.False((t.Refresh -1).Announce)
