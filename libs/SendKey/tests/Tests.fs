module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "sendkey.txt"))

/// 金样由旧代码（从 旧 Views/Composer.fs 的回车分支 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

[<Fact>]
let ``shift enter never sends`` () =
    Assert.False(SendKey.sends true true false)
    Assert.False(SendKey.sends false true true)

[<Fact>]
let ``ctrl enter is the newline key when enter sends`` () =
    Assert.True(SendKey.sends true false false)
    Assert.False(SendKey.sends true false true)
    Assert.True(SendKey.sends false false true)
    Assert.False(SendKey.sends false false false)
