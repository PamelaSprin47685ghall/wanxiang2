module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "startlayout.txt"))

/// 金样由旧代码（旧 Views/MainLayout.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``centered only when starting and tall enough`` () =
    Assert.True(StartLayout.centered true 560.0 560.0)
    Assert.False(StartLayout.centered true 559.0 560.0)
    Assert.False(StartLayout.centered false 900.0 560.0)

[<Fact>]
let ``sidebar width is clamped`` () =
    Assert.Equal(232.0, StartLayout.sidebarWidth 100.0 232.0 420.0)
    Assert.Equal(420.0, StartLayout.sidebarWidth 999.0 232.0 420.0)
    Assert.Equal(300.0, StartLayout.sidebarWidth 300.0 232.0 420.0)
