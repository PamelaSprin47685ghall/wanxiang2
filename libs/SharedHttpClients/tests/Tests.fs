module SharedHttpClientsTests

open Xunit
open Wanxiang.Agent

[<Fact>]
let ``same parameters share a client, different ones do not`` () =
    let pool = SharedHttpClients()
    let a = pool.Get("p", 10, Map [ "X", "1" ])
    Assert.Same(a, pool.Get("p", 10, Map [ "X", "1" ]))
    Assert.NotSame(a, pool.Get("p", 11, Map [ "X", "1" ]))
    Assert.NotSame(a, pool.Get("p", 10, Map.empty))
    Assert.Equal(100.0, a.Timeout.TotalSeconds)
    Assert.True(a.DefaultRequestHeaders.Contains "X")
