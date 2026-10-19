module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``any unicode digits become six ascii digits`` () =
    Assert.Equal("123456", PairingCodeInput.normalize "１２３ ٤٥٦ 7")
    Assert.Equal("", PairingCodeInput.normalize null)

[<Fact>]
let ``the same full code submits once until it changes`` () =
    let a = PairingAutoSubmit()
    Assert.True(a.Changed "111111")
    Assert.False(a.Changed "111111")
    Assert.False(a.Changed "11111")
    Assert.True(a.Changed "111111")
