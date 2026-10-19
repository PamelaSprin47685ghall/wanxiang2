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

[<Fact>]
let ``NeighborExcluding returns next visible neighbor when active conversation is removed`` () =
    let id1, id2, id3 = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    // 侧栏按 updatedAt 降序：可见顺序为 [ id3; id2; id1 ]
    let order = [ string id3; string id2; string id1 ]
    Assert.Equal(Some id3, PendingDeletion.neighborAfter (Some id2) [ id2 ] order)
    Assert.Equal(Some id1, PendingDeletion.neighborAfter (Some id2) [ id3; id2 ] order)
    Assert.True((PendingDeletion.neighborAfter (Some id2) [ id1; id2; id3 ] order).IsNone)
    Assert.True((PendingDeletion.neighborAfter (Some id1) [ id3 ] order).IsNone)

[<Fact>]
let ``到期跟踪：宽限期到才交出 token，只交一次；撤销的批次不再到期`` () =
    let t0 = DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero)
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    let s1, ba = PendingDeletion.scheduleAt t0 [ a, "甲" ] PendingDeletion.empty
    let s2, bb = PendingDeletion.scheduleAt (t0.AddSeconds 2.0) [ b, "乙" ] s1
    Assert.Equal(Some(t0 + PendingDeletion.grace), PendingDeletion.nextDue s2)
    let s3, early = PendingDeletion.tick (t0.AddSeconds 7.9) s2
    Assert.Empty early
    let s4, due = PendingDeletion.tick (t0.AddSeconds 8.0) s3
    Assert.Equal<Guid list>([ ba.Value.token ], due)
    let s5, again = PendingDeletion.tick (t0.AddSeconds 9.0) s4
    Assert.Empty again
    let s6, ids = PendingDeletion.commit ba.Value.token s5
    Assert.Equal<Guid list>([ a ], ids)
    let s7, _ = PendingDeletion.undo bb.Value.token s6
    Assert.Equal(None, PendingDeletion.nextDue s7)
    let _, late = PendingDeletion.tick (t0.AddSeconds 60.0) s7
    Assert.Empty late
