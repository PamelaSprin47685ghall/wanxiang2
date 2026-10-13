module RefreshingPoolTests

open Xunit
open Wanxiang.Server

type Item(n: int) =
    member val Stopped = false with get, set
    member _.N = n

[<Fact>]
let ``same fingerprint reuses, a new one replaces and stops the old`` () =
    let pool = RefreshingPool<Item>(fun i -> i.Stopped <- true)
    let a = (pool.Acquire("k", Some("1", fun () -> Item 1))).Value
    let a2 = (pool.Acquire("k", Some("1", fun () -> Item 9))).Value
    Assert.Same(a, a2)
    let b = (pool.Acquire("k", Some("2", fun () -> Item 2))).Value
    Assert.True(a.Stopped)
    Assert.Equal(2, b.N)
    Assert.Equal(None, pool.Acquire("k", None))
    Assert.True(b.Stopped)

[<Fact>]
let ``prune stops keys that are no longer live and stopAll stops the rest`` () =
    let pool = RefreshingPool<Item>(fun i -> i.Stopped <- true)
    let a = (pool.Acquire("a", Some("1", fun () -> Item 1))).Value
    let b = (pool.Acquire("b", Some("1", fun () -> Item 2))).Value
    pool.Prune((=) "b")
    Assert.True(a.Stopped)
    Assert.False(b.Stopped)
    pool.StopAll()
    Assert.True(b.Stopped)
