module TranscriptMarkdownSamples
open System
open Wanxiang.UI

let private m role text =
    { role = role; committedAt = None; reasoning = ""; toolCalls = []; toolResults = []; text = text; attachments = [] }

let at = DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero)

/// 附件大小文案取旧 AttachmentRef.formatSize 对 512、1536、2097152 字节的输出
let sizes = [ "512 B", 512L; "1.5 KiB", 1536L; "2.0 MiB", 2097152L ]

let private tools =
    [ { m "assistant" "" with
          toolCalls =
              [ { name = "read"; argumentsJson = "{\"path\":\"a\"}"; result = Some "content ``` fence" }
                { name = "noargs"; argumentsJson = " "; result = None }
                { name = "blank"; argumentsJson = "{}"; result = Some "  " } ] }
      { m "tool" "" with toolResults = [ ("orphan", "result with ```` four") ]; committedAt = Some at } ]

let private files =
    [ { m "user" "看图" with
          attachments = [ { fileName = "a.png"; sizeText = "512 B" }; { fileName = "b.pdf"; sizeText = "1.5 KiB" } ] }
      { m "user" "" with attachments = [ { fileName = "c.zip"; sizeText = "2.0 MiB" } ] } ]

let transcripts : (string * TranscriptMessage list) list =
    [ "空对话", []
      "普通", [ { m "user" "你好" with committedAt = Some at }; m "assistant" "你好，有什么可以帮你？"; m "system" "sys"; m "custom" "x" ]
      "隐藏", [ m "assistant" ""; m "assistant" "   " ]
      "思考", [ { m "assistant" "答" with reasoning = "第一行  \n第二行\n" } ]
      "工具", tools
      "附件", files ]
