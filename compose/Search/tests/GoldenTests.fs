module GoldenTests

// 金样由旧仓库 extract-ledger@1a1a4d7 的 Server/SearchService.fs 跑同一个 Scenario 生成：
// 结果页的线上字节（命中顺序、片段、下标、hasMore、truncated）与旧实现逐字节相同。

open System.IO
open Xunit

[<Fact>]
let ``search result bytes equal the old implementation`` () =
    let expected =
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "search.txt")).TrimEnd()

    Assert.Equal(expected, Scenario.all().TrimEnd())
