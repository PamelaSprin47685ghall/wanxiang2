module Tests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``mcp 检查文字与旧实现一致`` () =
    let exists (p: string) = p = "/bin/sh"
    Assert.Equal((true, "ok"), McpCheck.run exists [ "a", true, Some "/bin/sh", None; "b", false, Some "/nope", None; "c", true, Some "/nope", Some "http://u" ])
    Assert.Equal((false, "x: enabled command not found: /nope"), McpCheck.run exists [ "x", true, Some "/nope", None ])
