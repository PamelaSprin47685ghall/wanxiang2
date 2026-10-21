module SecretTextTests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``凭据替换与键值兜底打码`` () =
    Assert.Equal("a *** b ***", SecretText.replaceKnown [ "sk-1"; " " ] "a sk-1 b sk-1")
    let toml = "[p]\n  apiKey = \"x\"\ntoken=1\nname = \"n\"\nsecretish"

    Assert.Equal(
        "[p]\n  apiKey = \"***\"\ntoken= \"***\"\nname = \"n\"\nsecretish",
        SecretText.maskAssignments [ "apiKey"; "token"; "secret" ] toml
    )

[<Fact>]
let ``空白或空的凭据不替换任何东西`` () =
    Assert.Equal("a b", SecretText.replaceKnown [ ""; "  " ] "a b")

[<Fact>]
let ``按给定顺序替换：短密钥在前会留下长密钥的尾巴`` () =
    Assert.Equal("***2", SecretText.replaceKnown [ "abc"; "abc2" ] "abc2")
    Assert.Equal("***", SecretText.replaceKnown [ "abc2"; "abc" ] "abc2")

[<Fact>]
let ``没有等号的行和不匹配前缀的行原样保留`` () =
    let s = "apiKeyNote\n# token = x\nuser = \"u\""
    Assert.Equal(s, SecretText.maskAssignments [ "apiKey"; "token" ] s)

[<Fact>]
let ``保留缩进，值里的等号也一并打码`` () =
    Assert.Equal("  token = \"***\"\n", SecretText.maskAssignments [ "token" ] "  token = 'a=b'\n")

[<Fact>]
let ``空文本不出错`` () =
    Assert.Equal("", SecretText.maskAssignments [ "apiKey" ] "")
    Assert.Equal("", SecretText.replaceKnown [ "x" ] "")
