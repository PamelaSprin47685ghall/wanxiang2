module Tests

open System
open System.IO
open Xunit
open Wanxiang.Client

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "reconnectloop.txt"))

/// 金样由旧代码（旧 AppShell.fs 连接与重连记账，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

let mk () = ReconnectLoop(1000, Samples.next)

[<Fact>]
let ``backoff grows across failed reconnects and only resets on success or manual`` () =
    let r = mk ()
    let delays =
        [ for _ in 1..6 do
            r.Connect() |> ignore
            let t, d = (r.Schedule true).Value
            Assert.True(r.Fire t)
            yield d ]
    Assert.Equal<int list>([ 1000; 2000; 4000; 8000; 16000; 30000 ], delays)
    r.ResetDelay()
    Assert.Equal(1000, r.DelayMs)

[<Fact>]
let ``at most one pending reconnect, none without a token`` () =
    let r = mk ()
    Assert.Equal(None, r.Schedule false)
    Assert.True((r.Schedule true).IsSome)
    Assert.Equal(None, r.Schedule true)

[<Fact>]
let ``a new connect voids the pending timer and stale callbacks`` () =
    let r = mk ()
    let a = r.Connect()
    let t, _ = (r.Schedule true).Value
    let b = r.Connect()
    Assert.False(r.Fire t)
    Assert.False(r.IsCurrent a)
    Assert.True(r.IsCurrent b)

[<Fact>]
let ``resume runs the pending reconnect now and resets backoff`` () =
    let r = mk ()
    r.Schedule true |> ignore
    r.Schedule true |> ignore
    Assert.True(r.ResumeNow())
    Assert.Equal(1000, r.DelayMs)
    Assert.False(r.ResumeNow())
