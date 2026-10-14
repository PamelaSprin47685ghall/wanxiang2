module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "conversationsurface.txt"))

/// 金样由旧代码（旧 AppShell.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``an empty conversation shows the empty card and no tail error`` () =
    let v = ConversationSurface.decide true false false false true true true false
    Assert.True v.showEmpty
    Assert.Equal(TailError.None, v.tailError)

[<Fact>]
let ``run error beats unanswered; unanswered needs an idle tail`` () =
    Assert.Equal(TailError.RunError, (ConversationSurface.decide false false true false false true true false).tailError)
    Assert.Equal(TailError.Unanswered, (ConversationSurface.decide false false false false false true true false).tailError)
    Assert.Equal(TailError.None, (ConversationSurface.decide false false false true false true true false).tailError)
    Assert.Equal(TailError.None, (ConversationSurface.decide false false false false true true true false).tailError)

[<Fact>]
let ``disconnected composer and stop`` () =
    let v = ConversationSurface.decide false false false false true false false true
    Assert.False v.composerEnabled
    Assert.Equal("连接已断开，重新连接后可继续发送。", v.composerReason)
    Assert.False v.canStop
    Assert.Equal("已连接 · 配置有误", ConversationSurface.hostStatusLine true)
