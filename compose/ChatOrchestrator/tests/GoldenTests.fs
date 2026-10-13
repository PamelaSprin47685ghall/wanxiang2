module GoldenTests

// 金样由旧仓库 d9651ed 的 Server/GenerationContext.fs、TitleGenerator.fs、ChatOrchestrator.fs（其余协作者用已验证等价的包）
// 跑同一个真实组合场景生成：广播事件、记账消息、上游请求体与旧实现逐字节相同。

open System.IO
open Xunit

[<Fact>]
let ``chat orchestration behaves byte for byte like the old implementation`` () =
    let expected = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "orchestrator.txt")).TrimEnd()
    Assert.Equal(expected, Scenario.all().TrimEnd())
