module CommitTimelineTests
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Client

let private arr (s: string) = JsonNode.Parse(s).AsArray()

[<Fact>]
let ``commitIdOf 只认对象里的 commitId`` () =
    Assert.Equal(Some 3UL, CommitTimeline.commitIdOf (JsonNode.Parse """{"commitId":3}"""))
    Assert.Equal(None, CommitTimeline.commitIdOf (JsonNode.Parse """{"commitId":null}"""))
    Assert.Equal(None, CommitTimeline.commitIdOf (JsonNode.Parse """{"x":1}"""))
    Assert.Equal(None, CommitTimeline.commitIdOf (JsonNode.Parse "7"))
    Assert.Equal(None, CommitTimeline.commitIdOf null)

[<Fact>]
let ``contains 按 commitId 判断，跳过非对象项`` () =
    let a = arr """[1,"s",{"commitId":5}]"""
    Assert.True(CommitTimeline.contains 5UL a)
    Assert.False(CommitTimeline.contains 1UL a)

[<Fact>]
let ``without 删除匹配项，保留非对象项与顺序，不改原数组`` () =
    let a = arr """[{"commitId":1},"x",{"commitId":2},{"commitId":1}]"""
    let b = CommitTimeline.without 1UL a
    Assert.Equal("""["x",{"commitId":2}]""", b.ToJsonString())
    Assert.Equal(4, a.Count)

[<Fact>]
let ``prependOlder 去重、丢弃无 commitId 的项并报告最早一条`` () =
    let cur = arr """[{"commitId":5},{"commitId":6}]"""
    let older = arr """[{"commitId":3},{"commitId":5},"junk",{"commitId":0},{"commitId":4}]"""
    let merged, earliest = CommitTimeline.prependOlder older cur
    Assert.Equal("""[{"commitId":3},{"commitId":4},{"commitId":5},{"commitId":6}]""", merged.ToJsonString())
    Assert.Equal(Some 3UL, earliest)

[<Fact>]
let ``prependOlder 没有新项时最早一条为 None，现有列表原样保留`` () =
    let cur = arr """[{"commitId":5},"keep"]"""
    let merged, earliest = CommitTimeline.prependOlder (arr """[{"commitId":5}]""") cur
    Assert.Equal("""[{"commitId":5},"keep"]""", merged.ToJsonString())
    Assert.Equal(None, earliest)

[<Fact>]
let ``commitId 不是整数时抛出（与旧实现一致）`` () =
    Assert.ThrowsAny<exn>(fun () -> CommitTimeline.contains 1UL (arr """[{"commitId":"1"}]""") |> ignore) |> ignore
