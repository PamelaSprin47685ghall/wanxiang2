module StartReadyTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "StartReady", "startready.txt"))

/// 金样由旧代码（从 旧 AppShell.fs Refresh 的 startReady，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), StartReadyRender.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``only a new or empty conversation on a ready idle host can start`` () =
    Assert.True(StartReady.isReady true true false false ActiveConversation.None)
    Assert.True(StartReady.isReady true true false false (ActiveConversation.Loaded 0))
    Assert.False(StartReady.isReady true true false false (ActiveConversation.Loaded 1))
    Assert.False(StartReady.isReady true true false false ActiveConversation.Loading)
    for a, c, g, e in [ false, true, false, false; true, false, false, false; true, true, true, false; true, true, false, true ] do
        Assert.False(StartReady.isReady a c g e ActiveConversation.None)
