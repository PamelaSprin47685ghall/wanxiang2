module ToolSummaryTests

open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core

[<Fact>]
let ``builtin and MCP tools get a plain sentence`` () =
    let args (json: string) = JsonNode.Parse json
    Assert.Equal("查看当前时间", ToolSummary.describe "builtin_time" (args "{}"))

    Assert.Equal(
        "读取文件 /home/a/notes.txt",
        ToolSummary.describe "builtin_file_read" (args """{"path":"/home/a/notes.txt"}""")
    )

    Assert.Equal("列出文件夹 /home/a 里的内容", ToolSummary.describe "builtin_file_list" (args """{"path":"/home/a"}"""))
    Assert.Equal("读取一个文件（展开看参数）", ToolSummary.describe "builtin_file_read" (args "{}"))
    Assert.Equal("调用 fs_read_file（展开看参数）", ToolSummary.describe "mcp_fs_read_file" (args """{"p":1}"""))
    Assert.Equal("调用 something_else（展开看参数）", ToolSummary.describe "something_else" null)
    Assert.Equal("用户拒绝了此次工具调用。", ToolSummary.userDeniedText)
