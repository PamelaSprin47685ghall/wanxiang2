module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "copyconfirm.txt"))
let t0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

/// 金样由旧代码（旧 Views/MessageCard.fs 计时器结构，照抄）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``copy fades first, holds the check for 1500 ms, then restores`` () =
    let c = CopyConfirm()
    c.Copied(t0, false)
    Assert.Equal(CopyVisual.Fading, c.Tick(t0.AddMilliseconds 10.0))
    Assert.Equal(CopyVisual.Confirmed, c.Tick(t0.AddMilliseconds 150.0))
    Assert.Equal(CopyVisual.Confirmed, c.Tick(t0.AddMilliseconds 1499.0))
    Assert.Equal(CopyVisual.Default, c.Tick(t0.AddMilliseconds 1500.0))
    Assert.Equal(None, c.NextDue)

[<Fact>]
let ``reduced motion skips the fade`` () =
    let c = CopyConfirm()
    c.Copied(t0, true)
    Assert.Equal(CopyVisual.Confirmed, c.Tick t0)
    Assert.Equal(Some(t0.AddMilliseconds 1500.0), c.NextDue)

[<Fact>]
let ``a second copy restarts the hold`` () =
    let c = CopyConfirm()
    c.Copied(t0, false)
    c.Copied(t0.AddMilliseconds 1000.0, false)
    Assert.Equal(CopyVisual.Confirmed, c.Tick(t0.AddMilliseconds 2000.0))
    Assert.Equal(CopyVisual.Default, c.Tick(t0.AddMilliseconds 2500.0))

[<Fact>]
let ``detaching restores the default at once`` () =
    let c = CopyConfirm()
    c.Copied(t0, false)
    c.Detached()
    Assert.Equal(CopyVisual.Default, c.Tick t0)
