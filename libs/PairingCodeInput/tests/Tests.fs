module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "pairingcodeinput.txt"))

/// 金样由旧代码（旧 Views/Dialogs.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

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
