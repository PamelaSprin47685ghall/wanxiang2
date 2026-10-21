namespace Wanxiang.Server

open System.Text
open System.Text.Json
open System.Text.Json.Nodes

/// 从消息 JSON（Microsoft.Extensions.AI 的 ChatMessage 形状）取出用户看得见的正文。
/// 只取 text 字段和 contents 里的 text 项，跳过思维链（$type 或 type 为 reasoning）。
module MessageText =

    /// 正文最多 maxChars 个字符，超出部分丢弃。
    let visible (maxChars: int) (payload: JsonNode) : string =
        let sb = StringBuilder()

        let rec walk (node: JsonNode) =
            if sb.Length < maxChars then
                match node with
                | null -> ()
                | :? JsonObject as o ->
                    let kind (key: string) =
                        let mutable n: JsonNode = null

                        if
                            o.TryGetPropertyValue(key, &n)
                            && not (isNull n)
                            && n.GetValueKind() = JsonValueKind.String
                        then
                            n.GetValue<string>()
                        else
                            null

                    if kind "$type" <> "reasoning" && kind "type" <> "reasoning" then
                        match kind "text" with
                        | null -> ()
                        | t ->
                            if sb.Length > 0 then
                                sb.Append '\n' |> ignore

                            sb.Append t |> ignore

                        let mutable contents: JsonNode = null

                        if
                            o.TryGetPropertyValue("contents", &contents)
                            && not (isNull contents)
                            && contents.GetValueKind() = JsonValueKind.Array
                        then
                            for c in contents.AsArray() do
                                walk c
                | :? JsonArray as a ->
                    for item in a do
                        walk item
                | _ -> ()

        walk payload

        if sb.Length > maxChars then
            sb.ToString(0, maxChars)
        else
            sb.ToString()
