module ConfigFeedbackTests

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

    let expired =
        tracker.Expire(DateTimeOffset.UtcNow.AddSeconds 13.0, TimeSpan.FromSeconds 12.0)

    Assert.Equal(1, expired.Length)
    expired |> List.iter (fun f -> f.onCompleted false)
    Assert.Equal(Some false, result)
    Assert.True((tracker.Resolve id).IsNone)
    Assert.Equal(0, tracker.Count)


[<Fact>]
let ``首次运行卡片登记空文案：成功时不叠 toast 但仍拉目录`` () =
    Assert.Equal((ConfigAppliedNotice.Silent, true), ConfigApplied.notice (Some "") true [])
    Assert.Equal((ConfigAppliedNotice.Failure "a；b", false), ConfigApplied.notice None false [ "a"; "b" ])

// 迁自旧 AppShell 的 ConfigChanged / HostConfigRejected 分支：旧代码只在恢复、首次有误时 toast。
[<Fact>]
let ``config issue notices fire only on transitions`` () =
    Assert.Equal(Some "主机配置已恢复正常。", ConfigIssueNotice.recovered true)
    Assert.Equal(None, ConfigIssueNotice.recovered false)
    Assert.Equal(Some "坏了", ConfigIssueNotice.rejected false "坏了")
    Assert.Equal(None, ConfigIssueNotice.rejected true "坏了")
