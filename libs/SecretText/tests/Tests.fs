module Tests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``凭据替换与键值兜底打码`` () =
    Assert.Equal("a *** b ***", SecretText.replaceKnown [ "sk-1"; " " ] "a sk-1 b sk-1")
    let toml = "[p]\n  apiKey = \"x\"\ntoken=1\nname = \"n\"\nsecretish"
    Assert.Equal("[p]\n  apiKey = \"***\"\ntoken= \"***\"\nname = \"n\"\nsecretish", SecretText.maskAssignments [ "apiKey"; "token"; "secret" ] toml)
