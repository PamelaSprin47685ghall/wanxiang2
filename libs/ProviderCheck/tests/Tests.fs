module Tests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``provider 检查文字与旧实现一致`` () =
    Assert.Equal((true, "ok"), ProviderCheck.run [ "a", true, true, "http://x"; "b", false, false, "" ])
    Assert.Equal((false, "a: enabled but no apiKey; enabled but empty baseUrl; c: enabled but no apiKey"),
                 ProviderCheck.run [ "a", true, false, " "; "b", true, true, "u"; "c", true, false, "u" ])
