module SidebarSelectionTests
open System
open System.IO
open Xunit
open Wanxiang.UI


open SidebarSelectionSamples

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
