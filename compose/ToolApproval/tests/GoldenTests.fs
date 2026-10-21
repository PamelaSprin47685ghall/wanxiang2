module GoldenTests

// 金样由旧仓库 extract-ledger@1a1a4d7 的 Server/ToolApproval.fs 跑同一个 Scenario 生成：
// 审批请求、结果广播、「已处理」回复的线上字节和审批结果，与旧实现逐字节相同。

open System.IO
open Xunit

[<Fact>]
let ``approval events and outcomes equal the old implementation`` () =
    let expected =
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "approval.txt")).TrimEnd()

    Assert.Equal(expected, Scenario.all().TrimEnd())
