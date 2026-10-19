module McpCheckTests
open Xunit
open Wanxiang.Server

[<Fact>]
let ``mcp 检查文字与旧实现一致`` () =
    let exists (p: string) = p = "/bin/sh"
    Assert.Equal((true, "ok"), McpCheck.run exists [ "a", true, Some "/bin/sh", None; "b", false, Some "/nope", None; "c", true, Some "/nope", Some "http://u" ])
    Assert.Equal((false, "x: enabled command not found: /nope"), McpCheck.run exists [ "x", true, Some "/nope", None ])

[<Fact>]
let ``HTTP 服务与停用服务不探测文件`` () =
    let probe (_: string) : bool = failwith "不应探测"
    Assert.Equal((true, "ok"), McpCheck.run probe [ "h", true, Some "x", Some "http://u"; "d", false, Some "x", None; "n", true, None, None ])

[<Fact>]
let ``多个缺失命令按顺序列出`` () =
    Assert.Equal((false, "a: enabled command not found: /x; b: enabled command not found: /y"),
                 McpCheck.run (fun c -> c = "/ok") [ "a", true, Some "/x", None; "o", true, Some "/ok", None; "b", true, Some "/y", None ])
