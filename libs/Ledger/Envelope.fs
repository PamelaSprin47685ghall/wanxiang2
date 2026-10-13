namespace Ledger

open System
open System.Globalization
open System.Text.Json.Nodes

/// 提交外壳的 JSON 形状（版本 1，不兼容任何旧格式）：
/// {"v":1,"id":"1","at":"…","stamp":{"id":"…","hash":"…"},"events":[…]}
/// 外壳只承载 JSON 形式的事件 Commit<JsonNode>；领域事件与 JSON 之间的转换由调用方配合 Commit.map 完成。
[<RequireQualifiedAccess>]
module Envelope =
    [<Literal>]
    let Version = 1

    type Parsed =
        | Parsed of Commit<JsonNode>
        | Invalid of string
        | NewerVersion of int

    let write (c: Commit<JsonNode>) : string =
        let o = JsonObject()
        o["v"] <- JsonValue.Create Version
        // id 用字符串承载：uint64 超出 double 精确范围
        o["id"] <- JsonValue.Create(c.id.ToString CultureInfo.InvariantCulture)
        o["at"] <- JsonValue.Create(c.at.ToString("o", CultureInfo.InvariantCulture))
        match c.stamp with
        | Some s ->
            let so = JsonObject()
            so["id"] <- JsonValue.Create s.commandId
            so["hash"] <- JsonValue.Create s.hash
            o["stamp"] <- so
        | None -> ()
        let arr = JsonArray()
        for e in c.events do arr.Add(e.DeepClone())
        o["events"] <- arr
        o.ToJsonString()

    let read (text: string) : Parsed =
        try
            match JsonNode.Parse text with
            | :? JsonObject as o ->
                let v = o["v"].GetValue<int>()
                if v > Version then NewerVersion v
                elif v <> Version then Invalid(sprintf "unknown version %d" v)
                else
                    let evs = o["events"].AsArray() |> List.ofSeq
                    if evs |> List.exists isNull then Invalid "null event" else
                    Parsed
                            { id = UInt64.Parse(o["id"].GetValue<string>(), CultureInfo.InvariantCulture)
                              at = DateTimeOffset.Parse(o["at"].GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                              stamp =
                                match o["stamp"] with
                                | :? JsonObject as s -> Some { commandId = s["id"].GetValue<string>(); hash = s["hash"].GetValue<string>() }
                                | _ -> None
                              events = evs |> List.map _.DeepClone() }
            | _ -> Invalid "record is not a JSON object"
        with e -> Invalid e.Message
