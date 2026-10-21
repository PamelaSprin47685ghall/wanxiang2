module GoldenTests

// 金样由旧仓库 extract-ledger@d9651ed 的 Server/Doctor.fs（及其依赖的 Store、ServerModel、AttachmentStore）跑同一个 Scenario 生成：
// 检查项、说明文字、证据包清单与旧实现逐字节相同（临时目录、剩余空间、时间已统一替换）。

open System.IO
open Xunit

[<Fact>]
let ``doctor reports and recovery bundle equal the old implementation`` () =
    let expected =
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "doctor.txt")).TrimEnd()

    Assert.Equal(expected, Scenario.all().TrimEnd())
