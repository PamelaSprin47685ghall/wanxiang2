namespace Wanxiang.Server

open System
open System.Text.Json
open System.Text.Json.Nodes

/// 从客户端发来的 JSON 对象里宽容地读字段：类型不对就当没给。字符串去首尾空白，空白串当没给；
/// 字符串列表去空、去重；字符串表只收字符串值。只依赖 BCL。从旧 ConfigMutation 拆出，行为不变。
module PayloadFields =

    let str (o: JsonObject) (key: string) : string option =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue(key, &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.String
        then
            let s = n.GetValue<string>()
            if String.IsNullOrWhiteSpace s then None else Some(s.Trim())
        else
            None

    let intOf (o: JsonObject) (key: string) : int option =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue(key, &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.Number
        then
            match n with
            | :? JsonValue as v ->
                match v.TryGetValue<int>() with
                | true, i -> Some i
                | _ -> None
            | _ -> None
        else
            None

    let floatOf (o: JsonObject) (key: string) : float option =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue(key, &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.Number
        then
            match n with
            | :? JsonValue as v ->
                match v.TryGetValue<float>() with
                | true, f -> Some f
                | _ -> None
            | _ -> None
        else
            None

    let boolOf (o: JsonObject) (key: string) (fallback: bool) : bool =
        let mutable n: JsonNode = null

        if o.TryGetPropertyValue(key, &n) && not (isNull n) then
            match n.GetValueKind() with
            | JsonValueKind.True -> true
            | JsonValueKind.False -> false
            | _ -> fallback
        else
            fallback

    let stringList (o: JsonObject) (key: string) : string list =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue(key, &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.Array
        then
            [
                for item in n.AsArray() do
                    if not (isNull item) && item.GetValueKind() = JsonValueKind.String then
                        let s = item.GetValue<string>()

                        if not (String.IsNullOrWhiteSpace s) then
                            s.Trim()
            ]
            |> List.distinct
        else
            []

    let stringMap (o: JsonObject) (key: string) : Map<string, string> =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue(key, &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.Object
        then
            n.AsObject()
            |> Seq.choose (fun kv ->
                if isNull kv.Value then
                    None
                elif kv.Value.GetValueKind() = JsonValueKind.String then
                    Some(kv.Key, kv.Value.GetValue<string>())
                else
                    None)
            |> Map.ofSeq
        else
            Map.empty
