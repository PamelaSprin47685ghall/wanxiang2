module Tests

open System
open Xunit
open Wanxiang.UI

// 迁自旧 LibraryUndoTests。
[<Fact>]
let ``§214 待删批次：隐去、撤销恢复、撤销后不再提交`` () =
    let a, b, c = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let s1, batch = PendingDeletion.schedule [ a, "甲"; b, "乙" ] PendingDeletion.empty
    let batch = batch.Value
    Assert.Equal("已删除 2 个对话", batch.label)
    Assert.True(PendingDeletion.isPending a s1)
    Assert.False(PendingDeletion.isPending c s1)
    let s2, restored = PendingDeletion.undo batch.token s1
    Assert.Equal<Guid list>([ a; b ], restored)
    Assert.False(PendingDeletion.isPending a s2)
    let _, committed = PendingDeletion.commit batch.token s2
    Assert.Empty committed

[<Fact>]
let ``§214 到期提交只交出一次；重复登记不重复删`` () =
    let a = Guid.NewGuid()
    let s1, batch = PendingDeletion.schedule [ a, "甲" ] PendingDeletion.empty
    Assert.Equal("已删除「甲」", batch.Value.label)
    let again, dup = PendingDeletion.schedule [ a, "甲" ] s1
    Assert.True dup.IsNone
    let s2, ids = PendingDeletion.commit batch.Value.token again
    Assert.Equal<Guid list>([ a ], ids)
    let _, twice = PendingDeletion.commit batch.Value.token s2
    Assert.Empty twice
