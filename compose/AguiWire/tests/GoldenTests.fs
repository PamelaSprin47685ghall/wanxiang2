module GoldenTests

// 金样由旧仓库 extract-ledger@1a1a4d7 的 Protocol/WireAgui.fs 与 Agui/*.fs 跑同一个 Scenario 生成。
// AG-UI 文本是客户端（PWA、其他设备）看到的字节，任何一行变化都是协议变更。

open System.IO
open Xunit

[<Fact>]
let ``AG-UI bytes and decode results equal the old implementation`` () =
    let expected =
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "agui.txt")).TrimEnd()

    Assert.Equal(expected, Scenario.all().TrimEnd())
