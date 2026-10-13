module Tests

open Xunit
open Wanxiang.Client

[<Fact>]
let ``等待时长逐次翻倍`` () =
    Assert.Equal(2000, ReconnectBackoff.next 1000)
    Assert.Equal(4000, ReconnectBackoff.next 2000)
    Assert.Equal(8000, ReconnectBackoff.next 4000)

[<Fact>]
let ``等待时长封顶在三十秒`` () =
    // 再长用户会以为程序已经放弃重连
    Assert.Equal(30000, ReconnectBackoff.next 16000)
    Assert.Equal(30000, ReconnectBackoff.next 30000)
    Assert.Equal(30000, ReconnectBackoff.next 120000)

[<Fact>]
let ``连续失败时真的退避而不是原地打转`` () =
    // 这条是为一个真实缺陷写的回归：原实现把重置放在「每次连接尝试」上，
    // 而重连自己就走那条路径，于是主机宕着时永远是每秒一次。
    let delays = ReconnectBackoff.schedule 6
    Assert.Equal<int list>([ 1000; 2000; 4000; 8000; 16000; 30000 ], delays)
    // 序列必须单调不减，否则就是「退避」名不副实
    for (earlier, later) in List.pairwise delays do
        Assert.True(later >= earlier, $"{later} 比前一次 {earlier} 还短")

[<Fact>]
let ``异常输入不会退出合法区间`` () =
    for weird in [ 0; -1; -100000 ] do
        let value = ReconnectBackoff.next weird
        Assert.InRange(value, ReconnectBackoff.baseDelayMs, ReconnectBackoff.maxDelayMs)

// 以下迁自旧 ClientStateAndWsClientTests。
[<Fact>]
let ``ReconnectBackoff base delay is 1000ms and max delay is 30000ms`` () =
    Assert.Equal(1000, ReconnectBackoff.baseDelayMs)
    Assert.Equal(30000, ReconnectBackoff.maxDelayMs)

[<Fact>]
let ``ReconnectBackoff next doubles delay under cap`` () =
    Assert.Equal(2000, ReconnectBackoff.next 1000)
    Assert.Equal(4000, ReconnectBackoff.next 2000)
    Assert.Equal(8000, ReconnectBackoff.next 4000)
    Assert.Equal(16000, ReconnectBackoff.next 8000)

[<Fact>]
let ``ReconnectBackoff next clamps at maxDelayMs`` () =
    Assert.Equal(30000, ReconnectBackoff.next 16000)
    Assert.Equal(30000, ReconnectBackoff.next 25000)
    Assert.Equal(30000, ReconnectBackoff.next 30000)
    Assert.Equal(30000, ReconnectBackoff.next 50000)

[<Fact>]
let ``ReconnectBackoff next handles smaller than baseDelayMs values safely`` () =
    // max baseDelayMs current ensures lower values bounce to at least 2000ms
    Assert.Equal(2000, ReconnectBackoff.next 0)
    Assert.Equal(2000, ReconnectBackoff.next 500)
    Assert.Equal(2000, ReconnectBackoff.next -100)

[<Fact>]
let ``ReconnectBackoff schedule produces correct sequence for failure counts`` () =
    let s0 = ReconnectBackoff.schedule 0
    Assert.Empty s0

    let s1 = ReconnectBackoff.schedule 1
    Assert.Equal<int list>([ 1000 ], s1)

    let s3 = ReconnectBackoff.schedule 3
    Assert.Equal<int list>([ 1000; 2000; 4000 ], s3)

    let s7 = ReconnectBackoff.schedule 7
    Assert.Equal<int list>([ 1000; 2000; 4000; 8000; 16000; 30000; 30000 ], s7)

[<Fact>]
let ``ReconnectBackoff schedule and state machine correctly clamps at max delay and resets`` () =
    // ReconnectBackoff specifies: baseDelayMs = 1000, maxDelayMs = 30000
    Assert.Equal(1000, ReconnectBackoff.baseDelayMs)
    Assert.Equal(30000, ReconnectBackoff.maxDelayMs)

    // 1. Exponential escalation: 1000 -> 2000 -> 4000 -> 8000 -> 16000 -> 30000 (clamped)
    let mutable currentDelay = ReconnectBackoff.baseDelayMs
    let expectedSequence = [ 2000; 4000; 8000; 16000; 30000; 30000; 30000 ]

    for expected in expectedSequence do
        currentDelay <- ReconnectBackoff.next currentDelay
        Assert.Equal(expected, currentDelay)

    // 2. ReconnectBackoff schedule generation helper
    let schedule5 = ReconnectBackoff.schedule 5
    Assert.Equal<int list>([ 1000; 2000; 4000; 8000; 16000 ], schedule5)

    // 3. Reset rule: on connection success or manual reconnect, delay resets to baseDelayMs
    let resetDelay = ReconnectBackoff.baseDelayMs
    Assert.Equal(1000, resetDelay)


// 本库补充的边界测试。
[<Fact>]
let ``失败次数为负是调用方错误，直接抛出`` () =
    Assert.Throws<System.ArgumentException>(fun () -> ReconnectBackoff.schedule -3 |> ignore) |> ignore

[<Fact>]
let ``长时间宕机时一直停在上限`` () =
    let s = ReconnectBackoff.schedule 100
    Assert.Equal(100, s.Length)
    Assert.True(s |> List.skip 5 |> List.forall ((=) ReconnectBackoff.maxDelayMs))
