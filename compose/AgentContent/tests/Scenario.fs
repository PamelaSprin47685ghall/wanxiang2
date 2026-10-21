module Scenario

// 新组合与旧 MessageSerde.fs、AttachmentContent.fs 共用的场景；输出逐字节比较。

open System
open System.Text
open System.Text.Json.Nodes
open Microsoft.Extensions.AI
open Wanxiang.Agent

let private show (c: AIContent) =
    match c with
    | :? TextContent as t -> sprintf "text %s" (t.Text.Replace("\n", "\\n"))
    | :? DataContent as d -> sprintf "data %s %d" d.MediaType d.Data.Length
    | :? FunctionCallContent as f -> sprintf "call %s %s" f.CallId f.Name
    | :? FunctionResultContent as r -> sprintf "result %s %O" r.CallId r.Result
    | other -> other.GetType().Name

let private showMsg (m: ChatMessage option) =
    match m with
    | None -> "none"
    | Some m -> sprintf "%s [%s]" (MessageSerde.roleName m) (m.Contents |> Seq.map show |> String.concat "; ")

let messages () =
    let b = StringBuilder()
    let line (s: string) = b.AppendLine s |> ignore
    let call = FunctionCallContent("c1", "builtin_time")

    let m =
        ChatMessage(ChatRole.Assistant, ResizeArray<AIContent>([ TextContent "hi" :> AIContent; call :> AIContent ]))

    line (MessageSerde.serialize m)
    line (MessageSerde.serialize (MessageSerde.toolResultMessage call "{\"ok\":1}"))
    line (MessageSerde.serialize (MessageSerde.textMessage ChatRole.System "s"))

    for raw in
        [
            """{"role":"user","contents":[{"text":"a"},{"type":"attachment","sha256":"AB","mediaType":"image/png","fileName":"p.png","size":5}]}"""
            """{"role":"user","contents":[{"type":"attachment","sha256":"ab"}]}"""
            """{"role":"assistant","contents":[{"$type":"text","text":"t"},{"text":"u"},{"$type":"functionCall","callId":"x","name":"f"}]}"""
            """{"role":"tool","contents":[{"$type":"functionResult","callId":"x","result":"r"}]}"""
            """{"role":"weird","contents":[{"text":""}]}"""
            """[1,2]"""
        ] do
        let n = JsonNode.Parse raw
        line (sprintf "strict %s" (showMsg (MessageSerde.fromJsonNode n)))
        line (sprintf "lenient %s" (showMsg (MessageSerde.fromJsonNodeWith true n)))
        line (sprintf "refs %A" (MessageSerde.attachmentRefs n))

    let back = MessageSerde.deserialize (MessageSerde.serialize m)

    line (
        sprintf
            "roundtrip %s toolCall=%b calls=%d text=%s"
            (showMsg back)
            (MessageSerde.hasToolCall back.Value)
            (MessageSerde.toolCalls back.Value).Length
            (MessageSerde.textOf back.Value)
    )

    line (sprintf "bad %b" (MessageSerde.deserialize "not json").IsNone)
    b.ToString()

let attachments () =
    let b = StringBuilder()
    let line (s: string) = b.AppendLine s |> ignore

    let blobs =
        Map
            [
                "img", Array.create 10 1uy
                "big", Array.zeroCreate (8 * 1024 * 1024 + 1)
                "pdf", Array.create 4 2uy
                "txt", Encoding.UTF8.GetBytes "let x = 1"
                "long", Encoding.UTF8.GetBytes(String('a', 128 * 1024 + 5))
                "empty", [||]
                "bin", Array.create 3 0uy
            ]

    let load k = Map.tryFind k blobs

    let refs =
        [
            "img", "IMAGE/PNG ", "p.png", 10L
            "big", "image/jpeg", "b.jpg", int64 (8 * 1024 * 1024 + 1)
            "pdf", "application/pdf", "d.pdf", 4L
            "pdf", "audio/x-custom", "s.ogg", 4L
            "txt", "text/x-fsharp", "a.fs", 9L
            "txt", "application/json", "noext", 9L
            "long", "text/plain", "l.txt", int64 (128 * 1024 + 5)
            "empty", "text/plain", "e.txt", 0L
            "gone", "image/png", "g.png", 2048L
            "bin", "application/zip", "z.zip", 3000000L
        ]

    for support, name in
        [
            MediaSupport.openAiCompatible, "openai"
            MediaSupport.anthropic, "anthropic"
            MediaSupport.ofProviderKind "gemini", "gemini"
        ] do
        for (sha, mt, fn, size) in refs do
            let r =
                {
                    sha256 = sha
                    mediaType = mt
                    fileName = fn
                    size = size
                }

            let out =
                AttachmentContent.resolve support load r
                |> List.map show
                |> List.map (fun s ->
                    if s.Length > 120 then
                        s.Substring(0, 80) + "…" + s.Substring(s.Length - 60)
                    else
                        s)

            line (sprintf "%s %s %s" name fn (String.Join(" | ", out)))

    let msg = MessageSerde.textMessage ChatRole.User "see"

    let msg =
        AttachmentContent.appendTo
            MediaSupport.gemini
            load
            [
                {
                    sha256 = "img"
                    mediaType = "image/gif"
                    fileName = "x.gif"
                    size = 10L
                }
            ]
            msg

    line (showMsg (Some msg))

    line (
        sprintf
            "%b %b %b %b %d %d %d"
            (AttachmentContent.isImage "image/webp")
            (AttachmentContent.isPdf "application/pdf")
            (AttachmentContent.isAudio "audio/zz")
            (AttachmentContent.isText "application/toml")
            AttachmentContent.maxInlineTextBytes
            AttachmentContent.maxInlineImageBytes
            AttachmentContent.maxInlineBinaryBytes
    )

    b.ToString()

let all () = messages () + attachments ()
