module Tests

open System
open Xunit
open Wanxiang.UI

// 迁自旧 R27HostCapabilitiesTests。
[<Fact>]
let ``config tracker expires silent requests and resolves only once`` () =
    let tracker = ConfigFeedbackTracker()
    let id = Guid.NewGuid()
    let mutable result = None
    tracker.Track(id, "ok", fun ok -> result <- Some ok)
    Assert.Empty(tracker.Expire(DateTimeOffset.UtcNow, TimeSpan.FromSeconds 12.0))
    let expired = tracker.Expire(DateTimeOffset.UtcNow.AddSeconds 13.0, TimeSpan.FromSeconds 12.0)
    Assert.Equal(1, expired.Length)
    expired |> List.iter (fun f -> f.onCompleted false)
    Assert.Equal(Some false, result)
    Assert.True((tracker.Resolve id).IsNone)
    Assert.Equal(0, tracker.Count)

