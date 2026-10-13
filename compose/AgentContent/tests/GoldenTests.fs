module GoldenTests

// 金样由旧仓库 d9651ed 的 Agent/MessageSerde.fs、AttachmentContent.fs 跑同一个 Scenario 生成。

open System.IO
open Xunit

[<Fact>]
let ``message serde and attachment content behave byte for byte like the old implementation`` () =
    let expected = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "agentcontent.txt")).TrimEnd()
    Assert.Equal(expected, Scenario.all().TrimEnd())
