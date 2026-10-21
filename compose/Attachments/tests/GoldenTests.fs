module GoldenTests

// 金样由旧仓库 extract-ledger@1a1a4d7 的 Server/AttachmentStore.fs、Stderr.fs 跑同一个 Scenario 生成：
// 返回值、错误文本、元数据文件字节、stderr 行格式与旧实现逐字节相同。

open System.IO
open Xunit

[<Fact>]
let ``attachment store and stderr behave byte for byte like the old implementation`` () =
    let expected =
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "attachments.txt")).TrimEnd()

    Assert.Equal(expected, Scenario.all().TrimEnd())
