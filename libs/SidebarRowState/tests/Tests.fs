module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "sidebarrowstate.txt"))

/// 金样由旧代码（旧 Views/Sidebar.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``selection mode takes the more button offline`` () =
    Assert.Equal({ visible = false; fullOpacity = false; interactive = false }, SidebarRowState.moreButton true true)
    Assert.Equal({ visible = true; fullOpacity = false; interactive = true }, SidebarRowState.moreButton false false)
    Assert.Equal("当前对话", SidebarRowState.itemStatus (SidebarRowState.highlighted false true))
