namespace Wanxiang.Server

open System.Text.Json
open System.Text.Json.Nodes

/// 从消息 JSON 里找出附件引用：任意深度上 `"type":"attachment"` 的对象。只依赖 BCL。
module AttachmentRefs =

    /// 返回 (sha256, mediaType, fileName, size) 列表，按出现顺序；缺的字符串字段为 ""，缺的 size 为 0。
    /// 附件对象内部不再往下找。
    let ofPayload (payload: JsonNode) : (string * string * string * int64) list =
        let results = System.Collections.Generic.List<string * string * string * int64>()
        let rec walk (node: JsonNode) =
            match node with
            | null -> ()
            | :? JsonArray as a ->
                for item in a do walk item
            | :? JsonObject as o ->
                let mutable t: JsonNode = null
                if o.TryGetPropertyValue("type", &t) && t <> null && t.GetValueKind() = JsonValueKind.String && t.GetValue<string>() = "attachment" then
                    let getStr k =
                        let mutable n: JsonNode = null
                        if o.TryGetPropertyValue(k, &n) && n <> null && n.GetValueKind() = JsonValueKind.String then n.GetValue<string>() else ""
                    let size =
                        let mutable n: JsonNode = null
                        if o.TryGetPropertyValue("size", &n) && n <> null && n.GetValueKind() = JsonValueKind.Number then
                            match n with
                            | :? JsonValue as v ->
                                match v.TryGetValue<int64>() with true, val64 -> val64 | _ -> 0L
                            | _ -> 0L
                        else 0L
                    results.Add(getStr "sha256", getStr "mediaType", getStr "fileName", size)
                else
                    for kv in o do walk kv.Value
            | _ -> ()
        walk payload
        List.ofSeq results
