module RevealFlashTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "RevealFlash", "revealflash.txt"))

/// 金样由旧代码（旧 Views/ChatView.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), RevealFlashRender.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``a new highlight restores the previous one first`` () =
    let f = RevealFlash()
    f.Revealed 1UL
    Assert.Equal(None, f.Begin 1UL)
    Assert.Equal(Some 1UL, f.Begin 2UL)
    Assert.Equal(Some 2UL, f.End())
    Assert.Equal(None, f.End())
    Assert.Equal(Some 1UL, f.LastFlashed)
