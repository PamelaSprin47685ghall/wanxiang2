module GoldenTests

// 金样由旧仓库 d9651ed 的 Server/ServerModel.fs（协作者用已验证等价的包）跑同一场景生成，新实现须逐字节相同。

open System.IO
open Xunit

[<Fact>]
let ``ServerModel behaves byte for byte like the old implementation`` () =
    let expected = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "servermodel.txt")).TrimEnd()
    Assert.Equal(expected, Scenario.all().TrimEnd())
