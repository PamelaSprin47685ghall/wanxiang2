namespace Wanxiang.Server

open System
open Microsoft.Extensions.AI
open Wanxiang.Agent
open Wanxiang.Config
open Wanxiang.Core

/// 构造送给 Provider 的上下文消息。
///
/// 三件事在这里发生，而且只在这里发生：
/// 1. 投影里的透明 JSON 还原成 `ChatMessage`；
/// 2. 消息携带的附件引用解析成模型真正能读的内容（图片 / 文本）；
/// 3. 按预算裁剪历史，避免长对话必然撞上上游的 context 上限。
module GenerationContext =

    /// 按条数与 token 预算裁剪，保留最近的对话（ContextTrim 库）。
    let trim (maxMessages: int) (maxTokens: int) (msgs: ChatMessage list) : ChatMessage list =
        ContextTrim.trim maxMessages maxTokens msgs

    /// 对话当前有效上下文。`loadBlob` 用于解析附件内容，
    /// `support` 声明当前传输吃得下哪些二进制媒体（各家差别很大）。
    let build
        (proj: Projection)
        (generation: GenerationDefaults)
        (support: MediaSupport)
        (loadBlob: string -> byte[] option)
        (convId: Guid)
        : ChatMessage list =
        match Projection.tryConversation proj convId with
        | None -> []
        | Some conv ->
            let restored =
                Projection.effectiveMessages proj conv
                |> List.choose (fun m ->
                    let refs = MessageSerde.attachmentRefs m.payloadJson
                    // 仅带附件的用户消息解析后 contents 为空，属正常情况
                    match MessageSerde.fromJsonNodeWith (not (List.isEmpty refs)) m.payloadJson with
                    | None -> None
                    | Some msg ->
                        if List.isEmpty refs then Some msg
                        else Some(AttachmentContent.appendTo support loadBlob refs msg))
                |> List.filter (fun m -> m.Contents.Count > 0)
            trim generation.maxContextMessages generation.maxContextTokens restored

    /// 供「重新生成」使用：最后一条用户消息的文本（用于标题生成与日志）。
    let lastUserText (msgs: ChatMessage list) : string =
        msgs
        |> List.filter (fun m -> m.Role = ChatRole.User)
        |> List.tryLast
        |> Option.map MessageSerde.textOf
        |> Option.defaultValue ""
