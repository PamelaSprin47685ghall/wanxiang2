module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "generationerrortitle.txt"))

/// 金样由旧代码（从 旧 Views/MessageCard.fs 的 errorTitle 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

[<Fact>]
let ``raw exception text falls back to a fixed title`` () =
    Assert.Equal("生成失败", GenerationErrorTitle.ofMessage "System.Exception: x\n   at A.B()")

[<Fact>]
let ``human message is kept trimmed`` () =
    Assert.Equal("额度用完了", GenerationErrorTitle.ofMessage " 额度用完了 ")
