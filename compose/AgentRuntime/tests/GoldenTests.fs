module GoldenTests

// 金样由旧仓库 d9651ed 的 Agent/AgentRuntime.fs、HistoryProvider.fs 跑同一个 Scenario 生成：
// 失败归类、流式增量、用量、限流信号、历史回调与发给 Provider 的请求体与旧实现逐字节相同。

open System.IO
open Xunit

[<Fact>]
let ``agent runtime behaves byte for byte like the old implementation`` () =
    let expected = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "runtime.txt")).TrimEnd()
    Assert.Equal(expected, Scenario.all().TrimEnd())
