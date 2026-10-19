module HostRequestsTests
open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由 tools/ 下原样摘出的旧代码跑同一份 HostRequestsRender.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "HostRequests", "expected.txt")), HostRequestsRender.all ())

[<Fact>]
let ``W4 只认最近一次查询，迟到的旧结果丢掉`` () =
    let s = SearchInFlight()
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    s.Sent(a, DateTimeOffset.UtcNow)
    s.Sent(b, DateTimeOffset.UtcNow)
    Assert.False(s.Accept a)
    Assert.True(s.Accept b)

[<Fact>]
let ``443 到点的探测按发出顺序收尾，已回应的不再收尾`` () =
    let p = PendingProbes()
    let t = DateTimeOffset.UtcNow
    let a, b, c = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    p.Track(a, "x", t); p.Track(b, "y", t); p.Track(c, "z", t)
    p.Resolve b
    Assert.Equal<(Guid * string) list>([ a, "x"; c, "z" ], p.Expire(t.AddMinutes 1.0, TimeSpan.FromSeconds 15.0))
    Assert.Empty(p.Expire(t.AddMinutes 2.0, TimeSpan.FromSeconds 15.0))
