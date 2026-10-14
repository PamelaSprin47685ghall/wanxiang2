module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "transcriptmarkdown.txt"))

/// 金样由旧代码（从 旧 Views/Brand.fs 的 Export.toMarkdown，消息还原成旧 MessageView 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 旧 ConversationExportTests

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
