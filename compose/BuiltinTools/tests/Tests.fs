module BuiltinToolsTests

// 完整行为由 ToolRegistry 的金样（旧实现生成）钉住；这里只钉目录组成。

open Xunit
open Wanxiang.Config
open Wanxiang.Server

[<Fact>]
let ``file tools appear only when sandbox roots are configured`` () =
    let ids cfg =
        BuiltinTools.descriptors cfg |> List.map (fun (id, _, _) -> id)

    Assert.Equal<string list>([ "builtin:echo"; "builtin:time" ], ids ToolsConfig.defaults)

    Assert.Equal<string list>(
        [ "builtin:echo"; "builtin:time"; "builtin:file.read"; "builtin:file.list" ],
        ids
            { ToolsConfig.defaults with
                fileReadRoots = [ "/tmp" ]
            }
    )

    Assert.True(BuiltinTools.isDebug "builtin:echo")
