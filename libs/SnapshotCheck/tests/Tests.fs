module Tests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``snapshot 检查文字与旧实现一致`` () =
    Assert.Equal((true, "no snapshot (full replay on next start)"), SnapshotCheck.run SnapshotState.Absent)
    Assert.Equal((true, "valid through commit 7 (3 commits replayed after it)"), SnapshotCheck.run (SnapshotState.Used(7UL, 3)))
    Assert.Equal((true, "discarded: bad (falls back to full replay)"), SnapshotCheck.run (SnapshotState.Discarded "bad"))
    Assert.Equal((true, "not checked: m"), SnapshotCheck.run (SnapshotState.NotChecked "m"))
