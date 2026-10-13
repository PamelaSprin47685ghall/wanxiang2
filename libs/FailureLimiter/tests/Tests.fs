module FailureLimiterTests

open System
open Xunit
open FailureLimiter

let t0 = DateTimeOffset.Parse "2026-01-01T12:00:00Z"
let s (n: float) = TimeSpan.FromSeconds n

// SPEC 1：窗口内达到阈值冻结；键之间隔离；冻结期满解除；Clear 立即解除
[<Fact>]
let ``freezes at the threshold per key, expires, clears`` () =
    let l = FailureLimiter(TimeSpan.FromMinutes 1.0, 3, TimeSpan.FromMinutes 5.0)
    Assert.False(l.RecordFailure(t0, "a"))
    Assert.False(l.RecordFailure(t0 + s 10.0, "a"))
    Assert.True(l.RecordFailure(t0 + s 20.0, "a"))
    Assert.Equal(Some(t0 + s 20.0 + TimeSpan.FromMinutes 5.0), l.FrozenUntil(t0 + s 20.0, "a"))
    Assert.False(l.IsFrozen(t0 + s 20.0, "b"))
    Assert.True(l.IsFrozen(t0 + TimeSpan.FromMinutes 4.0, "a"))
    Assert.False(l.IsFrozen(t0 + s 20.0 + TimeSpan.FromMinutes 5.0, "a"))
    for i in 0..2 do l.RecordFailure(t0 + TimeSpan.FromMinutes 10.0 + s (float i), "a") |> ignore
    Assert.True(l.IsFrozen(t0 + TimeSpan.FromMinutes 10.0 + s 2.0, "a"))
    l.Clear "a"
    Assert.False(l.IsFrozen(t0 + TimeSpan.FromMinutes 10.0 + s 2.0, "a"))

// SPEC 2：窗口外的旧失败不累计
[<Fact>]
let ``old failures slide out of the window`` () =
    let l = FailureLimiter(TimeSpan.FromMinutes 1.0, 3, TimeSpan.FromMinutes 5.0)
    l.RecordFailure(t0, "a") |> ignore
    l.RecordFailure(t0 + s 10.0, "a") |> ignore
    Assert.False(l.RecordFailure(t0 + TimeSpan.FromMinutes 2.0, "a"))

// SPEC 3：大量不同键时内存有界：过期条目被清掉
[<Fact>]
let ``many distinct keys stay bounded`` () =
    let l = FailureLimiter(TimeSpan.FromMinutes 1.0, 3, TimeSpan.FromMinutes 5.0, 100)
    for i in 1..1000 do l.RecordFailure(t0 + s (float i), sprintf "k%d" i) |> ignore
    Assert.True(l.TrackedKeys <= 161, sprintf "tracked %d" l.TrackedKeys)

// SPEC 4：非法参数拒绝构造
[<Fact>]
let ``max failures must be positive`` () =
    Assert.Throws<ArgumentException>(fun () -> FailureLimiter(TimeSpan.FromMinutes 1.0, 0, TimeSpan.Zero) |> ignore) |> ignore
