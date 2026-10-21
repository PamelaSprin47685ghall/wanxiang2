module ToolErrorSummaryTests

open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``no result means no error`` () =
    Assert.Equal(None, ToolErrorSummary.tryExtract None)

[<Fact>]
let ``json error object yields its message`` () =
    Assert.Equal(Some "inner", ToolErrorSummary.tryExtract (Some """{"error":{"message":"inner"}}"""))

[<Fact>]
let ``traceback summary is the last non blank line`` () =
    Assert.Equal(Some "KeyError: 'a'", ToolErrorSummary.tryExtract (Some "Traceback:\n  x\nKeyError: 'a'\n\n"))

// ---- 旧 ToolCallStatusTests 的错误摘要部分（状态与角标断言属于 MessageModel，留在旧仓库）

[<Theory>]
[<InlineData("操作已成功完成", false)>]
[<InlineData("操作已成功完成", true)>]
[<InlineData("""{"status":"ok","count":42}""", false)>]
[<InlineData("""{"result":"hello world"}""", false)>]
[<InlineData("""{"status":"success","data":[1,2,3]}""", false)>]
[<InlineData("""{"success":true,"result":"completed"}""", false)>]
[<InlineData("""{"error":null,"result":"ok"}""", false)>]
[<InlineData("Plain text output without any error", false)>]
[<InlineData("This documentation explains how 404 not found errors should be handled", false)>]
[<InlineData("No syntax error found in lines 1-100", false)>]
[<InlineData("To prevent command not found errors, make sure the path is in PATH", false)>]
[<InlineData("The access denied dialog was redesigned for improved accessibility", false)>]
let ``no error pattern yields no summary`` (resultText: string, _isStreaming: bool) =
    Assert.Equal(None, ToolErrorSummary.tryExtract (Some resultText))

[<Theory>]
[<InlineData("error: connection refused", "connection refused")>]
[<InlineData("Error: 未找到指定资源", "未找到指定资源")>]
[<InlineData("error:   ", "执行失败")>]
[<InlineData("""{"error": "参数校验失败"}""", "参数校验失败")>]
[<InlineData("""{"error": {"message": "权限不足"}}""", "权限不足")>]
[<InlineData("""{"isError": true}""", "执行失败")>]
[<InlineData("500 internal server error: gateway crash", "500 Internal Server Error")>]
[<InlineData("internal server error encountered", "Internal Server Error")>]
[<InlineData("command failed with exit code 1", "command failed with exit code 1")>]
[<InlineData("403 Forbidden", "403 Forbidden")>]
[<InlineData("404 Not Found", "404 Not Found")>]
[<InlineData("401 Unauthorized", "401 Unauthorized")>]
[<InlineData("HTTP/1.1 403 Forbidden", "403 Forbidden")>]
[<InlineData("HTTP 404 Not Found", "404 Not Found")>]
[<InlineData("401: Unauthorized", "401 Unauthorized")>]
[<InlineData("Permission denied", "Permission denied")>]
[<InlineData("access denied", "access denied")>]
[<InlineData("bash: /bin/secret: Permission denied", "bash: /bin/secret: Permission denied")>]
[<InlineData("Access denied for user 'admin'@'localhost'", "Access denied for user 'admin'@'localhost'")>]
[<InlineData("command not found", "command not found")>]
[<InlineData("bash: mytool: command not found", "bash: mytool: command not found")>]
[<InlineData("zsh: command not found: mytool", "zsh: command not found: mytool")>]
[<InlineData("fatal: not a git repository (or any of the parent directories): .git",
             "not a git repository (or any of the parent directories): .git")>]
[<InlineData("fatal:   ", "执行失败")>]
[<InlineData("""{"status":"error","message":"rate limit exceeded"}""", "rate limit exceeded")>]
[<InlineData("""{"status":"error"}""", "执行失败")>]
[<InlineData("""{"status":"failed","reason":"request timeout"}""", "request timeout")>]
[<InlineData("""{"status":"failure"}""", "执行失败")>]
[<InlineData("""{"success":false,"message":"item not found"}""", "item not found")>]
[<InlineData("""{"success":false}""", "执行失败")>]
[<InlineData("""{"is_error":true,"msg":"invalid token"}""", "invalid token")>]
let ``error patterns yield their summary`` (errorResultText: string, expectedSummary: string) =
    Assert.Equal(Some expectedSummary, ToolErrorSummary.tryExtract (Some errorResultText))
