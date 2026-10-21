module RowFocusTests
open System
open Xunit
open Wanxiang.UI

// 迁自旧 KeyConvergenceTests（原用例由真实侧栏排出行序；这里直接给出同样的顺序：三二一）。
[<Fact>]
let ``neighbor after delete matches the row focus falls to`` () =
    let first, second, third = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let f = RowFocus()
    f.ApplyVisible [| third; second; first |]
    Assert.Equal(Some first, f.NeighborAfterDelete(second.ToString()))
    Assert.Equal(Some second, f.NeighborAfterDelete(first.ToString()))
    // 打开谁与焦点落谁同一个答案
    Assert.Equal(RowFocusTarget.Row(2, first), f.FocusAfterDelete(second.ToString()))
    Assert.Equal(Some first, f.FocusedId)

[<Fact>]
let ``no neighbor when the list becomes empty`` () =
    let only = Guid.NewGuid()
    let f = RowFocus()
    f.ApplyVisible [| only |]
    Assert.Equal(None, f.NeighborAfterDelete(only.ToString()))
    Assert.Equal(RowFocusTarget.Search, f.FocusAfterDelete(only.ToString()))

[<Fact>]
let ``删除后才问（id 已不在）：按上次行焦点钳制`` () =
    let a, b, c = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let f = RowFocus()
    f.ApplyVisible [| a; b; c |]
    f.Focused c
    f.ApplyVisible [| a; b |]
    Assert.Equal(Some b, f.NeighborAfterDelete(Guid.NewGuid().ToString()))
    Assert.Equal(Some b, f.NeighborAfterDelete "not-a-guid")
    f.ApplyVisible [||]
    Assert.Equal(None, f.NeighborAfterDelete "x")

[<Fact>]
let ``重建：删除意图优先且只用一次，其次还原原焦点行`` () =
    let a, b, c = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let f = RowFocus()
    f.ApplyVisible [| a; b; c |]
    f.FocusAfterDelete(b.ToString()) |> ignore
    f.ApplyVisible [| a; c |]
    Assert.Equal(Some c, f.RestoreAfterRebuild(Some a))
    Assert.Equal(Some a, f.RestoreAfterRebuild(Some a))
    // 意图指向的行不见了：意图作废，不还原到别处
    Assert.Equal(RowFocusTarget.Row(1, c), f.FocusAfterDelete(a.ToString()))
    f.ApplyVisible [| a |]
    Assert.Equal(None, f.RestoreAfterRebuild(Some b))
    // 意图作废后仍还原原焦点行（与旧 Rebuild 相同）
    f.FocusAfterDelete(a.ToString()) |> ignore
    f.ApplyVisible [| b |]
    Assert.Equal(Some b, f.RestoreAfterRebuild(Some b))
