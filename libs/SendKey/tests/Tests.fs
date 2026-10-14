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

// ---- 文档规则的真实行为

[<Fact>]
let ``send and newline are complementary in every mode`` () =
    for enterSends in [ true; false ] do
        // 不按 Shift 时，裸 Enter 与 Ctrl+Enter 恰有一个发送
        Assert.NotEqual(SendKey.sends enterSends false false, SendKey.sends enterSends false true)
        // 当前模式的发送键就是 enterSends 所指的那个
        Assert.Equal(enterSends, SendKey.sends enterSends false false)
        for ctrl in [ true; false ] do Assert.False(SendKey.sends enterSends true ctrl)
