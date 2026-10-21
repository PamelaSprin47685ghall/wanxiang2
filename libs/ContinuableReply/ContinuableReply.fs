namespace Wanxiang.Server

open System
open System.Text.Json.Nodes

/// 半截回复是否值得保存以便续写：助手角色，且 contents 里至少有一段非空白文本
/// （从旧 ChatOrchestrator.PartialText 拆出）。形状不对一律算否，不抛异常。
module ContinuableReply =

    let private str (n: JsonNode) =
        match n with
        | null -> None
        | n ->
            (try
                Some(n.GetValue<string>())
             with _ ->
                 None)

    let hasText (node: JsonNode) : bool =
        match node with
        | :? JsonObject as o ->
            let role = str o["role"] |> Option.defaultValue ""

            let hasText =
                match o["contents"] with
                | :? JsonArray as a ->
                    a
                    |> Seq.exists (fun c ->
                        match c with
                        | :? JsonObject as co -> str co["text"] |> Option.exists (String.IsNullOrWhiteSpace >> not)
                        | _ -> false)
                | _ -> false

            role = "assistant" && hasText
        | _ -> false
