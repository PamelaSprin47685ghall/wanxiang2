module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "sidebarselection.txt"))

/// 金样由旧代码（旧 Views/Sidebar.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

open Samples

[<Fact>]
let ``clearing the last item leaves selection mode`` () =
    let s = SidebarSelection()
    s.Enter a
    s.Toggle b
    s.Toggle a
    Assert.True s.Active
    s.Toggle b
    Assert.False s.Active

[<Fact>]
let ``select all toggles and replaces hidden picks`` () =
    let s = SidebarSelection()
    s.Enter d
    s.ToggleAllVisible [ a; b ]
    Assert.Equal(2, s.Count)
    Assert.Equal("取消全选", (s.Chrome([ a; b ], allPinned)).selectAllText)
    s.ToggleAllVisible [ a; b ]
    Assert.False s.Active

[<Fact>]
let ``pin text and batch keys follow visible picks only`` () =
    let s = SidebarSelection()
    s.Enter a
    s.Enter b
    let ch = s.Chrome([ a; b; c ], allPinned)
    Assert.Equal("取消置顶", ch.pinText)
    Assert.True ch.batchEnabled
    let gone = s.Chrome([ c ], allPinned)
    Assert.False gone.batchEnabled
    Assert.Equal("已选 2 项", gone.countText)
