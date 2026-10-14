namespace Wanxiang.UI

module TranscriptMarkdown =
    let render (title: string) (messages: TranscriptMessage list) =
        let size s = Samples.sizes |> List.find (fun (t, _) -> t = s) |> snd
        let view (m: TranscriptMessage) : MessageView =
            { role = m.role; text = m.text; reasoning = m.reasoning
              toolCalls = m.toolCalls |> List.mapi (fun i c -> { ToolCallView.callId = string i; name = c.name; argumentsJson = c.argumentsJson; result = c.result })
              toolResults = m.toolResults
              attachments = m.attachments |> List.map (fun a -> { AttachmentRef.sha256 = ""; size = size a.sizeText; mediaType = ""; fileName = a.fileName })
              commitId = None; committedAt = m.committedAt }
        Export.toMarkdown title (List.map view messages)
