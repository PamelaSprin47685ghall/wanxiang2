module GoldenTests

// 金样由旧仓库 d9651ed 的 Server/BuiltinTools.fs、ToolRegistry.fs 跑同一个 Scenario 生成。

open System.IO
open Xunit

[<Fact>]
let ``builtin tools and registry behave byte for byte like the old implementation`` () =
    let expected =
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "tools.txt")).TrimEnd()

    Assert.Equal(expected, Scenario.all().TrimEnd())
