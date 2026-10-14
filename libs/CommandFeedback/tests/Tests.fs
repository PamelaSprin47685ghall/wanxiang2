module Tests

open System
open Xunit
open Wanxiang.UI

// 迁自旧 R27HostCapabilitiesTests：命令换成 invocationId + 是否可超时（由命令得出这两项在组合者 Outbox，那里用真实命令再核对一次）。
[<Fact>]
let ``command tracker expires newer commands but leaves message delivery to outbox`` () =
    let tracker = CommandFeedbackTracker()
    tracker.TrackId(Guid.NewGuid(), true, None, ignore)
    tracker.TrackId(Guid.NewGuid(), false, None, ignore)
    let expired = tracker.Expire(DateTimeOffset.UtcNow.AddMinutes 1.0, TimeSpan.FromSeconds 12.0)
    Assert.Equal(1, expired.Length)
    Assert.Equal(1, tracker.Count)

[<Fact>]
let ``commit and reject hand back the feedback once and reject all completes with false`` () =
    let tracker = CommandFeedbackTracker()
    let a, b = Guid.NewGuid(), Guid.NewGuid()
    let mutable completed = []
    tracker.TrackIdWithCompletion(a, true, Some "好了", ignore, fun ok -> completed <- ("a", ok) :: completed)
    tracker.TrackIdWithCompletion(b, true, None, ignore, fun ok -> completed <- ("b", ok) :: completed)
    Assert.Equal(Some "好了", (tracker.Commit a).Value.successMessage)
    Assert.True((tracker.Commit a).IsNone)
    tracker.RejectAll()
    Assert.Equal<(string * bool) list>([ "b", false ], completed)
    Assert.Equal(0, tracker.Count)
