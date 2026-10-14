module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "idlesurface.txt"))

/// 金样由旧代码（从 旧 AppShell.fs Refresh 的「没有打开对话」分支，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``connection error shows its text and a reconnect entry`` () =
    let v = IdleSurface.decide false (Some "超时") false true true false
    Assert.Equal(IdleEmpty.ConnectionFailed "超时", v.empty)
    Assert.Equal(Some "重新连接", v.primary)
    Assert.False v.composerEnabled
    Assert.Equal(Some "重新配对", (IdleSurface.decide false (Some "x") true true true false).primary)

[<Fact>]
let ``never connected and reconnecting differ only in wording`` () =
    let first = IdleSurface.decide false None false false true false
    let again = IdleSurface.decide false None false true true false
    Assert.Equal(IdleEmpty.NotConnected, first.empty)
    Assert.Equal(first.primary, again.primary)
    Assert.NotEqual<string>(first.composerReason, again.composerReason)

[<Fact>]
let ``onboarding stays while busy even if the catalog became ready`` () =
    Assert.Equal(IdleEmpty.Onboarding, (IdleSurface.decide true None false true true true).empty)
    Assert.Equal(IdleEmpty.Onboarding, (IdleSurface.decide true None false true false false).empty)

[<Fact>]
let ``ready host lets the user type with the selected model`` () =
    let v = IdleSurface.decide true (Some "旧错误") false true true false
    Assert.Equal(IdleEmpty.NoConversation, v.empty)
    Assert.True v.composerEnabled
    Assert.Equal(None, v.modelLabel)
