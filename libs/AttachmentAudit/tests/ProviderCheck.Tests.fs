module ProviderCheckTests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``provider 检查文字与旧实现一致`` () =
    Assert.Equal((true, "ok"), ProviderCheck.run [ "a", true, true, "http://x"; "b", false, false, "" ])

    Assert.Equal(
        (false, "a: enabled but no apiKey; enabled but empty baseUrl; c: enabled but no apiKey"),
        ProviderCheck.run [ "a", true, false, " "; "b", true, true, "u"; "c", true, false, "u" ]
    )

[<Fact>]
let ``没有提供方时通过`` () =
    Assert.Equal((true, "ok"), ProviderCheck.run [])

[<Fact>]
let ``停用的提供方不检查`` () =
    Assert.Equal((true, "ok"), ProviderCheck.run [ "p", false, false, "" ])

[<Fact>]
let ``同一提供方两个问题合并，多个提供方按顺序分号连接`` () =
    Assert.Equal(
        (false, "a: enabled but no apiKey; enabled but empty baseUrl; b: enabled but empty baseUrl"),
        ProviderCheck.run [ "a", true, false, " "; "ok", true, true, "u"; "b", true, true, "" ]
    )
