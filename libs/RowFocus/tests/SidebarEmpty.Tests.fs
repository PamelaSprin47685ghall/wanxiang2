module SidebarEmptyTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "SidebarEmpty", "sidebarempty.txt"))

/// 金样由旧代码（旧 Views/Sidebar.fs Rebuild，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), SidebarEmptyRender.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``search with no hit shows the search hint, not the card`` () =
    let v = SidebarEmpty.decide "x" true false true false false
    Assert.True v.searchEmpty
    Assert.False v.hostVisible

[<Fact>]
let ``three kinds are exclusive: disconnected beats loading beats none`` () =
    Assert.Equal(Some SidebarEmptyKind.NotConnected, (SidebarEmpty.decide "" true false false true false).kind)
    let l = SidebarEmpty.decide "" true false true true false
    Assert.Equal(Some SidebarEmptyKind.ListLoading, l.kind)
    Assert.True l.skeletonVisible
    Assert.Equal(Some SidebarEmptyKind.NoConversations, (SidebarEmpty.decide "" true false true false false).kind)

[<Fact>]
let ``the connect button shows only in compact mode`` () =
    Assert.False (SidebarEmpty.decide "" true false false false false).actionVisible
    Assert.True (SidebarEmpty.decide "" true false false false true).actionVisible

[<Fact>]
let ``a non-empty list or a hit section has no empty state`` () =
    Assert.Equal(None, (SidebarEmpty.decide "" false false false false false).kind)
    Assert.Equal(None, (SidebarEmpty.decide "x" true true false false false).kind)
