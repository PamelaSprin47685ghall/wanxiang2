module TranscriptMarkdownTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``Markdown keeps orphan tool results timestamps and embedded fences`` () =
    let timestamp = DateTimeOffset.Parse "2026-09-07T12:00:00Z"
    let tool =
        { role = "tool"; committedAt = Some timestamp; reasoning = ""; toolCalls = []
          toolResults = [ ("orphan", "result with ``` inside") ]; text = ""; attachments = [] }
    let markdown = TranscriptMarkdown.render "工具记录" [ tool ]
    Assert.Contains("## 工具", markdown)
    Assert.Contains("result with ``` inside", markdown)
    Assert.Contains("````json", markdown)
    Assert.Contains("2026-09-07", markdown)

[<Fact>]
let ``messages without visible body or tool results are skipped`` () =
    let blank = { role = "assistant"; committedAt = None; reasoning = " "; toolCalls = []; toolResults = []; text = ""; attachments = [] }
    Assert.Equal("# t\n\n".Replace("\n", Environment.NewLine), TranscriptMarkdown.render "t" [ blank ])
