module GoldenTests

// 金样由旧仓库 extract-ledger@1a1a4d7 的 Wanxiang.Agent 源码（ProviderSse、*Request、*Transport）跑同一个 Scenario 生成。
// 发给模型提供方的请求（地址、头、正文）和交给下游的增量，与旧实现逐字节相同。

open System.IO
open Xunit

[<Fact>]
let ``requests and decoded updates equal the old implementation byte for byte`` () =
    let expected =
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "transports.txt")).TrimEnd()

    Assert.Equal(expected, Scenario.all().TrimEnd())
