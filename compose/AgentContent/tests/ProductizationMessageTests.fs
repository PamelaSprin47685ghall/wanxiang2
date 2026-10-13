module Wanxiang.Tests.ProductizationMessageTests

// 原样迁自旧仓库 d9651ed tests/Wanxiang.Tests/ProductizationTests.fs「消息解析」「附件送入模型」两节，
// 以及 NativeTransportTests.fs 的两条序列化往返（只改了模块名与 open）。

open System
open System.Text
open System.Text.Json.Nodes
open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Agent
open Wanxiang.Core

// ---------------------------------------------------------------- 消息解析

let private mixedUserMessage () : JsonNode =
    let o = JsonObject()
    o["role"] <- "user"
    let contents = JsonArray()
    let text = JsonObject()
    text["text"] <- "看看这张图"
    contents.Add text
    let attachment = JsonObject()
    attachment["type"] <- "attachment"
    attachment["sha256"] <- "ab12"
    attachment["size"] <- 42L
    attachment["mediaType"] <- "image/png"
    attachment["fileName"] <- "shot.png"
    contents.Add attachment
    o["contents"] <- contents
    o

[<Fact>]
let ``mixed message keeps its text while exposing attachment references`` () =
    let node = mixedUserMessage ()
    let refs = MessageSerde.attachmentRefs node
    Assert.Single refs |> ignore
    Assert.Equal("ab12", refs.Head.sha256)
    Assert.Equal("image/png", refs.Head.mediaType)
    let message = MessageSerde.fromJsonNode node |> Option.get
    Assert.Equal("看看这张图", MessageSerde.textOf message)

[<Fact>]
let ``attachment-only message parses when empty contents are allowed`` () =
    let o = JsonObject()
    o["role"] <- "user"
    let contents = JsonArray()
    let attachment = JsonObject()
    attachment["type"] <- "attachment"
    attachment["sha256"] <- "ff00"
    contents.Add attachment
    o["contents"] <- contents
    Assert.True((MessageSerde.fromJsonNodeWith true o).IsSome)
    Assert.True((MessageSerde.fromJsonNodeWith false o).IsNone)

// ---------------------------------------------------------------- 附件送入模型

let private attachment (mediaType: string) (fileName: string) (size: int64) : AttachmentReference =
    { sha256 = "ab12"; mediaType = mediaType; fileName = fileName; size = size }

let private resolveWith (support: MediaSupport) (bytes: byte[] option) (reference: AttachmentReference) =
    AttachmentContent.resolve support (fun _ -> bytes) reference

let private textOf (contents: AIContent list) =
    contents |> List.pick (function :? TextContent as t -> Some t.Text | _ -> None)

let private binaries (contents: AIContent list) =
    contents |> List.choose (function :? DataContent as d -> Some d | _ -> None)

[<Fact>]
let ``images reach the model as binary content`` () =
    let bytes = Array.init 64 byte
    let contents = resolveWith MediaSupport.openAiCompatible (Some bytes) (attachment "image/png" "shot.png" 64L)
    Assert.Contains(contents, fun c -> c :? DataContent)

[<Fact>]
let ``text attachments are inlined as a fenced block`` () =
    let bytes = Encoding.UTF8.GetBytes "let x = 1"
    let contents =
        resolveWith MediaSupport.openAiCompatible (Some bytes) (attachment "text/plain" "a.fs" (int64 bytes.Length))
    let text = textOf contents
    Assert.Contains("```fsharp", text)
    Assert.Contains("let x = 1", text)

[<Fact>]
let ``a lost blob degrades to an explanation instead of failing`` () =
    let contents = resolveWith MediaSupport.openAiCompatible None (attachment "image/png" "gone.png" 10L)
    Assert.Contains("丢失", textOf contents)
    Assert.DoesNotContain(contents, fun c -> c :? DataContent)

[<Fact>]
let ``PDF 在支持的传输上作为二进制递过去`` () =
    // 兼容层只吃图片，原生 Anthropic / Gemini 才吃 PDF
    let bytes = Array.init 128 byte
    let file = attachment "application/pdf" "spec.pdf" 128L
    Assert.Empty(binaries (resolveWith MediaSupport.openAiCompatible (Some bytes) file))
    Assert.Single(binaries (resolveWith MediaSupport.anthropic (Some bytes) file)) |> ignore
    Assert.Single(binaries (resolveWith MediaSupport.gemini (Some bytes) file)) |> ignore

[<Fact>]
let ``音频只在 Gemini 上作为二进制递过去`` () =
    let bytes = Array.init 128 byte
    let file = attachment "audio/mpeg" "note.mp3" 128L
    Assert.Empty(binaries (resolveWith MediaSupport.openAiCompatible (Some bytes) file))
    Assert.Empty(binaries (resolveWith MediaSupport.anthropic (Some bytes) file))
    Assert.Single(binaries (resolveWith MediaSupport.gemini (Some bytes) file)) |> ignore

[<Fact>]
let ``传输吃不下的类型要说清原因而不是默默省略`` () =
    // 「悄悄退化」是最坏的结果：用户以为模型看过那份文件
    let contents = resolveWith MediaSupport.openAiCompatible (Some(Array.init 32 byte)) (attachment "application/pdf" "a.pdf" 32L)
    Assert.Contains("不接受该类型", textOf contents)

[<Fact>]
let ``超过内联上限的二进制附件给出上限说明`` () =
    let tooBig = int64 AttachmentContent.maxInlineBinaryBytes + 1L
    let bytes = Array.zeroCreate<byte> (int tooBig)
    let contents = resolveWith MediaSupport.gemini (Some bytes) (attachment "application/pdf" "huge.pdf" tooBig)
    Assert.Empty(binaries contents)
    Assert.Contains("内联上限", textOf contents)

[<Fact>]
let ``工具调用 id 能穿过序列化往返`` () =
    // Gemini 的调用 id 是自己造的 `name@序号`，函数名要从 id 里拆回来。
    // 消息要落 NDJSON 再重放，id 在这一路上被改写或丢掉，工具结果就配不回调用了。
    let original =
        ChatMessage(
            ChatRole.Assistant,
            [| FunctionCallContent("echo@0", "echo", dict [ "text", box "hi" ]) :> AIContent |])
    let json = MessageSerde.serialize original
    match MessageSerde.deserialize json with
    | None -> failwith $"重放失败：{json}"
    | Some restored ->
        match restored.Contents |> Seq.choose (function :? FunctionCallContent as c -> Some c | _ -> None) |> List.ofSeq with
        | [ call ] ->
            Assert.Equal("echo@0", call.CallId)
            Assert.Equal("echo", call.Name)
            Assert.Equal("echo", GeminiRequest.nameOfCallId call.CallId)
        | other -> failwith $"重放后应当仍有一次工具调用，实际 {other.Length} 次"

[<Fact>]
let ``工具结果的调用 id 同样能穿过序列化往返`` () =
    let call = FunctionCallContent("echo@3", "echo", dict [ "text", box "x" ])
    let message = MessageSerde.toolResultMessage call "{\"ok\":true}"
    match MessageSerde.deserialize (MessageSerde.serialize message) with
    | None -> failwith "工具结果消息重放失败"
    | Some restored ->
        let ids =
            restored.Contents
            |> Seq.choose (function :? FunctionResultContent as r -> Some r.CallId | _ -> None)
            |> List.ofSeq
        Assert.Equal<string list>([ "echo@3" ], ids)
