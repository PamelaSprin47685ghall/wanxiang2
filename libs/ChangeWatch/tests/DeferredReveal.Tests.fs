module DeferredRevealTests

open System
open Xunit
open Wanxiang.UI

let t0 = DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero)

[<Fact>]
let ``同一对话只武装一次，反复渲染不顺延`` () =
    let r = DeferredReveal<Guid>(DeferredReveal<Guid>.SkeletonDelay)
    let a = Guid.NewGuid()
    Assert.True(r.Arm(a, t0))
    Assert.Equal(Some(t0.AddMilliseconds 300.0), r.Due)
    Assert.False(r.Arm(a, t0.AddMilliseconds 200.0))
    Assert.Equal(Some(t0.AddMilliseconds 300.0), r.Due)
    Assert.True(r.Expired a)

[<Fact>]
let ``到点后不再武装同一对话；解除后可以重新武装`` () =
    let r = DeferredReveal<Guid>(TimeSpan.FromMilliseconds 300.0)
    let a = Guid.NewGuid()
    r.Arm(a, t0) |> ignore
    Assert.True(r.Expired a)
    Assert.False(r.Expired a)
    Assert.False(r.Arm(a, t0.AddSeconds 1.0))
    r.Disarm()
    Assert.True(r.Arm(a, t0.AddSeconds 2.0))

[<Fact>]
let ``快照先到或换了对话，旧计时到点不做事`` () =
    let r = DeferredReveal<Guid>(TimeSpan.FromMilliseconds 300.0)
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    r.Arm(a, t0) |> ignore
    r.Disarm()
    Assert.False(r.Expired a)
    r.Arm(a, t0) |> ignore
    Assert.True(r.Arm(b, t0.AddMilliseconds 100.0))
    Assert.False(r.Expired a)
    Assert.True(r.Expired b)
