module SnapshotCheckTests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``snapshot 检查文字与旧实现一致`` () =
    Assert.Equal((true, "no snapshot (full replay on next start)"), SnapshotCheck.run SnapshotState.Absent)

    Assert.Equal(
        (true, "valid through commit 7 (3 commits replayed after it)"),
        SnapshotCheck.run (SnapshotState.Used(7UL, 3))
    )

    Assert.Equal(
        (true, "discarded: bad (falls back to full replay)"),
        SnapshotCheck.run (SnapshotState.Discarded "bad")
    )

    Assert.Equal((true, "not checked: m"), SnapshotCheck.run (SnapshotState.NotChecked "m"))

[<Fact>]
let ``快照问题从不算失败`` () =
    for s in
        [
            SnapshotState.Absent
            SnapshotState.Used(0UL, 0)
            SnapshotState.Discarded ""
            SnapshotState.NotChecked ""
        ] do
        Assert.True(fst (SnapshotCheck.run s))

[<Fact>]
let ``最大提交号原样输出`` () =
    Assert.Equal(
        "valid through commit 18446744073709551615 (0 commits replayed after it)",
        snd (SnapshotCheck.run (SnapshotState.Used(System.UInt64.MaxValue, 0)))
    )
