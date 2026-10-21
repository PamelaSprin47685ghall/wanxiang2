module RowFocusSidebarSelectionTests
open System
open Xunit
open Wanxiang.UI

open RowFocusSidebarSelectionSamples

// 多选的事实在状态树（`Chrome.BatchMode` / `Chrome.SelectedRowIds`）。
// 这里只测侧栏侧**唯一剩下的派生读法**：按可见行过滤选中项。
// 「进多选 / 切换 / 全选 / 退出」的规则回归在 `libs/WorkspaceState/tests/WorkspaceStateTests.fs`。

[<Fact>]
let ``selected ids follow the visible rows only`` () =
    let selected = Set.ofList [ a; b ]
    Assert.Equal<Guid list>([ a; b ], SidebarSelection.SelectedIds [ a; b; c ] selected)
    // c 不可见：不在结果里
    Assert.Equal<Guid list>([ a ], SidebarSelection.SelectedIds [ a; c ] selected)

[<Fact>]
let ``selected ids keep the visible row order`` () =
    let selected = Set.ofList [ b; a ]
    Assert.Equal<Guid list>([ a; b ], SidebarSelection.SelectedIds [ a; b ] selected)

[<Fact>]
let ``nothing selected yields nothing`` () =
    Assert.Equal<Guid list>([], SidebarSelection.SelectedIds [ a; b ] Set.empty)
