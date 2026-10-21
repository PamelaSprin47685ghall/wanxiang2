namespace Wanxiang.Server

open Microsoft.Extensions.AI

/// 按条数与 token 粗估预算裁剪上下文，保留最近的消息，且不让开头留下孤立的工具结果。
/// 只依赖 Microsoft.Extensions.AI。从旧 GenerationContext 拆出，行为不变。
module ContextTrim =

    /// 裁剪时始终保留的尾部消息数：太少会让模型丢掉刚才的问题。
    let private minKeptTail = 8

    /// 一条消息大致占多少 token 的粗估（4 字节 ≈ 1 token，CJK 偏保守）。
    let private estimateTokens (msg: ChatMessage) : int =
        let textLength =
            msg.Contents
            |> Seq.sumBy (fun c ->
                match c with
                | :? TextContent as t -> (if isNull t.Text then 0 else t.Text.Length)
                | :? TextReasoningContent as r -> (if isNull r.Text then 0 else r.Text.Length)
                | :? DataContent -> 1200 // 一张图的定价量级，避免图片被当成零成本
                | _ -> 64)

        8 + textLength / 2

    /// 工具调用与其结果必须成对出现，否则上游会拒绝整个请求。
    /// 裁剪时如果切在中间，就把开头的孤立 tool 结果一并丢掉。
    let private dropOrphanToolResults (msgs: ChatMessage list) : ChatMessage list =
        msgs |> List.skipWhile (fun m -> m.Role = ChatRole.Tool)

    /// 按条数与 token 预算裁剪，保留最近的对话。
    let trim (maxMessages: int) (maxTokens: int) (msgs: ChatMessage list) : ChatMessage list =
        let byCount =
            if maxMessages <= 0 || List.length msgs <= maxMessages then
                msgs
            else
                msgs |> List.skip (List.length msgs - maxMessages)

        let byBudget =
            if maxTokens <= 0 then
                byCount
            else
                let rec keep (acc: ChatMessage list) (budget: int) (rest: ChatMessage list) =
                    match rest with
                    | [] -> acc
                    | m :: tail ->
                        let cost = estimateTokens m

                        if budget - cost < 0 && List.length acc >= minKeptTail then
                            acc
                        else
                            keep (m :: acc) (budget - cost) tail

                keep [] maxTokens (List.rev byCount)

        dropOrphanToolResults byBudget
