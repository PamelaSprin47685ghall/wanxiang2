module BoundedCacheTests
open Xunit
open Wanxiang.UI

// 以下迁自旧 UiPureLogicTests（RichBackend.Cache 改为 BoundedCache）。
[<Fact>]
let ``RichBackend Cache stores and evicts by capacity`` () =
    let cache = BoundedCache<string, int>(2)
    cache.Set("a", 1)
    cache.Set("b", 2)
    Assert.Equal(Some 1, cache.TryGet("a"))
    Assert.Equal(Some 2, cache.TryGet("b"))

    // 触发容量淘汰
    cache.Set("c", 3)
    Assert.Equal(Some 3, cache.TryGet("c"))
    // 超出容量 2，最早的 "a" 被淘汰
    Assert.True(cache.TryGet("a").IsNone)


// 本库补充。

[<Fact>]
let ``GetOrAdd 命中时不再计算`` () =
    let c = BoundedCache<int, int>(4)
    let mutable n = 0
    let f k = n <- n + 1; k * 10
    Assert.Equal(10, c.GetOrAdd(1, f))
    Assert.Equal(10, c.GetOrAdd(1, f))
    Assert.Equal(1, n)

[<Fact>]
let ``Clear 清空全部`` () =
    let c = BoundedCache<string, int>(4)
    c.Set("a", 1)
    c.Clear()
    Assert.True((c.TryGet "a").IsNone)

[<Fact>]
let ``并发写不越过上限也不抛`` () =
    let c = BoundedCache<int, int>(8)
    System.Threading.Tasks.Parallel.For(0, 1000, fun i -> c.Set(i, i)) |> ignore
    let hits = [ 0 .. 999 ] |> List.filter (fun i -> (c.TryGet i).IsSome)
    Assert.True(hits.Length <= 8)
