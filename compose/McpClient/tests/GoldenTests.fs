module GoldenTests

// 金样由旧仓库 extract-ledger@1a1a4d7 的 Server/McpTransport.fs、McpClient.fs 跑同一个 Scenario 生成：
// 发给 MCP 服务的每一行（stdio）和每个请求的头与正文（HTTP），以及交给模型的工具结果文本，与旧实现逐字节相同。

open System.IO
open Xunit

[<Fact>]
let ``bytes sent to MCP servers and results given to the model equal the old implementation`` () =
    let expected =
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "mcp.txt")).TrimEnd()

    Assert.Equal(expected, Scenario.all().TrimEnd())
