module ClientTokenTests

open System
open Xunit
open ClientToken

// SPEC 1：令牌格式：前缀 + base64url 无填充，256 bit，两次不同
[<Fact>]
let ``tokens have the fixed prefix, base64url body and 256 bits`` () =
    let a, b = ClientToken.generate (), ClientToken.generate ()
    Assert.NotEqual<string>(a, b)
    Assert.StartsWith(ClientToken.Prefix, a)
    let raw = a.Substring ClientToken.Prefix.Length
    Assert.Equal(43, raw.Length)
    Assert.True(raw |> Seq.forall (fun c -> Char.IsAsciiLetterOrDigit c || c = '-' || c = '_'))

// SPEC 2：哈希与已存进用户配置的值逐字一致（金标准向量，独立用 Python hashlib 算出）
[<Fact>]
let ``hash matches the golden vector stored in existing configs`` () =
    Assert.Equal(
        "13c36d373dadbf047bf832e383e4d2d88583f0ea741b9b7cda49272adc593f2b",
        ClientToken.hash "wanxiang_client_test_token_12345"
    )

// SPEC 3：matches 只认对应哈希；空值不认
[<Fact>]
let ``matches accepts only the right token`` () =
    let t = ClientToken.generate ()
    let h = ClientToken.hash t
    Assert.True(ClientToken.matches t h)
    Assert.False(ClientToken.matches (t + "x") h)
    Assert.False(ClientToken.matches null h)
    Assert.False(ClientToken.matches t null)
    Assert.True(ClientToken.fixedTimeEquals "abc" "abc")
    Assert.False(ClientToken.fixedTimeEquals "abc" "ab")

// SPEC 4：deviceId 是哈希前 16 位
[<Fact>]
let ``device id is the first 16 hex chars of the hash`` () =
    Assert.Equal(
        "13c36d373dadbf04",
        ClientToken.deviceId "13c36d373dadbf047bf832e383e4d2d88583f0ea741b9b7cda49272adc593f2b"
    )

    Assert.Equal("abc", ClientToken.deviceId "abc")
    Assert.Equal("", ClientToken.deviceId null)
