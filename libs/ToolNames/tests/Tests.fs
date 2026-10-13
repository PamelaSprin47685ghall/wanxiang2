module ToolNamesTests

open Xunit
open Wanxiang.Server

[<Fact>]
let ``builtin ids and function names round trip`` () =
    Assert.Equal("builtin_file_read", ToolNames.builtinFunctionName "builtin:file.read")
    Assert.Equal("builtin:file.read", ToolNames.builtinStableId "builtin_file_read")

[<Fact>]
let ``mcp ids split at the first slash`` () =
    Assert.Equal("mcp:fs/a/b", ToolNames.mcpStableId "fs" "a/b")
    Assert.Equal("mcp_fs_read", ToolNames.mcpFunctionName "fs" "read")
    Assert.Equal(Some("fs", "a/b"), ToolNames.parseMcpId "mcp:fs/a/b")
    Assert.Equal(None, ToolNames.parseMcpId "mcp:fs")
    Assert.Equal(None, ToolNames.parseMcpId "builtin:time")
