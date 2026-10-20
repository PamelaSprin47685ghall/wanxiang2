module GenerationErrorTitleTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``raw exception text falls back to a fixed title`` () =
    Assert.Equal("生成失败", GenerationErrorTitle.ofMessage "System.Exception: x\n   at A.B()")

[<Fact>]
let ``human message is kept trimmed`` () =
    Assert.Equal("额度用完了", GenerationErrorTitle.ofMessage " 额度用完了 ")
