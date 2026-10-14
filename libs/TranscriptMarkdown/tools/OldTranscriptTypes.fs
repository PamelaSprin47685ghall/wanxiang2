namespace Wanxiang.UI

open System

type TranscriptToolCall = { name: string; argumentsJson: string; result: string option }
type TranscriptAttachment = { fileName: string; sizeText: string }
type TranscriptMessage =
    { role: string; committedAt: DateTimeOffset option; reasoning: string; toolCalls: TranscriptToolCall list
      toolResults: (string * string) list; text: string; attachments: TranscriptAttachment list }
